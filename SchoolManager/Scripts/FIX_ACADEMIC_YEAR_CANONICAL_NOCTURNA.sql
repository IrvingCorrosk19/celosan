-- Fase 2: normalizar año académico canónico CELOSAM sin borrar historial.
-- Seguro: no elimina registros ni reescribe referencias históricas.
-- Efecto: deja activo solo el año académico canónico 2026 más usado por matrículas.

BEGIN;

DO $$
DECLARE
    v_school_id uuid := '6e42399f-6f17-4585-b92e-fa4fff02cb65';
    v_canonical_academic_year_id uuid := 'f7ccb57f-fa3e-4d9f-973b-552030c9852d';
    v_total_active_2026 integer;
    v_canonical_exists integer;
BEGIN
    SELECT COUNT(*)
    INTO v_total_active_2026
    FROM academic_years
    WHERE school_id = v_school_id
      AND name = '2026'
      AND is_active = true;

    SELECT COUNT(*)
    INTO v_canonical_exists
    FROM academic_years
    WHERE id = v_canonical_academic_year_id
      AND school_id = v_school_id
      AND name = '2026'
      AND start_date = '2026-01-01 00:00:00+00'::timestamptz
      AND end_date = '2026-12-31 23:59:59+00'::timestamptz;

    IF v_canonical_exists <> 1 THEN
        RAISE EXCEPTION 'Año académico canónico no existe o no coincide con CELOSAM/rango esperado: %',
            v_canonical_academic_year_id;
    END IF;

    IF v_total_active_2026 < 1 THEN
        RAISE EXCEPTION 'No hay años académicos activos 2026 para CELOSAM.';
    END IF;

    UPDATE academic_years
    SET is_active = true,
        updated_at = CURRENT_TIMESTAMP
    WHERE id = v_canonical_academic_year_id;

    UPDATE academic_years
    SET is_active = false,
        updated_at = CURRENT_TIMESTAMP,
        description = CONCAT(
            COALESCE(NULLIF(description, ''), ''),
            CASE WHEN COALESCE(description, '') = '' THEN '' ELSE E'\n' END,
            'Desactivado como duplicado 2026 CELOSAM; canónico: ',
            v_canonical_academic_year_id::text,
            '; fecha: ',
            CURRENT_TIMESTAMP::text
        )
    WHERE school_id = v_school_id
      AND name = '2026'
      AND id <> v_canonical_academic_year_id
      AND is_active = true;
END $$;

-- Validación post-cambio esperada: active_2026_count = 1
SELECT id, name, is_active, start_date, end_date, created_at, updated_at
FROM academic_years
WHERE school_id = '6e42399f-6f17-4585-b92e-fa4fff02cb65'
ORDER BY is_active DESC, created_at ASC;

COMMIT;
