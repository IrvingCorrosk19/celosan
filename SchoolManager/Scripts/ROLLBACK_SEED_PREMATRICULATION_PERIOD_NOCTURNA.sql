-- Rollback Fase 3: remover período de prematrícula nocturna creado por seed.
-- Seguro: se detiene si el período ya tiene prematrículas asociadas.

BEGIN;

DO $$
DECLARE
    v_school_id uuid := '6e42399f-6f17-4585-b92e-fa4fff02cb65';
    v_period_id uuid;
    v_usage_count integer;
BEGIN
    SELECT id
    INTO v_period_id
    FROM prematriculation_periods
    WHERE school_id = v_school_id
      AND name = 'Prematrícula Nocturna CELOSAM 2026 - 2T'
    ORDER BY created_at DESC
    LIMIT 1;

    IF v_period_id IS NULL THEN
        RAISE NOTICE 'No existe período sembrado para revertir.';
        RETURN;
    END IF;

    SELECT COUNT(*)
    INTO v_usage_count
    FROM prematriculations
    WHERE prematriculation_period_id = v_period_id;

    IF v_usage_count > 0 THEN
        RAISE EXCEPTION 'Rollback detenido: el período % tiene % prematrículas asociadas.',
            v_period_id, v_usage_count;
    END IF;

    DELETE FROM prematriculation_periods
    WHERE id = v_period_id;
END $$;

COMMIT;
