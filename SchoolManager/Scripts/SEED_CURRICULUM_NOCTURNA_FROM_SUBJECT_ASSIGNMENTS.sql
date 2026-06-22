-- Fase 4: crear malla curricular modular nocturna desde SubjectAssignment existente.
-- Fuente: subject_assignments + subjects + grade_levels + groups + shifts.
-- Idempotente, transaccional y sin duplicar materias base.

BEGIN;

DO $$
DECLARE
    v_school_id uuid := '6e42399f-6f17-4585-b92e-fa4fff02cb65';
    v_academic_year_id uuid := 'f7ccb57f-fa3e-4d9f-973b-552030c9852d';
    v_track_id uuid;
    v_inserted integer;
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM academic_years
        WHERE id = v_academic_year_id
          AND school_id = v_school_id
          AND is_active = true
    ) THEN
        RAISE EXCEPTION 'Año académico canónico no activo o no encontrado: %', v_academic_year_id;
    END IF;

    SELECT id
    INTO v_track_id
    FROM curriculum_tracks
    WHERE school_id = v_school_id
      AND academic_year_id = v_academic_year_id
      AND is_active = true
    ORDER BY created_at DESC
    LIMIT 1;

    IF v_track_id IS NULL THEN
        INSERT INTO curriculum_tracks (
            id,
            school_id,
            name,
            description,
            academic_year_id,
            is_active,
            created_at
        )
        VALUES (
            gen_random_uuid(),
            v_school_id,
            'Malla Modular Nocturna CELOSAM 2026',
            'Generada desde subject_assignments existentes de grupos con jornada Noche. No duplica subjects ni subject_assignments.',
            v_academic_year_id,
            true,
            CURRENT_TIMESTAMP
        )
        RETURNING id INTO v_track_id;
    END IF;

    WITH nocturnal_subjects AS (
        SELECT DISTINCT
            sa.subject_id,
            sa.grade_level_id,
            gl.name AS level_name,
            ROW_NUMBER() OVER (
                PARTITION BY sa.grade_level_id
                ORDER BY COALESCE(s.name, ''), sa.subject_id
            ) AS module_order
        FROM subject_assignments sa
        JOIN subjects s ON s.id = sa.subject_id
        JOIN grade_levels gl ON gl.id = sa.grade_level_id
        JOIN groups g ON g.id = sa.group_id
        LEFT JOIN shifts sh ON sh.id = g.shift_id
        WHERE sa."SchoolId" = v_school_id
          AND COALESCE(sa.status, 'Active') <> 'Closed'
          AND (g.shift = 'Noche' OR sh.name = 'Noche')
    ),
    inserted AS (
        INSERT INTO curriculum_subjects (
            id,
            curriculum_track_id,
            subject_id,
            grade_level_id,
            level_name,
            module_order,
            credits,
            minimum_passing_score,
            is_active,
            created_at
        )
        SELECT
            gen_random_uuid(),
            v_track_id,
            ns.subject_id,
            ns.grade_level_id,
            ns.level_name,
            ns.module_order,
            1.00,
            3.00,
            true,
            CURRENT_TIMESTAMP
        FROM nocturnal_subjects ns
        WHERE NOT EXISTS (
            SELECT 1
            FROM curriculum_subjects existing
            WHERE existing.curriculum_track_id = v_track_id
              AND existing.subject_id = ns.subject_id
              AND existing.grade_level_id IS NOT DISTINCT FROM ns.grade_level_id
        )
        RETURNING 1
    )
    SELECT COUNT(*) INTO v_inserted FROM inserted;

    RAISE NOTICE 'Curriculum nocturno sembrado/validado. Track %, materias nuevas: %', v_track_id, v_inserted;
END $$;

-- Validación: cada curriculum_subject debe tener al menos un subject_assignment compatible.
WITH active_track AS (
    SELECT id
    FROM curriculum_tracks
    WHERE school_id = '6e42399f-6f17-4585-b92e-fa4fff02cb65'
      AND academic_year_id = 'f7ccb57f-fa3e-4d9f-973b-552030c9852d'
      AND is_active = true
    ORDER BY created_at DESC
    LIMIT 1
)
SELECT cs.id, cs.subject_id, s.name AS subject_name, cs.grade_level_id, gl.name AS grade_name,
       COUNT(sa.id) AS compatible_subject_assignments
FROM curriculum_subjects cs
JOIN active_track t ON t.id = cs.curriculum_track_id
JOIN subjects s ON s.id = cs.subject_id
LEFT JOIN grade_levels gl ON gl.id = cs.grade_level_id
LEFT JOIN subject_assignments sa
       ON sa.subject_id = cs.subject_id
      AND sa.grade_level_id IS NOT DISTINCT FROM cs.grade_level_id
      AND sa."SchoolId" = '6e42399f-6f17-4585-b92e-fa4fff02cb65'
      AND COALESCE(sa.status, 'Active') <> 'Closed'
GROUP BY cs.id, cs.subject_id, s.name, cs.grade_level_id, gl.name
HAVING COUNT(sa.id) = 0;

SELECT gl.name AS grade, COUNT(*) AS curriculum_subjects
FROM curriculum_subjects cs
JOIN curriculum_tracks ct ON ct.id = cs.curriculum_track_id
LEFT JOIN grade_levels gl ON gl.id = cs.grade_level_id
WHERE ct.school_id = '6e42399f-6f17-4585-b92e-fa4fff02cb65'
  AND ct.academic_year_id = 'f7ccb57f-fa3e-4d9f-973b-552030c9852d'
  AND ct.is_active = true
  AND cs.is_active = true
GROUP BY gl.name
ORDER BY gl.name::int NULLS LAST;

COMMIT;
