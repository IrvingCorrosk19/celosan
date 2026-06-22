-- Rollback Fase 4: remover malla curricular nocturna sembrada.
-- Seguro: se detiene si ya existe uso por selecciones, matrículas modulares,
-- créditos, equivalencias o promociones.

BEGIN;

DO $$
DECLARE
    v_school_id uuid := '6e42399f-6f17-4585-b92e-fa4fff02cb65';
    v_academic_year_id uuid := 'f7ccb57f-fa3e-4d9f-973b-552030c9852d';
    v_track_id uuid;
    v_usage_count integer;
BEGIN
    SELECT id
    INTO v_track_id
    FROM curriculum_tracks
    WHERE school_id = v_school_id
      AND academic_year_id = v_academic_year_id
      AND name = 'Malla Modular Nocturna CELOSAM 2026'
    ORDER BY created_at DESC
    LIMIT 1;

    IF v_track_id IS NULL THEN
        RAISE NOTICE 'No existe track sembrado para revertir.';
        RETURN;
    END IF;

    SELECT
        (SELECT COUNT(*)
         FROM student_prematriculation_subject_selections s
         JOIN curriculum_subjects cs ON cs.id = s.curriculum_subject_id
         WHERE cs.curriculum_track_id = v_track_id)
      + (SELECT COUNT(*)
         FROM student_subject_assignments ssa
         JOIN curriculum_subjects cs ON cs.id = ssa.curriculum_subject_id
         WHERE cs.curriculum_track_id = v_track_id)
      + (SELECT COUNT(*)
         FROM student_academic_credits c
         JOIN curriculum_subjects cs ON cs.id = c.curriculum_subject_id
         WHERE cs.curriculum_track_id = v_track_id)
      + (SELECT COUNT(*)
         FROM student_subject_equivalency_items e
         JOIN curriculum_subjects cs ON cs.id = e.curriculum_subject_id
         WHERE cs.curriculum_track_id = v_track_id)
      + (SELECT COUNT(*)
         FROM subject_promotion_records r
         JOIN curriculum_subjects cs ON cs.id = r.curriculum_subject_id
         WHERE cs.curriculum_track_id = v_track_id)
    INTO v_usage_count;

    IF v_usage_count > 0 THEN
        RAISE EXCEPTION 'Rollback detenido: la malla % ya tiene % referencias académicas.',
            v_track_id, v_usage_count;
    END IF;

    DELETE FROM curriculum_subjects
    WHERE curriculum_track_id = v_track_id;

    DELETE FROM curriculum_tracks
    WHERE id = v_track_id;
END $$;

COMMIT;
