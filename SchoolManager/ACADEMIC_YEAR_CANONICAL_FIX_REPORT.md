# Reporte de año académico canónico CELOSAM 2026

Fecha: 2026-06-22  
Fase: 2 - normalización segura de año académico  
Base: Render Producción

## Problema

La escuela CELOSAM (`6e42399f-6f17-4585-b92e-fa4fff02cb65`) tenía 13 registros activos para el año académico `2026`, todos con el mismo rango:

- Inicio: `2026-01-01 00:00:00+00`
- Fin: `2026-12-31 23:59:59+00`
- `is_active = true`

Esto afecta servicios como `AcademicYearService.GetActiveAcademicYearAsync`, que selecciona el año activo más reciente por `created_at`. Ese criterio puede escoger un año sin uso real en matrículas.

## Criterio de selección

Se eligió como año académico canónico el registro más usado por datos académicos reales:

`f7ccb57f-fa3e-4d9f-973b-552030c9852d`

Justificación:

- Escuela correcta: CELOSAM.
- Nombre correcto: `2026`.
- Rango correcto: `2026-01-01` a `2026-12-31`.
- Estado previo: activo.
- Mayor uso real:
  - 298 `student_assignments` activos.
  - 148 `student_subject_assignments` activos.
  - 172 `student_subject_assignments` totales.

## Uso detectado por año académico

| AcademicYearId | StudentAssignments activos | StudentSubjectAssignments activos | Total SSA |
|---|---:|---:|---:|
| `f7ccb57f-fa3e-4d9f-973b-552030c9852d` | 298 | 148 | 172 |
| `3629e8ea-584c-4424-b1ca-9c318a899457` | 44 | 141 | 141 |
| `13eeea1d-291d-4eee-8d6a-d89c95379ede` | 7 | 2 | 6 |
| otros 10 duplicados | 0 | 0 | 0 |

## Decisión

No se eliminará ningún año académico.

Se desactivarán los duplicados activos de `2026` dejando activo solo el canónico. No se reescribirán referencias históricas en esta fase. Las matrículas y materias existentes seguirán apuntando a sus IDs originales, pero los nuevos procesos que resuelvan "año activo" usarán el canónico.

## Script de corrección

`Scripts/FIX_ACADEMIC_YEAR_CANONICAL_NOCTURNA.sql`

Características:

- Transaccional (`BEGIN`/`COMMIT`).
- Idempotente.
- No borra registros.
- No modifica estudiantes.
- No modifica matrículas.
- No modifica materias.
- No modifica `subject_assignments`.
- Valida que el canónico exista y tenga rango esperado.

## Script de rollback

`Scripts/ROLLBACK_FIX_ACADEMIC_YEAR_CANONICAL_NOCTURNA.sql`

Características:

- Reactiva los 12 duplicados de `2026`.
- No restaura `updated_at`/`description` exactos; para restauración exacta se debe usar el backup/snapshot.
- No modifica matrículas ni referencias.

## Riesgo residual

Algunas matrículas históricas seguirán referenciando años 2026 desactivados. Esto es aceptable para preservar historial, pero cualquier consulta futura que filtre `academic_years.is_active = true` al navegar desde matrículas históricas podría ocultarlas. El código actual principal de matrícula no exige que el `academic_year_id` referenciado esté activo para listar asignaciones activas.

## Validación requerida después del cambio

- Confirmar que solo existe un `academic_years` activo para CELOSAM 2026.
- Confirmar que no cambió el conteo de estudiantes activos.
- Confirmar que no cambió el conteo de `student_assignments` activos.
- Confirmar que no cambió el conteo de `student_subject_assignments` activos.
