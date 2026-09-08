-- Carga inicial de horas curriculares B1/B2 desde las hojas MEDUCA (escuela nocturna).
-- UUID deterministas explícitos (los mismos 191 IDs que usa el rollback).
-- Inserta 1 CLS estructural (Electricidad 11 Educación Física y Salud Integral)
-- para resolver una de las 191 horas; no aumenta el manifiesto a 192.
-- Solo localhost / schoolmanager_daqf. No usar contra Render.
-- No toca subject_assignments, schedule_entries, matrícula, notas ni asistencia.
--
-- Supuesto provisional: B1/B2 son divisiones del plan de estudios, no grupos ni time_slots.
-- Ver docs/supuestos_carga_horaria_nocturna.md

\set ON_ERROR_STOP on
SET client_encoding TO 'UTF8';

BEGIN;

DO $meduca$
DECLARE
    v_school uuid := '6e42399f-6f17-4585-b92e-fa4fff02cb65';
    v_school_ok integer;
    v_cls_ok integer;
    v_hours_before integer;
    v_hours_other integer;
    v_test_pre integer;
    v_test_auto integer;
    v_sa_count bigint;
    v_sa_hash text;
    v_sa_count_after bigint;
    v_sa_hash_after text;
    v_b1 uuid;
    v_b2 uuid;
    v_row record;
    v_n integer;
    v_cls uuid;
    v_block uuid;
    v_from uuid;
    v_to uuid;
    v_grade uuid;
    v_hours_after integer;
    v_sum numeric;
    v_expected numeric;
    v_col numeric;
    v_resolved integer;
    v_new_cls uuid := 'c15ce11f-5e10-4000-a000-6e42399f6f17';
