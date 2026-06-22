-- Fase 3: crear/habilitar período activo de prematrícula nocturna CELOSAM.
-- Idempotente y transaccional. No toca estudiantes, matrículas ni materias.

BEGIN;

DO $$
DECLARE
    v_school_id uuid := '6e42399f-6f17-4585-b92e-fa4fff02cb65';
    v_academic_year_id uuid := 'f7ccb57f-fa3e-4d9f-973b-552030c9852d';
    v_trimester_id uuid := '7038b0cd-581b-470c-8753-618f34929a5a'; -- 2T activo
    v_period_id uuid;
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

    IF NOT EXISTS (
        SELECT 1
        FROM trimester
        WHERE id = v_trimester_id
          AND school_id = v_school_id
          AND is_active = true
    ) THEN
        RAISE EXCEPTION 'Trimestre 2T activo no encontrado para CELOSAM: %', v_trimester_id;
    END IF;

    SELECT id
    INTO v_period_id
    FROM prematriculation_periods
    WHERE school_id = v_school_id
      AND is_active = true
      AND start_date <= CURRENT_TIMESTAMP
      AND end_date >= CURRENT_TIMESTAMP
    ORDER BY created_at DESC
    LIMIT 1;

    IF v_period_id IS NULL THEN
        INSERT INTO prematriculation_periods (
            id,
            school_id,
            name,
            academic_year_id,
            trimester_id,
            start_date,
            end_date,
            is_active,
            max_capacity_per_group,
            max_subjects_allowed,
            auto_assign_by_shift,
            required_amount,
            created_at
        )
        VALUES (
            gen_random_uuid(),
            v_school_id,
            'Prematrícula Nocturna CELOSAM 2026 - 2T',
            v_academic_year_id,
            v_trimester_id,
            CURRENT_TIMESTAMP - INTERVAL '1 hour',
            '2026-12-31 23:59:59+00'::timestamptz,
            true,
            40,
            12,
            true,
            0,
            CURRENT_TIMESTAMP
        )
        RETURNING id INTO v_period_id;
    ELSE
        UPDATE prematriculation_periods
        SET academic_year_id = COALESCE(academic_year_id, v_academic_year_id),
            trimester_id = COALESCE(trimester_id, v_trimester_id),
            max_capacity_per_group = CASE WHEN max_capacity_per_group <= 0 THEN 40 ELSE max_capacity_per_group END,
            max_subjects_allowed = COALESCE(max_subjects_allowed, 12),
            auto_assign_by_shift = true,
            updated_at = CURRENT_TIMESTAMP
        WHERE id = v_period_id;
    END IF;
END $$;

SELECT id, name, school_id, academic_year_id, trimester_id, start_date, end_date,
       is_active, max_capacity_per_group, max_subjects_allowed, auto_assign_by_shift
FROM prematriculation_periods
WHERE school_id = '6e42399f-6f17-4585-b92e-fa4fff02cb65'
ORDER BY is_active DESC, created_at DESC;

COMMIT;
