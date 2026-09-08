-- Revierte apply_meduca_curriculum_hours.sql.
-- Elimina únicamente los 191 UUID explícitos del manifiesto.
-- Elimina después únicamente la CLS estructural de Electricidad 11
-- (c15ce11f-5e10-4000-a000-6e42399f6f17) si no fue modificada.
-- Restaura las 2 filas de prueba y los 7 movimientos de grado.
-- No toca subject_assignments, Schedule, matrícula, notas ni asistencia.
-- Si una fila del manifiesto o la CLS estructural fue editada, falta o no coincide, aborta sin borrar.

\set ON_ERROR_STOP on
SET client_encoding TO 'UTF8';

BEGIN;

DO $rb$
DECLARE
    v_school uuid := '6e42399f-6f17-4585-b92e-fa4fff02cb65';
    v_school_ok integer;
    v_cls_ok integer;
    v_b1 uuid;
    v_b2 uuid;
    v_from uuid;
    v_to uuid;
    v_row record;
    v_n integer;
    v_hours integer;
    v_deleted integer;
    v_cls uuid;
    v_block uuid;
    v_grade uuid;
    v_mismatch integer;
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

    IF v_cls_ok <> 206 THEN
        RAISE EXCEPTION 'Se esperaban 206 curriculum_load_subjects tras el apply; hay %', v_cls_ok;
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
        RAISE EXCEPTION 'Las 206 filas usan un GradeLevelId ajeno a los 6 GUID de Celosan';
    END IF;

    SELECT COUNT(DISTINCT grade_level_id) INTO v_n
    FROM curriculum_load_subjects
    WHERE school_id = v_school;

    IF v_n <> 6 THEN
        RAISE EXCEPTION 'Celosan debe usar exactamente 6 GradeLevelId; hay %', v_n;
    END IF;

    SELECT id INTO v_b1 FROM curriculum_blocks WHERE code = 'B1';
    SELECT id INTO v_b2 FROM curriculum_blocks WHERE code = 'B2';
    IF v_b1 IS NULL OR v_b2 IS NULL THEN
        RAISE EXCEPTION 'Faltan curriculum_blocks B1/B2';
    END IF;

    SELECT COUNT(*) INTO v_n
    FROM curriculum_load_subjects cls
    JOIN subjects sub ON sub.id = cls.subject_id
    JOIN specialties sp ON sp.id = cls.specialty_id
    JOIN area a ON a.id = cls.area_id
    WHERE cls.id = v_new_cls
      AND cls.school_id = v_school
      AND cls.specialty_id = 'a89c4c24-5e5e-4d27-90b3-08421ecfb3bb'::uuid
      AND cls.grade_level_id = 'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid
      AND cls.area_id = '00630e6b-29d6-4122-98a1-6f3d992b50d9'::uuid
      AND cls.subject_id = '9015b4ae-5da3-4c43-8cad-ea03db48c854'::uuid
      AND cls.is_active = true
      AND sp.name = 'BACHILLER EN ELECTRICIDAD'
      AND a.name = 'CIENTÍFICA'
      AND sub.name = 'EDUCACIÓN FÍSICA Y SALUD INTEGRAL';

    IF v_n <> 1 THEN
        RAISE EXCEPTION 'La CLS estructural de Electricidad 11 no coincide o fue modificada; no se borra';
    END IF;

    SELECT COUNT(*) INTO v_n
    FROM curriculum_load_hours
    WHERE curriculum_load_subject_id = v_new_cls;

    IF v_n <> 1 THEN
        RAISE EXCEPTION 'La CLS estructural de Electricidad 11 no tiene exactamente 1 hora; hay %. No se borra', v_n;
    END IF;

    SELECT COUNT(*) INTO v_n
    FROM curriculum_load_hours h
    WHERE h.id = 'c19100ab-5e10-4000-a000-6e42399f6f17'::uuid
      AND h.curriculum_load_subject_id = v_new_cls
      AND h.curriculum_block_id = v_b2
      AND h.hours = 2;

    IF v_n <> 1 THEN
        RAISE EXCEPTION 'La hora B2=2 de Electricidad 11 fue modificada o no coincide con el manifiesto; no se borra';
    END IF;

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

    IF (SELECT COUNT(*) FROM meduca_hours) <> 191
       OR (SELECT COUNT(DISTINCT hour_id) FROM meduca_hours) <> 191 THEN
        RAISE EXCEPTION 'El manifiesto de rollback debe tener 191 UUID únicos';
    END IF;

    SELECT COUNT(*) INTO v_n
    FROM curriculum_load_hours
    WHERE id IN (SELECT hour_id FROM meduca_hours);

    IF v_n <> 191 THEN
        RAISE EXCEPTION 'Deben existir exactamente los 191 UUID del manifiesto; hay %', v_n;
    END IF;

    SELECT COUNT(*) INTO v_n
    FROM curriculum_load_hours h
    JOIN curriculum_load_subjects cls ON cls.id = h.curriculum_load_subject_id
    WHERE cls.school_id = v_school
      AND h.id IN (SELECT hour_id FROM meduca_hours);

    IF v_n <> 191 THEN
        RAISE EXCEPTION 'Los 191 UUID no pertenecen todos, de forma indirecta, al SchoolId autorizado';
    END IF;

    SELECT COUNT(*) INTO v_hours
    FROM curriculum_load_hours h
    JOIN curriculum_load_subjects cls ON cls.id = h.curriculum_load_subject_id
    WHERE cls.school_id = v_school;

    IF v_hours <> 191 THEN
        RAISE EXCEPTION 'El estado posterior al apply no es el esperado: hay % horas de la escuela (se esperaban 191)', v_hours;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM curriculum_load_hours h
        JOIN curriculum_load_subjects cls ON cls.id = h.curriculum_load_subject_id
        WHERE cls.school_id = v_school
          AND h.id NOT IN (SELECT hour_id FROM meduca_hours)
    ) THEN
        RAISE EXCEPTION 'Hay horas ajenas a los 191 UUID del manifiesto; no se borra';
    END IF;

    v_mismatch := 0;
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

        IF v_n <> 1 THEN
            RAISE EXCEPTION 'No se resolvió el CLS esperado para % / % / % / % (coincidencias %)',
                v_row.specialty, v_row.grade, v_row.area, v_row.subject, v_n;
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

        SELECT COUNT(*) INTO v_n
        FROM curriculum_load_hours h
        JOIN curriculum_load_subjects cls ON cls.id = h.curriculum_load_subject_id
        WHERE h.id = v_row.hour_id
          AND h.curriculum_load_subject_id = v_cls
          AND h.curriculum_block_id = v_block
          AND h.hours = v_row.hours
          AND cls.school_id = v_school;

        IF v_n <> 1 THEN
            v_mismatch := v_mismatch + 1;
            RAISE EXCEPTION
                'Fila modificada, faltante o distinta del manifiesto (hour_id %, programa %, grado %, área %, materia %, bloque %, horas %). No se borra.',
                v_row.hour_id, v_row.specialty, v_row.grade, v_row.area, v_row.subject, v_row.block_code, v_row.hours;
        END IF;
    END LOOP;

    IF v_mismatch <> 0 THEN
        RAISE EXCEPTION 'Hay % filas que no coinciden con el manifiesto; no se borra', v_mismatch;
    END IF;

    DELETE FROM curriculum_load_hours
    WHERE id IN (
        SELECT hour_id FROM meduca_hours
    );

    GET DIAGNOSTICS v_deleted = ROW_COUNT;
    IF v_deleted <> 191 THEN
        RAISE EXCEPTION 'El DELETE debía eliminar exactamente 191 filas; eliminó %', v_deleted;
    END IF;

    IF EXISTS (
        SELECT 1 FROM curriculum_load_hours
        WHERE id IN (SELECT hour_id FROM meduca_hours)
    ) THEN
        RAISE EXCEPTION 'Tras el DELETE aún permanece alguno de los 191 UUID';
    END IF;

    DELETE FROM curriculum_load_subjects
    WHERE id = v_new_cls;

    GET DIAGNOSTICS v_deleted = ROW_COUNT;
    IF v_deleted <> 1 THEN
        RAISE EXCEPTION 'Debía eliminarse exactamente 1 CLS estructural; eliminó %', v_deleted;
    END IF;

    IF EXISTS (
        SELECT 1 FROM curriculum_load_subjects WHERE id = v_new_cls
    ) THEN
        RAISE EXCEPTION 'La CLS estructural de Electricidad 11 sigue presente';
    END IF;

    SELECT COUNT(*) INTO v_cls_ok
    FROM curriculum_load_subjects
    WHERE school_id = v_school;

    IF v_cls_ok <> 205 THEN
        RAISE EXCEPTION 'Tras borrar la CLS estructural deben quedar 205; hay %', v_cls_ok;
    END IF;

    CREATE TEMP TABLE meduca_moves_rb (
        cls_id uuid NOT NULL,
        specialty text NOT NULL,
        subject text NOT NULL,
        current_grade text NOT NULL,
        restore_grade text NOT NULL,
        current_grade_id uuid NOT NULL,
        restore_grade_id uuid NOT NULL
    ) ON COMMIT DROP;

    INSERT INTO meduca_moves_rb (cls_id, specialty, subject, current_grade, restore_grade, current_grade_id, restore_grade_id)
    VALUES
    ('6bf0b5e5-145e-477a-95f9-f9dc92f7f95a'::uuid, 'BACHILLER EN TURISMO', 'HISTORIA DE PANAMÁ', '10', '11', 'c3180447-1afd-4ba8-9ebb-4de5bc9eb5c3'::uuid, 'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid),
    ('7e71c8de-64f7-44dd-8397-fa62f7a57007'::uuid, 'BACHILLER EN TURISMO', 'GEOGRAFÍA DE PANAMÁ', '11', '10', 'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid, 'c3180447-1afd-4ba8-9ebb-4de5bc9eb5c3'::uuid),
    ('0f09b724-0f76-450d-8818-97c301e63ffc'::uuid, 'BACHILLER EN TURISMO', 'GEOGRAFÍA TURÍSTICA DE PANAMÁ', '11', '12', 'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid, '171563c1-3c14-486f-9d00-a7cf1b67f9a1'::uuid),
    ('d7f63ab0-9348-4c11-8a41-af5e880e4e9f'::uuid, 'BACHILLER EN TURISMO', 'BELLAS ARTES', '11', '10', 'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid, 'c3180447-1afd-4ba8-9ebb-4de5bc9eb5c3'::uuid),
    ('2372a8f3-3f32-4299-87b6-bc578714d9d9'::uuid, 'BACHILLER EN TURISMO', 'GEOGRAFÍA TURÍSTICA DEL MUNDO', '12', '11', '171563c1-3c14-486f-9d00-a7cf1b67f9a1'::uuid, 'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid),
    ('1828befa-566f-48a8-804e-e8fb4d60076f'::uuid, 'BACHILLER EN AUTOTRÓNICA', 'TALLER III (TECNOLOGÍA Y TALLER APLICADO)', '11', '12', 'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid, '171563c1-3c14-486f-9d00-a7cf1b67f9a1'::uuid),
    ('2a97b7db-fed7-4643-b33a-079bc988ed74'::uuid, 'BACHILLER EN AUTOTRÓNICA', 'TALLER V (MANTENIMIENTO AUTOMOTRIZ)', '11', '12', 'e921f5af-cbb1-4507-ac89-f4749569c28e'::uuid, '171563c1-3c14-486f-9d00-a7cf1b67f9a1'::uuid);

    FOR v_row IN SELECT * FROM meduca_moves_rb LOOP
        v_from := v_row.current_grade_id;
        v_to := v_row.restore_grade_id;

        IF NOT EXISTS (
            SELECT 1 FROM meduca_grades
            WHERE grade_code = v_row.current_grade AND grade_id = v_from
        ) OR NOT EXISTS (
            SELECT 1 FROM meduca_grades
            WHERE grade_code = v_row.restore_grade AND grade_id = v_to
        ) THEN
            RAISE EXCEPTION 'GradeLevelId de rollback no coincide con el mapa de Celosan: % %',
                v_row.current_grade, v_row.restore_grade;
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
            RAISE EXCEPTION 'Rollback GUID inválido: % % (grado actual %, coincidencias %)',
                v_row.specialty, v_row.subject, v_row.current_grade, v_n;
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
            RAISE EXCEPTION 'No se puede restaurar % a grado %: destino ocupado', v_row.subject, v_row.restore_grade;
        END IF;

        UPDATE curriculum_load_subjects
        SET grade_level_id = v_to,
            updated_at = CURRENT_TIMESTAMP
        WHERE id = v_row.cls_id;
    END LOOP;

    INSERT INTO curriculum_load_hours (
        id, curriculum_load_subject_id, curriculum_block_id, hours, created_at
    ) VALUES
        ('ae8c2ddc-c69d-40d7-91ba-3031a23e75a5'::uuid, '1fb32f05-b713-4947-be99-f51bcf7d1c52'::uuid, v_b1, 0.00, CURRENT_TIMESTAMP),
        ('b1165ed7-6075-4ff6-847c-85ad772a92d2'::uuid, 'e4f8c4d0-40be-48fb-834c-ed34c72daaa9'::uuid, v_b1, 2.00, CURRENT_TIMESTAMP);

    SELECT COUNT(*) INTO v_hours
    FROM curriculum_load_hours h
    JOIN curriculum_load_subjects cls ON cls.id = h.curriculum_load_subject_id
    WHERE cls.school_id = v_school;

    IF v_hours <> 2 THEN
        RAISE EXCEPTION 'Tras rollback deben quedar 2 horas de prueba; hay %', v_hours;
    END IF;

    SELECT COUNT(*) INTO v_n
    FROM curriculum_load_hours
    WHERE id = 'ae8c2ddc-c69d-40d7-91ba-3031a23e75a5'::uuid
      AND curriculum_load_subject_id = '1fb32f05-b713-4947-be99-f51bcf7d1c52'::uuid
      AND curriculum_block_id = v_b1
      AND hours = 0;

    IF v_n <> 1 THEN
        RAISE EXCEPTION 'No se restauró la fila de prueba de Premedia';
    END IF;

    SELECT COUNT(*) INTO v_n
    FROM curriculum_load_hours
    WHERE id = 'b1165ed7-6075-4ff6-847c-85ad772a92d2'::uuid
      AND curriculum_load_subject_id = 'e4f8c4d0-40be-48fb-834c-ed34c72daaa9'::uuid
      AND curriculum_block_id = v_b1
      AND hours = 2;

    IF v_n <> 1 THEN
        RAISE EXCEPTION 'No se restauró la fila de prueba de Autotrónica';
    END IF;

    SELECT COUNT(*) INTO v_n
    FROM meduca_moves_rb m
    JOIN curriculum_load_subjects cls ON cls.id = m.cls_id
    WHERE cls.school_id = v_school
      AND cls.grade_level_id = m.restore_grade_id;

    IF v_n <> 7 THEN
        RAISE EXCEPTION 'curriculum_load_subjects no volvió a su estado anterior (movimientos restaurados: %)', v_n;
    END IF;

    SELECT COUNT(*) INTO v_cls_ok
    FROM curriculum_load_subjects
    WHERE school_id = v_school;

    IF v_cls_ok <> 205 THEN
        RAISE EXCEPTION 'Tras rollback deben seguir existiendo 205 CLS; hay %', v_cls_ok;
    END IF;
END
$rb$;

COMMIT;
