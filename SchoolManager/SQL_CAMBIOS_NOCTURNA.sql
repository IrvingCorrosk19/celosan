-- =============================================================================
-- SQL_CAMBIOS_NOCTURNA.sql
-- Jornada nocturna / multi-matrícula — reglas de integridad y trazabilidad
-- Última actualización: 2026-04-18 (fase producción)
-- =============================================================================
--
-- RESUMEN DE LO APLICADO EN ENTORNO (vía migración EF
--   20260418204938_UqActiveStudentAssignmentEnrollment):
--
--   1) Deduplicación de matrículas activas: por cada partición
--      (student_id, grade_id, group_id, shift_id, academic_year_id) con
--      is_active = true, se conserva la fila más reciente (created_at / start_date)
--      y las demás se marcan is_active = false con end_date.
--   2) Inscripciones student_subject_assignments activas ligadas a esas
--      matrículas duplicadas se inactivan (status 'Inactive').
--   3) Índice único parcial PostgreSQL 15+:
--        CREATE UNIQUE INDEX uq_student_assignments_active_enrollment
--        ON student_assignments (student_id, grade_id, group_id, shift_id, academic_year_id)
--        NULLS NOT DISTINCT
--        WHERE is_active = true;
--
-- Ya existía en el modelo: índice único parcial en student_subject_assignments
--   ix_student_subject_assignments_active_unique
--   (student_id, subject_assignment_id, academic_year_id) WHERE is_active = true
--
-- =============================================================================
-- REGLAS DE NEGOCIO (MATRÍCULA VÁLIDA)
-- =============================================================================
--
-- * Válida: una fila en student_assignments con is_active = true por cada
--   contexto académico distinto: (estudiante, grado, grupo, jornada, año).
-- * Repetición permitida en el tiempo: varias filas con el mismo contexto si
--   las anteriores están is_active = false (historial).
-- * Nocturna vs diurna: se distingue por shift_id del grupo (y enrollment_type
--   en aplicación). A nivel BD la unicidad es por shift_id (NULL tratado como
--   valor único común vía NULLS NOT DISTINCT — solo una matrícula activa con
--   shift_id NULL en esa combinación de grado/grupo/año).
-- * Multi-matrícula intencional: el MISMO estudiante puede tener varias filas
--   activas si difieren en group_id o shift_id o academic_year_id.
--
-- =============================================================================
-- SCRIPT IDEMPOTENTE (otro servidor / respaldo): dedupe + índice
-- =============================================================================
-- Ejecutar en orden. En tablas muy grandes, sustituir CREATE UNIQUE INDEX por
-- CREATE UNIQUE INDEX CONCURRENTLY (fuera de transacción).

BEGIN;

WITH ranked AS (
    SELECT id,
           ROW_NUMBER() OVER (
               PARTITION BY student_id, grade_id, group_id, shift_id, academic_year_id
               ORDER BY COALESCE(created_at, start_date, NOW()) DESC, id
           ) AS rn
    FROM student_assignments
    WHERE is_active = true
),
dup_sa AS (SELECT id FROM ranked WHERE rn > 1)
UPDATE student_subject_assignments ssa
SET is_active = false,
    end_date = COALESCE(ssa.end_date, NOW()),
    status = 'Inactive'
WHERE ssa.is_active = true
  AND ssa.student_assignment_id IS NOT NULL
  AND ssa.student_assignment_id IN (SELECT id FROM dup_sa);

WITH ranked AS (
    SELECT id,
           ROW_NUMBER() OVER (
               PARTITION BY student_id, grade_id, group_id, shift_id, academic_year_id
               ORDER BY COALESCE(created_at, start_date, NOW()) DESC, id
           ) AS rn
    FROM student_assignments
    WHERE is_active = true
)
UPDATE student_assignments sa
SET is_active = false,
    end_date = COALESCE(sa.end_date, NOW())
FROM ranked r
WHERE sa.id = r.id AND r.rn > 1;

COMMIT;

-- Crear índice solo si no existe (manual en servidores sin migración EF):
-- CREATE UNIQUE INDEX IF NOT EXISTS uq_student_assignments_active_enrollment
-- ON student_assignments (student_id, grade_id, group_id, shift_id, academic_year_id)
-- NULLS NOT DISTINCT
-- WHERE is_active = true;

-- =============================================================================
-- VERIFICACIÓN POST-DEPLOY
-- =============================================================================
-- Debe devolver 0 filas:
--
-- SELECT student_id, grade_id, group_id, shift_id, academic_year_id, COUNT(*)
-- FROM student_assignments
-- WHERE is_active = true
-- GROUP BY 1,2,3,4,5
-- HAVING COUNT(*) > 1;
--
-- =============================================================================
-- NO EJECUTAR EN PRODUCCIÓN SIN RESPALDO: TRUNCATE / reconstrucción total
-- =============================================================================
-- La aplicación no requiere TRUNCATE global para nocturna. Si se reconstruye
-- un entorno desde cero, usar respaldo lógico o pipeline de seeds propio.
