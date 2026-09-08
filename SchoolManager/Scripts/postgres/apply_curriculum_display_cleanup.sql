-- Ajustes de presentación posteriores a la carga MEDUCA.
-- No cambia las 191 horas ni el total 578.
-- Desactiva CLS duplicadas de Informática (sin horas).
-- Separa Lógica y Filosofía de Electricidad reutilizando SubjectId existentes.
-- Solo localhost / schoolmanager_daqf. No ejecutar contra Render.
-- No toca subject_assignments, subjects, Schedule, matrícula, notas ni asistencia.

\set ON_ERROR_STOP on
SET client_encoding TO 'UTF8';

BEGIN;

DO $fix$
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
    IF v_cls <> 206 THEN
        RAISE EXCEPTION 'Se esperaban 206 CLS; hay %', v_cls;
    END IF;

    SELECT COUNT(*), SUM(h.hours) INTO v_hours, v_sum
    FROM curriculum_load_hours h
    JOIN curriculum_load_subjects cls ON cls.id = h.curriculum_load_subject_id
    WHERE cls.school_id = v_school;

    IF v_hours <> 191 OR v_sum <> 578 THEN
        RAISE EXCEPTION 'Estado inicial de horas inválido: % celdas, total %', v_hours, v_sum;
    END IF;

    SELECT id INTO v_b1 FROM curriculum_blocks WHERE code = 'B1';
    IF v_b1 IS NULL THEN
        RAISE EXCEPTION 'Falta curriculum_blocks B1';
    END IF;

    -- Informática: solo las 4 CLS sin horas, no oficiales.
    SELECT COUNT(*) INTO v_n
    FROM curriculum_load_subjects cls
    JOIN subjects s ON s.id = cls.subject_id
    WHERE cls.id IN (
        '27116cf8-66bc-47d5-b1f6-e16ce4ee8ac5'::uuid,
        '18488285-2c9a-4ba2-8b13-1d02738bd029'::uuid,
        '021025d5-f718-4156-804f-f01234da182b'::uuid,
        'f16774fd-415f-4ae6-a780-b545ead091d0'::uuid)
      AND cls.school_id = v_school
      AND cls.is_active = true
      AND NOT EXISTS (
          SELECT 1 FROM curriculum_load_hours h WHERE h.curriculum_load_subject_id = cls.id);

    IF v_n <> 4 THEN
        RAISE EXCEPTION 'Las 4 CLS duplicadas de Informática no están en el estado esperado; hay %', v_n;
    END IF;

    UPDATE curriculum_load_subjects
    SET is_active = false,
        updated_at = CURRENT_TIMESTAMP
    WHERE id IN (
        '27116cf8-66bc-47d5-b1f6-e16ce4ee8ac5'::uuid,
        '18488285-2c9a-4ba2-8b13-1d02738bd029'::uuid,
        '021025d5-f718-4156-804f-f01234da182b'::uuid,
        'f16774fd-415f-4ae6-a780-b545ead091d0'::uuid);

    -- Electricidad: SubjectId individuales ya existentes.
    IF (SELECT COUNT(*) FROM subjects WHERE id = 'a4d67d79-14d3-4a80-aa18-cab073936fc6' AND name = 'LÓGICA') <> 1
       OR (SELECT COUNT(*) FROM subjects WHERE id = '5c5f09e8-f240-46f8-8d7f-78771ee0b5a3' AND name = 'FILOSOFÍA') <> 1 THEN
        RAISE EXCEPTION 'No existen los SubjectId individuales únicos de LÓGICA y FILOSOFÍA';
    END IF;

    IF EXISTS (SELECT 1 FROM curriculum_load_subjects WHERE id IN (v_new_logica, v_new_filo)) THEN
        RAISE EXCEPTION 'Las CLS nuevas de Lógica/Filosofía ya existen';
    END IF;

    SELECT COUNT(*) INTO v_n
    FROM curriculum_load_hours
    WHERE id = 'c19100a5-5e10-4000-a000-6e42399f6f17'
      AND curriculum_load_subject_id = '003310bd-b18a-4f48-befd-2b9b8ab4e971'
      AND curriculum_block_id = v_b1
      AND hours = 1;

    IF v_n <> 1 THEN
        RAISE EXCEPTION 'La hora Electricidad 11 Lógica/Filosofía B1=1 no está en su CLS combinada';
    END IF;

    SELECT COUNT(*) INTO v_n
    FROM curriculum_load_hours
    WHERE id = 'c19100a6-5e10-4000-a000-6e42399f6f17'
      AND curriculum_load_subject_id = 'e2aca6e4-f433-4cc0-bb76-c36b2e7f8c60'
      AND curriculum_block_id = v_b1
      AND hours = 1;

    IF v_n <> 1 THEN
        RAISE EXCEPTION 'La hora Electricidad 12 Lógica/Filosofía B1=1 no está en su CLS combinada';
    END IF;

    INSERT INTO curriculum_load_subjects (
        id, school_id, specialty_id, grade_level_id, area_id, subject_id, is_active, created_at
    ) VALUES
    (v_new_logica, v_school,
     'a89c4c24-5e5e-4d27-90b3-08421ecfb3bb'::uuid,
     'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid,
     '7d91cd0b-c28e-4e15-bfa3-efbe9187e55b'::uuid,
     'a4d67d79-14d3-4a80-aa18-cab073936fc6'::uuid,
     true, CURRENT_TIMESTAMP),
    (v_new_filo, v_school,
     'a89c4c24-5e5e-4d27-90b3-08421ecfb3bb'::uuid,
     '171563c1-3c14-486f-9d00-a7cf1b67f9a1'::uuid,
     '7d91cd0b-c28e-4e15-bfa3-efbe9187e55b'::uuid,
     '5c5f09e8-f240-46f8-8d7f-78771ee0b5a3'::uuid,
     true, CURRENT_TIMESTAMP);

    UPDATE curriculum_load_hours
    SET curriculum_load_subject_id = v_new_logica,
        updated_at = CURRENT_TIMESTAMP
    WHERE id = 'c19100a5-5e10-4000-a000-6e42399f6f17';

    UPDATE curriculum_load_hours
    SET curriculum_load_subject_id = v_new_filo,
        updated_at = CURRENT_TIMESTAMP
    WHERE id = 'c19100a6-5e10-4000-a000-6e42399f6f17';

    UPDATE curriculum_load_subjects
    SET is_active = false,
        updated_at = CURRENT_TIMESTAMP
    WHERE id IN (
        '003310bd-b18a-4f48-befd-2b9b8ab4e971'::uuid,
        'e2aca6e4-f433-4cc0-bb76-c36b2e7f8c60'::uuid);

    SELECT COUNT(*) INTO v_cls FROM curriculum_load_subjects WHERE school_id = v_school;
    IF v_cls <> 208 THEN
        RAISE EXCEPTION 'Tras el cleanup deben existir 208 CLS; hay %', v_cls;
    END IF;

    SELECT COUNT(*), SUM(h.hours) INTO v_hours, v_sum
    FROM curriculum_load_hours h
    JOIN curriculum_load_subjects cls ON cls.id = h.curriculum_load_subject_id
    WHERE cls.school_id = v_school;

    IF v_hours <> 191 OR v_sum <> 578 THEN
        RAISE EXCEPTION 'Las horas no deben cambiar: % celdas, total %', v_hours, v_sum;
    END IF;

    IF (SELECT COUNT(*) FROM subject_assignments WHERE "SchoolId" = v_school) <> 435 THEN
        RAISE EXCEPTION 'subject_assignments cambió';
    END IF;
END
$fix$;

COMMIT;
