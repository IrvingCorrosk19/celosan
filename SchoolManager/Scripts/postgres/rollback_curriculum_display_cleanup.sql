-- Revierte apply_curriculum_display_cleanup.sql.
-- Restaura horas en las CLS combinadas de Electricidad.
-- Reactiva esas CLS y las 4 de Informática.
-- Elimina únicamente las dos CLS nuevas.
-- Conserva las 191 horas y el total 578.

\set ON_ERROR_STOP on
SET client_encoding TO 'UTF8';

BEGIN;

DO $rb$
DECLARE
    v_school uuid := '6e42399f-6f17-4585-b92e-fa4fff02cb65';
    v_n integer;
    v_hours integer;
    v_sum numeric;
    v_cls integer;
    v_new_logica uuid := 'c15c11a1-5e10-4000-a000-6e42399f6f17';
    v_new_filo uuid := 'c15c12a2-5e10-4000-a000-6e42399f6f17';
    v_b1 uuid;
BEGIN
    IF (SELECT COUNT(*) FROM schools WHERE id = v_school AND name ILIKE '%San Miguelito%') <> 1 THEN
        RAISE EXCEPTION 'SchoolId % no corresponde a CELO San Miguelito', v_school;
    END IF;

    SELECT COUNT(*) INTO v_cls FROM curriculum_load_subjects WHERE school_id = v_school;
    IF v_cls <> 208 THEN
        RAISE EXCEPTION 'Se esperaban 208 CLS tras el cleanup; hay %', v_cls;
    END IF;

    SELECT id INTO v_b1 FROM curriculum_blocks WHERE code = 'B1';

    SELECT COUNT(*) INTO v_n
    FROM curriculum_load_hours
    WHERE id = 'c19100a5-5e10-4000-a000-6e42399f6f17'
      AND curriculum_load_subject_id = v_new_logica
      AND curriculum_block_id = v_b1
      AND hours = 1;

    IF v_n <> 1 THEN
        RAISE EXCEPTION 'La hora de Lógica 11 fue modificada; no se revierte';
    END IF;

    SELECT COUNT(*) INTO v_n
    FROM curriculum_load_hours
    WHERE id = 'c19100a6-5e10-4000-a000-6e42399f6f17'
      AND curriculum_load_subject_id = v_new_filo
      AND curriculum_block_id = v_b1
      AND hours = 1;

    IF v_n <> 1 THEN
        RAISE EXCEPTION 'La hora de Filosofía 12 fue modificada; no se revierte';
    END IF;

    UPDATE curriculum_load_hours
    SET curriculum_load_subject_id = '003310bd-b18a-4f48-befd-2b9b8ab4e971',
        updated_at = CURRENT_TIMESTAMP
    WHERE id = 'c19100a5-5e10-4000-a000-6e42399f6f17';

    UPDATE curriculum_load_hours
    SET curriculum_load_subject_id = 'e2aca6e4-f433-4cc0-bb76-c36b2e7f8c60',
        updated_at = CURRENT_TIMESTAMP
    WHERE id = 'c19100a6-5e10-4000-a000-6e42399f6f17';

    UPDATE curriculum_load_subjects
    SET is_active = true,
        updated_at = CURRENT_TIMESTAMP
    WHERE id IN (
        '003310bd-b18a-4f48-befd-2b9b8ab4e971'::uuid,
        'e2aca6e4-f433-4cc0-bb76-c36b2e7f8c60'::uuid);

    DELETE FROM curriculum_load_subjects
    WHERE id IN (v_new_logica, v_new_filo);

    GET DIAGNOSTICS v_n = ROW_COUNT;
    IF v_n <> 2 THEN
        RAISE EXCEPTION 'Debían eliminarse exactamente 2 CLS nuevas; eliminó %', v_n;
    END IF;

    UPDATE curriculum_load_subjects
    SET is_active = true,
        updated_at = CURRENT_TIMESTAMP
    WHERE id IN (
        '27116cf8-66bc-47d5-b1f6-e16ce4ee8ac5'::uuid,
        '18488285-2c9a-4ba2-8b13-1d02738bd029'::uuid,
        '021025d5-f718-4156-804f-f01234da182b'::uuid,
        'f16774fd-415f-4ae6-a780-b545ead091d0'::uuid);

    SELECT COUNT(*) INTO v_cls FROM curriculum_load_subjects WHERE school_id = v_school;
    IF v_cls <> 206 THEN
        RAISE EXCEPTION 'Tras rollback deben existir 206 CLS; hay %', v_cls;
    END IF;

    SELECT COUNT(*), SUM(h.hours) INTO v_hours, v_sum
    FROM curriculum_load_hours h
    JOIN curriculum_load_subjects cls ON cls.id = h.curriculum_load_subject_id
    WHERE cls.school_id = v_school;

    IF v_hours <> 191 OR v_sum <> 578 THEN
        RAISE EXCEPTION 'Las horas no volvieron al estado MEDUCA: % celdas, total %', v_hours, v_sum;
    END IF;
END
$rb$;

COMMIT;