BEGIN
    SELECT COUNT(*) INTO v_school_ok
    FROM schools
    WHERE id = v_school
      AND name ILIKE '%San Miguelito%';

    IF v_school_ok <> 1 THEN
        RAISE EXCEPTION 'SchoolId % no corresponde a CELO San Miguelito', v_school;
    END IF;

    SELECT COUNT(*) INTO v_cls_ok
    FROM curriculum_load_subjects
    WHERE school_id = v_school;

    IF v_cls_ok <> 205 THEN
        RAISE EXCEPTION 'Se esperaban 205 curriculum_load_subjects de la escuela; hay %', v_cls_ok;
    END IF;

    CREATE TEMP TABLE meduca_grades (
        grade_code text PRIMARY KEY,
        grade_id uuid NOT NULL UNIQUE
    ) ON COMMIT DROP;

    INSERT INTO meduca_grades (grade_code, grade_id)
    VALUES
    ('7',  'eb42cd8b-8d58-4098-8b7d-1494cdc6b312'::uuid),
    ('8',  '7769dfbc-1ce6-4584-b581-0345943b1192'::uuid),
    ('9',  '9811c9ae-8e25-441c-b7f6-41e2e7cabdef'::uuid),
    ('10', 'c3180447-1afd-4ba8-9ebb-4de5bc9eb5c3'::uuid),
    ('11', 'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid),
    ('12', '171563c1-3c14-486f-9d00-a7cf1b67f9a1'::uuid);

    SELECT COUNT(*) INTO v_n
    FROM meduca_grades g
    JOIN grade_levels gl ON gl.id = g.grade_id;

    IF v_n <> 6 THEN
        RAISE EXCEPTION 'Los 6 GradeLevelId explícitos no coinciden con grade_levels';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM curriculum_load_subjects cls
        WHERE cls.school_id = v_school
          AND cls.grade_level_id NOT IN (SELECT grade_id FROM meduca_grades)
    ) THEN
        RAISE EXCEPTION 'Las 205 filas usan un GradeLevelId ajeno a los 6 GUID de Celosan';
    END IF;

    SELECT COUNT(DISTINCT grade_level_id) INTO v_n
    FROM curriculum_load_subjects
    WHERE school_id = v_school;

    IF v_n <> 6 THEN
        RAISE EXCEPTION 'Celosan debe usar exactamente 6 GradeLevelId; hay %', v_n;
    END IF;

    FOR v_row IN SELECT * FROM meduca_grades LOOP
        SELECT COUNT(*) INTO v_n
        FROM curriculum_load_subjects cls
        WHERE cls.school_id = v_school
          AND cls.grade_level_id = v_row.grade_id;

        IF v_n = 0 THEN
            RAISE EXCEPTION 'GradeLevelId % (grado %) no es el usado actualmente por las 205 filas',
                v_row.grade_id, v_row.grade_code;
        END IF;
    END LOOP;

    SELECT id INTO v_b1 FROM curriculum_blocks WHERE code = 'B1';
    SELECT id INTO v_b2 FROM curriculum_blocks WHERE code = 'B2';
    IF v_b1 IS NULL OR v_b2 IS NULL THEN
        RAISE EXCEPTION 'Faltan curriculum_blocks B1/B2';
    END IF;

    SELECT COUNT(*) INTO v_hours_before
    FROM curriculum_load_hours h
    JOIN curriculum_load_subjects cls ON cls.id = h.curriculum_load_subject_id
    WHERE cls.school_id = v_school;

    IF v_hours_before <> 2 THEN
        RAISE EXCEPTION 'Estado inicial inesperado: hay % filas en curriculum_load_hours (se esperaban 2 de prueba)', v_hours_before;
    END IF;

    SELECT COUNT(*) INTO v_hours_other
    FROM curriculum_load_hours h
    JOIN curriculum_load_subjects cls ON cls.id = h.curriculum_load_subject_id
    WHERE cls.school_id = v_school
      AND h.id NOT IN (
          'ae8c2ddc-c69d-40d7-91ba-3031a23e75a5'::uuid,
          'b1165ed7-6075-4ff6-847c-85ad772a92d2'::uuid);

    IF v_hours_other <> 0 THEN
        RAISE EXCEPTION 'Existen horas distintas de las dos pruebas esperadas (%)', v_hours_other;
    END IF;

    SELECT COUNT(*) INTO v_test_pre
    FROM curriculum_load_hours
    WHERE id = 'ae8c2ddc-c69d-40d7-91ba-3031a23e75a5'::uuid
      AND curriculum_load_subject_id = '1fb32f05-b713-4947-be99-f51bcf7d1c52'::uuid
      AND curriculum_block_id = v_b1
      AND hours = 0;

    SELECT COUNT(*) INTO v_test_auto
    FROM curriculum_load_hours
    WHERE id = 'b1165ed7-6075-4ff6-847c-85ad772a92d2'::uuid
      AND curriculum_load_subject_id = 'e4f8c4d0-40be-48fb-834c-ed34c72daaa9'::uuid
      AND curriculum_block_id = v_b1
      AND hours = 2;

    IF v_test_pre <> 1 OR v_test_auto <> 1 THEN
        RAISE EXCEPTION 'Las dos filas de prueba no coinciden (Premedia B1=0 / Autotrónica TI B1=2)';
    END IF;

    SELECT COUNT(*), md5(string_agg(id::text, ',' ORDER BY id))
    INTO v_sa_count, v_sa_hash
    FROM subject_assignments
    WHERE "SchoolId" = v_school;

    CREATE TEMP TABLE meduca_hours (
        hour_id uuid PRIMARY KEY,
        specialty text NOT NULL,
        grade text NOT NULL,
        area text NOT NULL,
        subject text NOT NULL,
        block_code text NOT NULL,
        hours numeric(5,2) NOT NULL
    ) ON COMMIT DROP;

    INSERT INTO meduca_hours (hour_id, specialty, grade, area, subject, block_code, hours)
    VALUES
    -- Premedia
    ('c1910001-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '7', 'HUMANISTICA', 'ESPAÑOL', 'B1', 4.00),
    ('c1910002-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '8', 'HUMANISTICA', 'ESPAÑOL', 'B1', 4.00),
    ('c1910003-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '9', 'HUMANISTICA', 'ESPAÑOL', 'B1', 4.00),
    ('c1910004-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '7', 'HUMANISTICA', 'HISTORIA', 'B1', 2.00),
    ('c1910005-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '8', 'HUMANISTICA', 'HISTORIA', 'B1', 2.00),
    ('c1910006-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '9', 'HUMANISTICA', 'HISTORIA', 'B1', 2.00),
    ('c1910007-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '7', 'HUMANISTICA', 'GEOGRAFÍA', 'B1', 2.00),
    ('c1910008-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '8', 'HUMANISTICA', 'GEOGRAFÍA', 'B1', 2.00),
    ('c1910009-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '9', 'HUMANISTICA', 'GEOGRAFÍA', 'B1', 2.00),
    ('c191000a-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '7', 'HUMANISTICA', 'CÍVICA', 'B2', 3.00),
    ('c191000b-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '8', 'HUMANISTICA', 'CÍVICA', 'B2', 3.00),
    ('c191000c-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '9', 'HUMANISTICA', 'CÍVICA', 'B2', 3.00),
    ('c191000d-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '7', 'HUMANISTICA', 'VAL. ÉTICOS / REL. HUMANAS', 'B2', 2.00),
    ('c191000e-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '8', 'HUMANISTICA', 'VAL. ÉTICOS / REL. HUMANAS', 'B2', 2.00),
    ('c191000f-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '9', 'HUMANISTICA', 'VAL. ÉTICOS / REL. HUMANAS', 'B2', 2.00),
    ('c1910010-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '7', 'HUMANISTICA', 'RELACIONES LABORALES', 'B1', 2.00),
    ('c1910011-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '8', 'HUMANISTICA', 'RELACIONES LABORALES', 'B1', 2.00),
    ('c1910012-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '9', 'HUMANISTICA', 'RELACIONES LABORALES', 'B1', 2.00),
    ('c1910013-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '7', 'HUMANISTICA', 'INGLÉS', 'B2', 2.00),
    ('c1910014-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '8', 'HUMANISTICA', 'INGLÉS', 'B2', 2.00),
    ('c1910015-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '7', 'HUMANISTICA', 'ORIENTACIÓN', 'B1', 2.00),
    ('c1910016-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '8', 'HUMANISTICA', 'EXPRESIONES ARTÍSTICA', 'B1', 2.00),
    ('c1910017-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '9', 'HUMANISTICA', 'MÚSICA', 'B1', 2.00),
    ('c1910018-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '9', 'HUMANISTICA', 'BELLAS ARTES', 'B2', 2.00),
    ('c1910019-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '7', 'CIENTÍFICA', 'MATEMÁTICA', 'B1', 4.00),
    ('c191001a-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '8', 'CIENTÍFICA', 'MATEMÁTICA', 'B1', 4.00),
    ('c191001b-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '9', 'CIENTÍFICA', 'MATEMÁTICA', 'B1', 4.00),
    ('c191001c-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '7', 'CIENTÍFICA', 'CIENCIAS NATURALES', 'B2', 4.00),
    ('c191001d-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '8', 'CIENTÍFICA', 'CIENCIAS NATURALES', 'B2', 4.00),
    ('c191001e-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '9', 'CIENTÍFICA', 'CIENCIAS NATURALES', 'B2', 4.00),
    ('c191001f-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '7', 'CIENTÍFICA', 'SALUD FÍSICA Y MENTAL', 'B2', 2.00),
    ('c1910020-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '8', 'CIENTÍFICA', 'SALUD FÍSICA Y MENTAL', 'B2', 2.00),
    ('c1910021-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '9', 'CIENTÍFICA', 'SALUD FÍSICA Y MENTAL', 'B2', 2.00),
    ('c1910022-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '7', 'TECNOLÓGICA', 'FDC 1', 'B1', 2.00),
    ('c1910023-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '7', 'TECNOLÓGICA', 'MET', 'B2', 2.00),
    ('c1910024-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '8', 'TECNOLÓGICA', 'FDC 2', 'B1', 2.00),
    ('c1910025-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '8', 'TECNOLÓGICA', 'D.L.', 'B2', 2.00),
    ('c1910026-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '9', 'TECNOLÓGICA', 'MCA 1', 'B1', 2.00),
    ('c1910027-5e10-4000-a000-6e42399f6f17'::uuid, 'PRE-MEDIA', '9', 'TECNOLÓGICA', 'MCA 2', 'B2', 2.00),
    -- Informática
    ('c1910028-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '10', 'HUMANISTICA', 'ESPAÑOL (LENGUAJE Y COMUNICACIÓN)', 'B1', 4.00),
    ('c1910029-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '11', 'HUMANISTICA', 'ESPAÑOL (LENGUAJE Y COMUNICACIÓN)', 'B1', 4.00),
    ('c191002a-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '12', 'HUMANISTICA', 'ESPAÑOL (LENGUAJE Y COMUNICACIÓN)', 'B1', 4.00),
    ('c191002b-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '10', 'HUMANISTICA', 'INGLÉS COMERCIAL', 'B2', 4.00),
    ('c191002c-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '11', 'HUMANISTICA', 'INGLÉS COMERCIAL', 'B2', 4.00),
    ('c191002d-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '12', 'HUMANISTICA', 'INGLÉS COMERCIAL', 'B2', 4.00),
    ('c191002e-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '10', 'HUMANISTICA', 'GEOGRAFÍA DE PANAMÁ', 'B1', 2.00),
    ('c191002f-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '11', 'HUMANISTICA', 'HISTORIA DE PANAMÁ', 'B2', 2.00),
    ('c1910030-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '10', 'HUMANISTICA', 'ÉTICA Y VALORES', 'B1', 2.00),
    ('c1910031-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '10', 'HUMANISTICA', 'BELLAS ARTES', 'B2', 2.00),
    ('c1910032-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '12', 'HUMANISTICA', 'HISTORIA DE LAS RELACIONES DE PANAMÁ Y E.U.', 'B1', 2.00),
    ('c1910033-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '12', 'HUMANISTICA', 'CÍVICA III', 'B2', 2.00),
    ('c1910034-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '11', 'HUMANISTICA', 'LÓGICA', 'B1', 1.00),
    ('c1910035-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '12', 'HUMANISTICA', 'FILOSOFÍA', 'B1', 1.00),
    ('c1910036-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '10', 'CIENTÍFICA', 'MATEMÁTICA', 'B1', 4.00),
    ('c1910037-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '11', 'CIENTÍFICA', 'MATEMÁTICA', 'B1', 4.00),
    ('c1910038-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '12', 'CIENTÍFICA', 'MATEMÁTICA', 'B2', 4.00),
    ('c1910039-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '10', 'CIENTÍFICA', 'CIENCIAS INTEGRADAS', 'B2', 4.00),
    ('c191003a-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '11', 'CIENTÍFICA', 'QUÍMICA', 'B2', 2.00),
    ('c191003b-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '12', 'CIENTÍFICA', 'QUÍMICA', 'B1', 2.00),
    ('c191003c-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '11', 'CIENTÍFICA', 'FÍSICA', 'B2', 4.00),
    ('c191003d-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '12', 'CIENTÍFICA', 'FÍSICA', 'B2', 4.00),
    ('c191003e-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '10', 'CIENTÍFICA', 'EDUCACIÓN FÍSICA', 'B2', 2.00),
    ('c191003f-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '11', 'CIENTÍFICA', 'EDUCACIÓN FÍSICA', 'B2', 2.00),
    ('c1910040-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '10', 'TECNOLÓGICA', 'TECNOLOGÍA DE LA INFORMACIÓN', 'B2', 4.00),
    ('c1910041-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '10', 'TECNOLÓGICA', 'CONFIGURACIÓN Y ADMINISTRACIÓN DE SISTEMAS OPERATIVOS', 'B1', 4.00),
    ('c1910042-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '10', 'TECNOLÓGICA', 'CONFIGURACIÓN Y ADMINISTRACIÓN DE SISTEMAS OPERATIVOS', 'B2', 4.00),
    ('c1910043-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '10', 'TECNOLÓGICA', 'DESARROLLO LÓGICO Y PROGRAMACIÓN', 'B1', 4.00),
    ('c1910044-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '11', 'TECNOLÓGICA', 'ARQUITECTURA DE LAS COMPUTADORAS', 'B1', 5.00),
    ('c1910045-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '11', 'TECNOLÓGICA', 'PROGRAMACIÓN', 'B1', 6.00),
    ('c1910046-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '12', 'TECNOLÓGICA', 'MULTIMEDIA Y DESARROLLO WEB', 'B1', 4.00),
    ('c1910047-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '11', 'TECNOLÓGICA', 'REDES DE COMPUTADORAS', 'B2', 3.00),
    ('c1910048-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '12', 'TECNOLÓGICA', 'REDES DE COMPUTADORAS', 'B1', 3.00),
    ('c1910049-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '12', 'TECNOLÓGICA', 'TALLER DE SISTEMAS ROBÓTICOS', 'B1', 4.00),
    ('c191004a-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '11', 'TECNOLÓGICA', 'APLICACIONES CON BASE DE DATOS', 'B2', 3.00),
    ('c191004b-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '12', 'TECNOLÓGICA', 'GESTIÓN EMPRESARIAL', 'B2', 2.00),
    ('c191004c-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN INFORMÁTICA', '12', 'TECNOLÓGICA', 'PRÁCTICA PROFESIONAL', 'B2', 4.00),
    -- Turismo (horas en el grado MEDUCA, tras los movimientos)
    ('c191004d-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '10', 'HUMANISTICA', 'ESPAÑOL (LENGUAJE Y COMUNICACIÓN)', 'B1', 4.00),
    ('c191004e-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '11', 'HUMANISTICA', 'ESPAÑOL (LENGUAJE Y COMUNICACIÓN)', 'B1', 4.00),
    ('c191004f-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '12', 'HUMANISTICA', 'ESPAÑOL (LENGUAJE Y COMUNICACIÓN)', 'B1', 4.00),
    ('c1910050-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '10', 'HUMANISTICA', 'INGLÉS (LENGUAJE Y COMUNICACIÓN)', 'B2', 4.00),
    ('c1910051-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '11', 'HUMANISTICA', 'INGLÉS (LENGUAJE Y COMUNICACIÓN)', 'B2', 4.00),
    ('c1910052-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '12', 'HUMANISTICA', 'INGLÉS (LENGUAJE Y COMUNICACIÓN)', 'B2', 4.00),
    ('c1910053-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '10', 'HUMANISTICA', 'FRANCÉS', 'B1', 3.00),
    ('c1910054-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '11', 'HUMANISTICA', 'FRANCÉS', 'B1', 3.00),
    ('c1910055-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '12', 'HUMANISTICA', 'FRANCÉS', 'B1', 3.00),
    ('c1910056-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '11', 'HUMANISTICA', 'GEOGRAFÍA DE PANAMÁ', 'B1', 2.00),
    ('c1910057-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '11', 'HUMANISTICA', 'GEOGRAFÍA TURÍSTICA DE PANAMÁ', 'B2', 2.00),
    ('c1910058-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '12', 'HUMANISTICA', 'GEOGRAFÍA TURÍSTICA DEL MUNDO', 'B2', 2.00),
    ('c1910059-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '10', 'HUMANISTICA', 'HISTORIA DE PANAMÁ', 'B2', 2.00),
    ('c191005a-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '12', 'HUMANISTICA', 'HISTORIA DE LAS RELACIONES DE PANAMÁ Y E.U.', 'B1', 2.00),
    ('c191005b-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '12', 'HUMANISTICA', 'CÍVICA', 'B1', 2.00),
    ('c191005c-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '10', 'HUMANISTICA', 'ÉTICA MORAL, VALORES Y RELACIONES HUMANAS', 'B2', 2.00),
    ('c191005d-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '11', 'HUMANISTICA', 'BELLAS ARTES', 'B1', 2.00),
    ('c191005e-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '10', 'CIENTÍFICA', 'MATEMÁTICA', 'B1', 4.00),
    ('c191005f-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '11', 'CIENTÍFICA', 'MATEMÁTICA', 'B1', 4.00),
    ('c1910060-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '12', 'CIENTÍFICA', 'MATEMÁTICA', 'B1', 4.00),
    ('c1910061-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '10', 'CIENTÍFICA', 'EDUCACIÓN FÍSICA Y SALUD INTEGRAL', 'B2', 2.00),
    ('c1910062-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '11', 'CIENTÍFICA', 'EDUCACIÓN FÍSICA Y SALUD INTEGRAL', 'B2', 2.00),
    ('c1910063-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '10', 'TECNOLÓGICA', 'TECNOLOGÍA DE LA INFORMACIÓN', 'B2', 4.00),
    ('c1910064-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '11', 'TECNOLÓGICA', 'TECNOLOGÍA COMERCIAL', 'B1', 2.00),
    ('c1910065-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '12', 'TECNOLÓGICA', 'TECNOLOGÍA COMERCIAL', 'B1', 2.00),
    ('c1910066-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '10', 'TECNOLÓGICA', 'CONTABILIDAD', 'B2', 3.00),
    ('c1910067-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '11', 'TECNOLÓGICA', 'CONTABILIDAD', 'B2', 3.00),
    ('c1910068-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '12', 'TECNOLÓGICA', 'CONTABILIDAD', 'B2', 2.00),
    ('c1910069-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '10', 'TECNOLÓGICA', 'TURISMO (INTRODUCCIÓN AL TURISMO Y CULTURA TURÍSTICA)', 'B1', 3.00),
    ('c191006a-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '11', 'TECNOLÓGICA', 'TURISMO (INTRODUCCIÓN AL TURISMO Y CULTURA TURÍSTICA)', 'B1', 3.00),
    ('c191006b-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '12', 'TECNOLÓGICA', 'TURISMO (INTRODUCCIÓN AL TURISMO Y CULTURA TURÍSTICA)', 'B1', 3.00),
    ('c191006c-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '11', 'TECNOLÓGICA', 'TURISMO SOSTENIBLE', 'B2', 3.00),
    ('c191006d-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '12', 'TECNOLÓGICA', 'TURISMO SOSTENIBLE', 'B2', 3.00),
    ('c191006e-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '10', 'TECNOLÓGICA', 'GESTIÓN EMPRESARIAL TURÍSTICA', 'B1', 3.00),
    ('c191006f-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '10', 'TECNOLÓGICA', 'GESTIÓN EMPRESARIAL TURÍSTICA', 'B2', 3.00),
    ('c1910070-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '11', 'TECNOLÓGICA', 'SERVICIOS TURÍSTICOS I Y II', 'B2', 3.00),
    ('c1910071-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '12', 'TECNOLÓGICA', 'SERVICIOS TURÍSTICOS I Y II', 'B2', 3.00),
    ('c1910072-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '11', 'TECNOLÓGICA', 'MERCADOTECNIA Y PUBLICIDAD', 'B2', 3.00),
    ('c1910073-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '10', 'TECNOLÓGICA', 'OFIMÁTICA', 'B1', 3.00),
    ('c1910074-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '12', 'TECNOLÓGICA', 'ELABORACIÓN DE PROYECTOS TURÍSTICOS', 'B2', 3.00),
    ('c1910075-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN TURISMO', '12', 'TECNOLÓGICA', 'PRÁCTICA PROFESIONAL', 'B2', 3.00),
    -- Autotrónica
    ('c1910076-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '10', 'HUMANISTICA', 'ESPAÑOL (LENGUAJE Y COMUNICACIÓN)', 'B1', 4.00),
    ('c1910077-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '11', 'HUMANISTICA', 'ESPAÑOL (LENGUAJE Y COMUNICACIÓN)', 'B1', 4.00),
    ('c1910078-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '12', 'HUMANISTICA', 'ESPAÑOL (LENGUAJE Y COMUNICACIÓN)', 'B1', 4.00),
    ('c1910079-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '10', 'HUMANISTICA', 'INGLÉS (LENGUAJE Y COMUNICACIÓN)', 'B2', 4.00),
    ('c191007a-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '11', 'HUMANISTICA', 'INGLÉS (LENGUAJE Y COMUNICACIÓN)', 'B2', 4.00),
    ('c191007b-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '12', 'HUMANISTICA', 'INGLÉS (LENGUAJE Y COMUNICACIÓN)', 'B2', 4.00),
    ('c191007c-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '10', 'HUMANISTICA', 'GEOGRAFÍA DE PANAMÁ', 'B1', 2.00),
    ('c191007d-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '10', 'HUMANISTICA', 'ÉTICA, MORAL, VALORES Y RELACIONES HUMANAS', 'B1', 2.00),
    ('c191007e-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '10', 'HUMANISTICA', 'BELLAS ARTES', 'B2', 2.00),
    ('c191007f-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '11', 'HUMANISTICA', 'HISTORIA DE PANAMÁ', 'B2', 2.00),
    ('c1910080-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '12', 'HUMANISTICA', 'HISTORIA DE LAS RELACIONES DE PANAMÁ Y E.U.', 'B1', 2.00),
    ('c1910081-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '12', 'HUMANISTICA', 'CÍVICA', 'B2', 2.00),
    ('c1910082-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '11', 'HUMANISTICA', 'LÓGICA', 'B1', 1.00),
    ('c1910083-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '12', 'HUMANISTICA', 'FILOSOFÍA', 'B1', 1.00),
    ('c1910084-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '10', 'CIENTÍFICA', 'MATEMÁTICA', 'B1', 4.00),
    ('c1910085-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '11', 'CIENTÍFICA', 'MATEMÁTICA', 'B1', 4.00),
    ('c1910086-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '12', 'CIENTÍFICA', 'MATEMÁTICA', 'B2', 4.00),
    ('c1910087-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '10', 'CIENTÍFICA', 'EDUCACIÓN FÍSICA Y SALUD INTEGRAL', 'B2', 2.00),
    ('c1910088-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '10', 'CIENTÍFICA', 'CIENCIAS NATURALES', 'B2', 4.00),
    ('c1910089-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '11', 'CIENTÍFICA', 'QUÍMICA', 'B2', 2.00),
    ('c191008a-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '12', 'CIENTÍFICA', 'QUÍMICA', 'B1', 2.00),
    ('c191008b-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '11', 'CIENTÍFICA', 'FÍSICA', 'B2', 4.00),
    ('c191008c-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '12', 'CIENTÍFICA', 'FÍSICA', 'B2', 4.00),
    ('c191008d-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '10', 'TECNOLÓGICA', 'TECNOLOGÍA DE LA INFORMACIÓN', 'B2', 4.00),
    ('c191008e-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '10', 'TECNOLÓGICA', 'DIBUJO I RELACIONADO', 'B2', 2.00),
    ('c191008f-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '10', 'TECNOLÓGICA', 'TALLER I (FUNDAMENTO DE TECNOLOGÍA INDUSTRIAL)', 'B1', 5.00),
    ('c1910090-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '12', 'TECNOLÓGICA', 'TALLER II (DIAGNÓSTICO AUTOMOTRIZ AUTOMATIZADO)', 'B1', 5.00),
    ('c1910091-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '10', 'TECNOLÓGICA', 'TALLER III (TECNOLOGÍA Y TALLER APLICADO)', 'B2', 2.00),
    ('c1910092-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '11', 'TECNOLÓGICA', 'TALLER III (TECNOLOGÍA Y TALLER APLICADO)', 'B2', 6.00),
    ('c1910093-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '11', 'TECNOLÓGICA', 'TALLER IV (ELECTRICIDAD Y ELECTRÓNICA AUTOMOTRIZ)', 'B1', 6.00),
    ('c1910094-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '12', 'TECNOLÓGICA', 'TALLER IV (ELECTRICIDAD Y ELECTRÓNICA AUTOMOTRIZ)', 'B1', 6.00),
    ('c1910095-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '10', 'TECNOLÓGICA', 'TALLER V (MANTENIMIENTO AUTOMOTRIZ)', 'B1', 3.00),
    ('c1910096-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '11', 'TECNOLÓGICA', 'TALLER V (MANTENIMIENTO AUTOMOTRIZ)', 'B1', 5.00),
    ('c1910097-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '11', 'TECNOLÓGICA', 'GESTIÓN EMPRESARIAL', 'B2', 2.00),
    ('c1910098-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN AUTOTRÓNICA', '12', 'TECNOLÓGICA', 'PRÁCTICA PROFESIONAL', 'B2', 5.00),
    -- Electricidad
    ('c1910099-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '10', 'HUMANISTICA', 'ESPAÑOL (LENGUAJE Y COMUNICACIÓN)', 'B1', 4.00),
    ('c191009a-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '11', 'HUMANISTICA', 'ESPAÑOL (LENGUAJE Y COMUNICACIÓN)', 'B1', 4.00),
    ('c191009b-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '12', 'HUMANISTICA', 'ESPAÑOL (LENGUAJE Y COMUNICACIÓN)', 'B1', 4.00),
    ('c191009c-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '10', 'HUMANISTICA', 'INGLÉS (LENGUAJE Y COMUNICACIÓN)', 'B2', 4.00),
    ('c191009d-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '11', 'HUMANISTICA', 'INGLÉS (LENGUAJE Y COMUNICACIÓN)', 'B2', 4.00),
    ('c191009e-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '12', 'HUMANISTICA', 'INGLÉS (LENGUAJE Y COMUNICACIÓN)', 'B2', 4.00),
    ('c191009f-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '10', 'HUMANISTICA', 'GEOGRAFÍA DE PANAMÁ', 'B1', 2.00),
    ('c19100a0-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '10', 'HUMANISTICA', 'ÉTICA, MORAL, VALORES Y RELACIONES HUMANAS', 'B1', 2.00),
    ('c19100a1-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '10', 'HUMANISTICA', 'BELLAS ARTES', 'B2', 2.00),
    ('c19100a2-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '11', 'HUMANISTICA', 'HISTORIA DE PANAMÁ', 'B2', 2.00),
    ('c19100a3-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '12', 'HUMANISTICA', 'HISTORIA DE LAS RELACIONES DE PANAMÁ Y E.U.', 'B1', 2.00),
    ('c19100a4-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '12', 'HUMANISTICA', 'CÍVICA', 'B2', 2.00),
    ('c19100a5-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '11', 'HUMANISTICA', 'LÓGICA / FILOSOFÍA', 'B1', 1.00),
    ('c19100a6-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '12', 'HUMANISTICA', 'LÓGICA / FILOSOFÍA', 'B1', 1.00),
    ('c19100a7-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '10', 'CIENTÍFICA', 'MATEMÁTICA', 'B1', 4.00),
    ('c19100a8-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '11', 'CIENTÍFICA', 'MATEMÁTICA', 'B1', 4.00),
    ('c19100a9-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '12', 'CIENTÍFICA', 'MATEMÁTICA', 'B2', 4.00),
    ('c19100aa-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '10', 'CIENTÍFICA', 'EDUCACIÓN FÍSICA Y SALUD INTEGRAL', 'B2', 2.00),
    ('c19100ab-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '11', 'CIENTÍFICA', 'EDUCACIÓN FÍSICA Y SALUD INTEGRAL', 'B2', 2.00),
    ('c19100ac-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '10', 'CIENTÍFICA', 'CIENCIAS NATURALES', 'B2', 4.00),
    ('c19100ad-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '11', 'CIENTÍFICA', 'QUÍMICA', 'B2', 2.00),
    ('c19100ae-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '12', 'CIENTÍFICA', 'QUÍMICA', 'B1', 2.00),
    ('c19100af-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '11', 'CIENTÍFICA', 'FÍSICA', 'B2', 4.00),
    ('c19100b0-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '12', 'CIENTÍFICA', 'FÍSICA', 'B2', 4.00),
    ('c19100b1-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '10', 'TECNOLÓGICA', 'TECNOLOGÍA DE LA INFORMACIÓN', 'B2', 4.00),
    ('c19100b2-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '10', 'TECNOLÓGICA', 'DIBUJO I (LINEAL)', 'B2', 2.00),
    ('c19100b3-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '10', 'TECNOLÓGICA', 'TALLER I (DE EQUIPO Y MEDICIONES)', 'B1', 4.00),
    ('c19100b4-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '10', 'TECNOLÓGICA', 'SEGURIDAD INDUSTRIAL', 'B1', 4.00),
    ('c19100b5-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '11', 'TECNOLÓGICA', 'DIBUJO II (APLICACIÓN Y ASISTENCIA POR COMPUTADORA)', 'B1', 3.00),
    ('c19100b6-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '10', 'TECNOLÓGICA', 'TALLER II (INSTALACIÓN RESIDENCIAL Y COMERCIAL)', 'B2', 2.00),
    ('c19100b7-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '12', 'TECNOLÓGICA', 'TALLER II (INSTALACIÓN RESIDENCIAL Y COMERCIAL)', 'B1', 4.00),
    ('c19100b8-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '11', 'TECNOLÓGICA', 'TALLER III (ELECTRÓNICA)', 'B1', 3.00),
    ('c19100b9-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '11', 'TECNOLÓGICA', 'TALLER IV (ANÁLISIS DE CIRCUITO)', 'B2', 3.00),
    ('c19100ba-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '12', 'TECNOLÓGICA', 'TALLER IV (ANÁLISIS DE CIRCUITO)', 'B1', 3.00),
    ('c19100bb-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '11', 'TECNOLÓGICA', 'TALLER V (MÁQUINAS ELÉCTRICAS)', 'B1', 5.00),
    ('c19100bc-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '12', 'TECNOLÓGICA', 'TALLER V (MÁQUINAS ELÉCTRICAS)', 'B1', 4.00),
    ('c19100bd-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '12', 'TECNOLÓGICA', 'TALLER DE PRODUCCIÓN Y DISTRIBUCIÓN', 'B2', 4.00),
    ('c19100be-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '11', 'TECNOLÓGICA', 'TALLER DE PROYECTOS Y PRESUPUESTOS', 'B2', 3.00),
    ('c19100bf-5e10-4000-a000-6e42399f6f17'::uuid, 'BACHILLER EN ELECTRICIDAD', '12', 'TECNOLÓGICA', 'GESTIÓN EMPRESARIAL', 'B2', 2.00);

    IF (SELECT COUNT(*) FROM meduca_hours) <> 191 THEN
        RAISE EXCEPTION 'La matriz planificada debe tener 191 celdas; hay %', (SELECT COUNT(*) FROM meduca_hours);
    END IF;

    IF (SELECT COUNT(DISTINCT hour_id) FROM meduca_hours) <> 191 THEN
        RAISE EXCEPTION 'Los UUID del manifiesto no son únicos';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM meduca_hours
        GROUP BY specialty, grade, area, subject, block_code
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION 'Hay destinos CLS + B1/B2 duplicados en el manifiesto';
    END IF;

    IF EXISTS (
        SELECT 1 FROM curriculum_load_hours
        WHERE id IN (SELECT hour_id FROM meduca_hours)
    ) THEN
        RAISE EXCEPTION 'Uno o más de los 191 UUID del manifiesto ya existen en curriculum_load_hours';
    END IF;

    IF EXISTS (
        SELECT 1 FROM curriculum_load_subjects WHERE id = v_new_cls
    ) THEN
        RAISE EXCEPTION 'La CLS estructural de Electricidad 11 (%) ya existe', v_new_cls;
    END IF;

    SELECT COUNT(*) INTO v_n
    FROM curriculum_load_subjects
    WHERE school_id = v_school
      AND specialty_id = 'a89c4c24-5e5e-4d27-90b3-08421ecfb3bb'::uuid
      AND grade_level_id = 'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid
      AND area_id = '00630e6b-29d6-4122-98a1-6f3d992b50d9'::uuid
      AND subject_id = '9015b4ae-5da3-4c43-8cad-ea03db48c854'::uuid;

    IF v_n <> 0 THEN
        RAISE EXCEPTION 'La combinación Electricidad 11 / Científica / Educación Física y Salud Integral ya existe';
    END IF;

    SELECT COUNT(*) INTO v_n
    FROM curriculum_load_subjects cls
    JOIN subjects sub ON sub.id = cls.subject_id
    WHERE cls.id = 'ca6824e0-c3c3-4845-ab5b-cb8c2f0c7310'::uuid
      AND cls.school_id = v_school
      AND cls.specialty_id = 'a89c4c24-5e5e-4d27-90b3-08421ecfb3bb'::uuid
      AND cls.grade_level_id = 'c3180447-1afd-4ba8-9ebb-4de5bc9eb5c3'::uuid
      AND cls.area_id = '00630e6b-29d6-4122-98a1-6f3d992b50d9'::uuid
      AND cls.subject_id = '9015b4ae-5da3-4c43-8cad-ea03db48c854'::uuid
      AND sub.name = 'EDUCACIÓN FÍSICA Y SALUD INTEGRAL';

    IF v_n <> 1 THEN
        RAISE EXCEPTION 'La CLS de Electricidad 10 Educación Física no está en su grado original';
    END IF;

    INSERT INTO curriculum_load_subjects (
        id,
        school_id,
        specialty_id,
        grade_level_id,
        area_id,
        subject_id,
        is_active,
        created_at
    ) VALUES (
        v_new_cls,
        v_school,
        'a89c4c24-5e5e-4d27-90b3-08421ecfb3bb'::uuid,
        'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid,
        '00630e6b-29d6-4122-98a1-6f3d992b50d9'::uuid,
        '9015b4ae-5da3-4c43-8cad-ea03db48c854'::uuid,
        true,
        CURRENT_TIMESTAMP
    );

    SELECT COUNT(*) INTO v_cls_ok
    FROM curriculum_load_subjects
    WHERE school_id = v_school;

    IF v_cls_ok <> 206 THEN
        RAISE EXCEPTION 'Tras insertar la CLS de Electricidad 11 deben existir 206; hay %', v_cls_ok;
    END IF;

    CREATE TEMP TABLE meduca_moves (
        cls_id uuid NOT NULL,
        specialty text NOT NULL,
        subject text NOT NULL,
        from_grade text NOT NULL,
        to_grade text NOT NULL,
        from_grade_id uuid NOT NULL,
        to_grade_id uuid NOT NULL
    ) ON COMMIT DROP;

    INSERT INTO meduca_moves (cls_id, specialty, subject, from_grade, to_grade, from_grade_id, to_grade_id)
    VALUES
    ('6bf0b5e5-145e-477a-95f9-f9dc92f7f95a'::uuid, 'BACHILLER EN TURISMO', 'HISTORIA DE PANAMÁ', '11', '10', 'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid, 'c3180447-1afd-4ba8-9ebb-4de5bc9eb5c3'::uuid),
    ('7e71c8de-64f7-44dd-8397-fa62f7a57007'::uuid, 'BACHILLER EN TURISMO', 'GEOGRAFÍA DE PANAMÁ', '10', '11', 'c3180447-1afd-4ba8-9ebb-4de5bc9eb5c3'::uuid, 'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid),
    ('0f09b724-0f76-450d-8818-97c301e63ffc'::uuid, 'BACHILLER EN TURISMO', 'GEOGRAFÍA TURÍSTICA DE PANAMÁ', '12', '11', '171563c1-3c14-486f-9d00-a7cf1b67f9a1'::uuid, 'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid),
    ('d7f63ab0-9348-4c11-8a41-af5e880e4e9f'::uuid, 'BACHILLER EN TURISMO', 'BELLAS ARTES', '10', '11', 'c3180447-1afd-4ba8-9ebb-4de5bc9eb5c3'::uuid, 'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid),
    ('2372a8f3-3f32-4299-87b6-bc578714d9d9'::uuid, 'BACHILLER EN TURISMO', 'GEOGRAFÍA TURÍSTICA DEL MUNDO', '11', '12', 'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid, '171563c1-3c14-486f-9d00-a7cf1b67f9a1'::uuid),
    ('1828befa-566f-48a8-804e-e8fb4d60076f'::uuid, 'BACHILLER EN AUTOTRÓNICA', 'TALLER III (TECNOLOGÍA Y TALLER APLICADO)', '12', '11', '171563c1-3c14-486f-9d00-a7cf1b67f9a1'::uuid, 'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid),
    ('2a97b7db-fed7-4643-b33a-079bc988ed74'::uuid, 'BACHILLER EN AUTOTRÓNICA', 'TALLER V (MANTENIMIENTO AUTOMOTRIZ)', '12', '11', '171563c1-3c14-486f-9d00-a7cf1b67f9a1'::uuid, 'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid);

    IF (SELECT COUNT(*) FROM meduca_moves) <> 7 THEN
        RAISE EXCEPTION 'Deben existir exactamente 7 movimientos de grado';
    END IF;

    FOR v_row IN SELECT * FROM meduca_moves LOOP
        v_from := v_row.from_grade_id;
        v_to := v_row.to_grade_id;

        IF NOT EXISTS (
            SELECT 1 FROM meduca_grades
            WHERE grade_code = v_row.from_grade AND grade_id = v_from
        ) OR NOT EXISTS (
            SELECT 1 FROM meduca_grades
            WHERE grade_code = v_row.to_grade AND grade_id = v_to
        ) THEN
            RAISE EXCEPTION 'GradeLevelId de movimiento no coincide con el mapa de Celosan: % %',
                v_row.from_grade, v_row.to_grade;
        END IF;

        SELECT COUNT(*) INTO v_n
        FROM curriculum_load_subjects cls
        JOIN specialties sp ON sp.id = cls.specialty_id
        JOIN subjects sub ON sub.id = cls.subject_id
        WHERE cls.id = v_row.cls_id
          AND cls.school_id = v_school
          AND sp.name = v_row.specialty
          AND sub.name = v_row.subject
          AND cls.grade_level_id = v_from;

        IF v_n <> 1 THEN
            RAISE EXCEPTION 'Movimiento GUID inválido o ya no está en su grado original: % % grado % (coincidencias %)',
                v_row.specialty, v_row.subject, v_row.from_grade, v_n;
        END IF;

        SELECT COUNT(*) INTO v_n
        FROM curriculum_load_hours
        WHERE curriculum_load_subject_id = v_row.cls_id
          AND id NOT IN (
              'ae8c2ddc-c69d-40d7-91ba-3031a23e75a5'::uuid,
              'b1165ed7-6075-4ff6-847c-85ad772a92d2'::uuid);

        IF v_n <> 0 THEN
            RAISE EXCEPTION 'La fila % ya tiene horas distintas de las de prueba', v_row.cls_id;
        END IF;

        SELECT COUNT(*) INTO v_n
        FROM curriculum_load_subjects dest
        JOIN curriculum_load_subjects src ON src.id = v_row.cls_id
        WHERE dest.school_id = src.school_id
          AND dest.specialty_id = src.specialty_id
          AND dest.area_id = src.area_id
          AND dest.subject_id = src.subject_id
          AND dest.grade_level_id = v_to
          AND dest.id <> src.id;

        IF v_n <> 0 THEN
            RAISE EXCEPTION 'Destino ocupado al mover % a grado %', v_row.subject, v_row.to_grade;
        END IF;

        UPDATE curriculum_load_subjects
        SET grade_level_id = v_to,
            updated_at = CURRENT_TIMESTAMP
        WHERE id = v_row.cls_id;
    END LOOP;

    CREATE TEMP TABLE meduca_resolved (
        hour_id uuid PRIMARY KEY,
        cls_id uuid NOT NULL,
        block_id uuid NOT NULL,
        hours numeric(5,2) NOT NULL
    ) ON COMMIT DROP;

    FOR v_row IN SELECT * FROM meduca_hours LOOP
        SELECT g.grade_id INTO v_grade
        FROM meduca_grades g
        WHERE g.grade_code = v_row.grade;

        IF v_grade IS NULL THEN
            RAISE EXCEPTION 'Grado del manifiesto no está en los 6 GUID: %', v_row.grade;
        END IF;

        SELECT COUNT(*) INTO v_n
        FROM curriculum_load_subjects cls
        JOIN specialties sp ON sp.id = cls.specialty_id
        JOIN area a ON a.id = cls.area_id
        JOIN subjects sub ON sub.id = cls.subject_id
        WHERE cls.school_id = v_school
          AND cls.grade_level_id = v_grade
          AND sp.name = v_row.specialty
          AND a.name = v_row.area
          AND sub.name = v_row.subject;

        IF v_n = 0 THEN
            RAISE EXCEPTION 'Sin celda curricular: % / % / % / %',
                v_row.specialty, v_row.grade, v_row.area, v_row.subject;
        END IF;
        IF v_n > 1 THEN
            RAISE EXCEPTION 'Coincidencia ambigua (%): % / % / % / %',
                v_n, v_row.specialty, v_row.grade, v_row.area, v_row.subject;
        END IF;

        SELECT cls.id INTO v_cls
        FROM curriculum_load_subjects cls
        JOIN specialties sp ON sp.id = cls.specialty_id
        JOIN area a ON a.id = cls.area_id
        JOIN subjects sub ON sub.id = cls.subject_id
        WHERE cls.school_id = v_school
          AND cls.grade_level_id = v_grade
          AND sp.name = v_row.specialty
          AND a.name = v_row.area
          AND sub.name = v_row.subject;

        v_block := CASE v_row.block_code WHEN 'B1' THEN v_b1 ELSE v_b2 END;

        INSERT INTO meduca_resolved (hour_id, cls_id, block_id, hours)
        VALUES (v_row.hour_id, v_cls, v_block, v_row.hours);
    END LOOP;

    SELECT COUNT(*) INTO v_resolved FROM meduca_resolved;
    IF v_resolved <> 191 THEN
        RAISE EXCEPTION 'No se resolvieron exactamente 191 destinos; hay %', v_resolved;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM meduca_resolved
        GROUP BY cls_id, block_id
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION 'Hay repetición de CurriculumLoadSubjectId + CurriculumBlockId';
    END IF;

    SELECT cls_id INTO v_cls
    FROM meduca_resolved
    WHERE hour_id = 'c19100ab-5e10-4000-a000-6e42399f6f17'::uuid;

    IF v_cls IS DISTINCT FROM v_new_cls THEN
        RAISE EXCEPTION 'c19100ab no resolvió a la CLS estructural de Electricidad 11';
    END IF;

    SELECT cls_id INTO v_cls
    FROM meduca_resolved
    WHERE hour_id = 'c19100aa-5e10-4000-a000-6e42399f6f17'::uuid;

    IF v_cls IS DISTINCT FROM 'ca6824e0-c3c3-4845-ab5b-cb8c2f0c7310'::uuid THEN
        RAISE EXCEPTION 'c19100aa no resolvió a la CLS original de Electricidad 10';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM curriculum_load_hours h
        JOIN meduca_resolved r
          ON r.cls_id = h.curriculum_load_subject_id
         AND r.block_id = h.curriculum_block_id
        WHERE h.id NOT IN (
            'ae8c2ddc-c69d-40d7-91ba-3031a23e75a5'::uuid,
            'b1165ed7-6075-4ff6-847c-85ad772a92d2'::uuid)
    ) THEN
        RAISE EXCEPTION 'Alguna combinación CLS + bloque ya tiene una fila no contemplada';
    END IF;

    DELETE FROM curriculum_load_hours
    WHERE id IN (
        'ae8c2ddc-c69d-40d7-91ba-3031a23e75a5'::uuid,
        'b1165ed7-6075-4ff6-847c-85ad772a92d2'::uuid);

    FOR v_row IN SELECT * FROM meduca_resolved LOOP
        v_cls := v_row.cls_id;
        v_block := v_row.block_id;

        IF EXISTS (
            SELECT 1 FROM curriculum_load_hours
            WHERE curriculum_load_subject_id = v_cls
              AND curriculum_block_id = v_block
        ) THEN
            RAISE EXCEPTION 'Ya existe hora no contemplada en CLS % bloque %', v_cls, v_block;
        END IF;

        INSERT INTO curriculum_load_hours (
            id,
            curriculum_load_subject_id,
            curriculum_block_id,
            hours,
            created_at
        )
        VALUES (
            v_row.hour_id,
            v_cls,
            v_block,
            v_row.hours,
            CURRENT_TIMESTAMP
        );
    END LOOP;

    SELECT COUNT(*) INTO v_hours_after
    FROM curriculum_load_hours h
    JOIN curriculum_load_subjects cls ON cls.id = h.curriculum_load_subject_id
    WHERE cls.school_id = v_school;

    IF v_hours_after <> 191 THEN
        RAISE EXCEPTION 'Tras la carga debe haber 191 horas; hay %', v_hours_after;
    END IF;

    IF EXISTS (
        SELECT 1 FROM curriculum_load_hours h
        JOIN curriculum_load_subjects cls ON cls.id = h.curriculum_load_subject_id
        WHERE cls.school_id = v_school AND h.hours = 0
    ) THEN
        RAISE EXCEPTION 'No se permiten horas en 0';
    END IF;

    IF EXISTS (
        SELECT 1 FROM curriculum_load_hours
        WHERE id IN (
            'ae8c2ddc-c69d-40d7-91ba-3031a23e75a5'::uuid,
            'b1165ed7-6075-4ff6-847c-85ad772a92d2'::uuid)
    ) THEN
        RAISE EXCEPTION 'Las filas de prueba no debían permanecer tras la carga';
    END IF;

    FOR v_row IN
        SELECT sp.name AS specialty, SUM(h.hours) AS total
        FROM curriculum_load_hours h
        JOIN curriculum_load_subjects cls ON cls.id = h.curriculum_load_subject_id
        JOIN specialties sp ON sp.id = cls.specialty_id
        WHERE cls.school_id = v_school
        GROUP BY sp.name
    LOOP
        v_expected := CASE v_row.specialty
            WHEN 'PRE-MEDIA' THEN 99
            WHEN 'BACHILLER EN INFORMÁTICA' THEN 120
            WHEN 'BACHILLER EN TURISMO' THEN 120
            WHEN 'BACHILLER EN AUTOTRÓNICA' THEN 119
            WHEN 'BACHILLER EN ELECTRICIDAD' THEN 120
            ELSE -1
        END;
        IF v_expected < 0 OR v_row.total <> v_expected THEN
            RAISE EXCEPTION 'Total de % = %; se esperaba %', v_row.specialty, v_row.total, v_expected;
        END IF;
    END LOOP;

    SELECT SUM(h.hours) INTO v_sum
    FROM curriculum_load_hours h
    JOIN curriculum_load_subjects cls ON cls.id = h.curriculum_load_subject_id
    WHERE cls.school_id = v_school;

    IF v_sum <> 578 THEN
        RAISE EXCEPTION 'Total general = %; se esperaba 578', v_sum;
    END IF;

    FOR v_row IN
        SELECT sp.name AS specialty, g.grade_code AS grade, cb.code AS block, SUM(h.hours) AS total
        FROM curriculum_load_hours h
        JOIN curriculum_load_subjects cls ON cls.id = h.curriculum_load_subject_id
        JOIN specialties sp ON sp.id = cls.specialty_id
        JOIN meduca_grades g ON g.grade_id = cls.grade_level_id
        JOIN curriculum_blocks cb ON cb.id = h.curriculum_block_id
        WHERE cls.school_id = v_school
        GROUP BY sp.name, g.grade_code, cb.code
    LOOP
        v_col := v_row.total;
        IF v_row.specialty = 'PRE-MEDIA' THEN
            IF v_row.block = 'B1' AND v_col <> 18 THEN
                RAISE EXCEPTION 'Premedia % % = %', v_row.grade, v_row.block, v_col;
            END IF;
            IF v_row.block = 'B2' AND v_col <> 15 THEN
                RAISE EXCEPTION 'Premedia % % = %', v_row.grade, v_row.block, v_col;
            END IF;
        ELSIF v_row.specialty = 'BACHILLER EN AUTOTRÓNICA' AND v_row.grade = '12' AND v_row.block = 'B2' THEN
            IF v_col <> 19 THEN
                RAISE EXCEPTION 'Autotrónica 12 B2 = %', v_col;
            END IF;
        ELSIF v_col <> 20 THEN
            RAISE EXCEPTION '% % % = % (se esperaba 20)', v_row.specialty, v_row.grade, v_row.block, v_col;
        END IF;
    END LOOP;

    SELECT COUNT(*) INTO v_cls_ok
    FROM curriculum_load_subjects
    WHERE school_id = v_school;

    IF v_cls_ok <> 206 THEN
        RAISE EXCEPTION 'Tras la carga deben existir 206 CLS; hay %', v_cls_ok;
    END IF;

    SELECT COUNT(*) INTO v_n
    FROM curriculum_load_subjects
    WHERE id = 'ca6824e0-c3c3-4845-ab5b-cb8c2f0c7310'::uuid
      AND grade_level_id = 'c3180447-1afd-4ba8-9ebb-4de5bc9eb5c3'::uuid
      AND subject_id = '9015b4ae-5da3-4c43-8cad-ea03db48c854'::uuid;

    IF v_n <> 1 THEN
        RAISE EXCEPTION 'Se modificó la CLS de Electricidad 10';
    END IF;

    SELECT COUNT(*) INTO v_n
    FROM curriculum_load_hours
    WHERE curriculum_load_subject_id = v_new_cls
      AND id = 'c19100ab-5e10-4000-a000-6e42399f6f17'::uuid;

    IF v_n <> 1 THEN
        RAISE EXCEPTION 'La CLS estructural de Electricidad 11 no quedó con la hora B2 del manifiesto';
    END IF;

    SELECT COUNT(*) INTO v_n
    FROM curriculum_load_hours
    WHERE curriculum_load_subject_id = v_new_cls
      AND id <> 'c19100ab-5e10-4000-a000-6e42399f6f17'::uuid;

    IF v_n <> 0 THEN
        RAISE EXCEPTION 'La CLS estructural de Electricidad 11 no debe tener horas distintas de B2=2';
    END IF;

    SELECT COUNT(*), md5(string_agg(id::text, ',' ORDER BY id))
    INTO v_sa_count_after, v_sa_hash_after
    FROM subject_assignments
    WHERE "SchoolId" = v_school;

    IF v_sa_count_after IS DISTINCT FROM v_sa_count OR v_sa_hash_after IS DISTINCT FROM v_sa_hash THEN
        RAISE EXCEPTION 'subject_assignments cambió; se aborta la carga';
    END IF;
END
$meduca$;

COMMIT;
