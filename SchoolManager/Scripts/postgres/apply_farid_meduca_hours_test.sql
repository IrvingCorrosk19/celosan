-- Prueba local: completar 4 periodos MEDUCA de TI / 10-A2 / Farid Alain
-- No contiene contraseñas. No toca la celda del jueves ni las 6 celdas de A2.
--
-- Propuesta (espacios 100% libres, jornada Noche, lun–vie):
-- | Día       | Bloque   | Inicio | Final  | Farid libre | 10-A2 libre |
-- | Lunes     | Bloque 1 | 18:10  | 19:00  | sí          | sí          |
-- | Martes    | Bloque 1 | 18:10  | 19:00  | sí          | sí          |
-- | Miércoles | Bloque 1 | 18:10  | 19:00  | sí          | sí          |
--
-- GUID explícitos de las 3 filas nuevas:
--   Lunes     9666022e-0a5a-4485-a5d8-475c533687b5
--   Martes    43e28d9d-091c-480c-b0d1-cd7120aa0b29
--   Miércoles 306ce1c3-2916-4e80-bf0b-85a2cc7a7a5b
--
-- Si cualquier validación falla, RAISE EXCEPTION aborta la transacción.

\set ON_ERROR_STOP on

BEGIN;

DO $$
DECLARE
    v_school uuid := '6e42399f-6f17-4585-b92e-fa4fff02cb65';
    v_ta uuid := 'c6a89793-8264-421a-afdc-d8562a60edac';
    v_sa_id uuid := 'ac3d5f8c-2a0a-4875-ae8e-3365e847e669';
    v_farid uuid := 'f222d4db-ea3b-43a5-8a15-300d2944c051';
    v_year uuid := 'f7ccb57f-fa3e-4d9f-973b-552030c9852d';
    v_original uuid := 'b48ef295-8d6a-4f40-8391-7e716f9673db';
    v_slot_b1 uuid := '28104a96-83b1-4e7c-ad17-9ae16a2b1c4b';
    v_new_mon uuid := '9666022e-0a5a-4485-a5d8-475c533687b5';
    v_new_tue uuid := '43e28d9d-091c-480c-b0d1-cd7120aa0b29';
    v_new_wed uuid := '306ce1c3-2916-4e80-bf0b-85a2cc7a7a5b';
    v_group uuid;
    v_ta_ok integer;
    v_sa_ok integer;
    v_year_ok integer;
    v_original_ok integer;
    v_slot_ok integer;
    v_farid_busy integer;
    v_group_busy integer;
    v_exists integer;
    v_inserted integer;
    v_total integer;
    v_night integer;
    v_minutes numeric;
    v_teacher_conflict integer;
    v_group_conflict integer;
    v_a2_untouched integer;
BEGIN
    SELECT sa.group_id INTO v_group
    FROM subject_assignments sa
    WHERE sa.id = v_sa_id;

    IF v_group IS NULL THEN
        RAISE EXCEPTION 'SubjectAssignment objetivo no existe';
    END IF;

    SELECT COUNT(*) INTO v_ta_ok
    FROM teacher_assignments
    WHERE id = v_ta
      AND teacher_id = v_farid
      AND subject_assignment_id = v_sa_id;

    IF v_ta_ok <> 1 THEN
        RAISE EXCEPTION 'La TeacherAssignment no pertenece a Farid o no coincide el SubjectAssignment (encontradas %)', v_ta_ok;
    END IF;

    SELECT COUNT(*) INTO v_sa_ok
    FROM subject_assignments sa
    JOIN groups g ON g.id = sa.group_id
    JOIN subjects sub ON sub.id = sa.subject_id
    JOIN specialties sp ON sp.id = sa.specialty_id
    JOIN grade_levels gl ON gl.id = sa.grade_level_id
    WHERE sa.id = v_sa_id
      AND g.name = '10-A2'
      AND gl.name = '10'
      AND sp.name = 'BACHILLER EN AUTOTRÓNICA'
      AND sub.name = 'TECNOLOGÍA DE LA INFORMACIÓN'
      AND g.school_id = v_school
      AND sa."SchoolId" = v_school;

    IF v_sa_ok <> 1 THEN
        RAISE EXCEPTION 'SubjectAssignment no coincide con 10-A2 / TI / Autotrónica / escuela local (encontrados %)', v_sa_ok;
    END IF;

    SELECT COUNT(*) INTO v_year_ok
    FROM academic_years
    WHERE id = v_year
      AND school_id = v_school;

    IF v_year_ok <> 1 THEN
        RAISE EXCEPTION 'AcademicYear oficial no pertenece a la escuela local';
    END IF;

    SELECT COUNT(*) INTO v_original_ok
    FROM schedule_entries se
    JOIN time_slots ts ON ts.id = se.time_slot_id
    JOIN shifts sh ON sh.id = ts.shift_id
    WHERE se.id = v_original
      AND se.teacher_assignment_id = v_ta
      AND se.academic_year_id = v_year
      AND se.time_slot_id = '0809045c-73f4-43fe-acd0-f2a2c38810ce'
      AND se.day_of_week = 4
      AND sh.name = 'Noche'
      AND ts.is_active
      AND ts.school_id = v_school;

    IF v_original_ok <> 1 THEN
        RAISE EXCEPTION 'La celda original del jueves no está intacta o no es nocturna';
    END IF;

    SELECT COUNT(*) INTO v_slot_ok
    FROM time_slots ts
    JOIN shifts sh ON sh.id = ts.shift_id
    WHERE ts.id = v_slot_b1
      AND ts.is_active
      AND sh.name = 'Noche'
      AND ts.school_id = v_school
      AND EXTRACT(EPOCH FROM (ts.end_time - ts.start_time)) / 60 = 50;

    IF v_slot_ok <> 1 THEN
        RAISE EXCEPTION 'Bloque 1 nocturno inválido, inactivo o no dura 50 minutos';
    END IF;

    SELECT COUNT(*) INTO v_farid_busy
    FROM schedule_entries se
    JOIN teacher_assignments ta ON ta.id = se.teacher_assignment_id
    WHERE ta.teacher_id = v_farid
      AND se.academic_year_id = v_year
      AND se.time_slot_id = v_slot_b1
      AND se.day_of_week IN (1, 2, 3);

    IF v_farid_busy <> 0 THEN
        RAISE EXCEPTION 'Farid ya tiene clase en uno de los espacios propuestos (lunes–miércoles Bloque 1)';
    END IF;

    SELECT COUNT(*) INTO v_group_busy
    FROM schedule_entries se
    JOIN teacher_assignments ta ON ta.id = se.teacher_assignment_id
    JOIN subject_assignments sa ON sa.id = ta.subject_assignment_id
    WHERE sa.group_id = v_group
      AND se.academic_year_id = v_year
      AND se.time_slot_id = v_slot_b1
      AND se.day_of_week IN (1, 2, 3);

    IF v_group_busy <> 0 THEN
        RAISE EXCEPTION 'El grupo 10-A2 ya tiene clase en uno de los espacios propuestos';
    END IF;

    SELECT COUNT(*) INTO v_exists
    FROM schedule_entries
    WHERE id IN (v_new_mon, v_new_tue, v_new_wed)
       OR (teacher_assignment_id = v_ta
           AND academic_year_id = v_year
           AND time_slot_id = v_slot_b1
           AND day_of_week IN (1, 2, 3));

    IF v_exists <> 0 THEN
        RAISE EXCEPTION 'Ya existe una ScheduleEntry equivalente o con esos GUID (% filas)', v_exists;
    END IF;

    INSERT INTO schedule_entries (
        id, teacher_assignment_id, time_slot_id, day_of_week, academic_year_id, created_at, created_by
    )
    VALUES
        (v_new_mon, v_ta, v_slot_b1, 1, v_year, timezone('utc', now()), v_farid),
        (v_new_tue, v_ta, v_slot_b1, 2, v_year, timezone('utc', now()), v_farid),
        (v_new_wed, v_ta, v_slot_b1, 3, v_year, timezone('utc', now()), v_farid);

    GET DIAGNOSTICS v_inserted = ROW_COUNT;

    IF v_inserted <> 3 THEN
        RAISE EXCEPTION 'Se insertaron % filas; se esperaban exactamente 3', v_inserted;
    END IF;

    SELECT COUNT(*) INTO v_total
    FROM schedule_entries
    WHERE teacher_assignment_id = v_ta
      AND academic_year_id = v_year;

    IF v_total <> 4 THEN
        RAISE EXCEPTION 'Tras el INSERT la TA no tiene 4 celdas en el año oficial (encontradas %)', v_total;
    END IF;

    SELECT COUNT(*) INTO v_night
    FROM schedule_entries se
    JOIN time_slots ts ON ts.id = se.time_slot_id
    JOIN shifts sh ON sh.id = ts.shift_id
    WHERE se.teacher_assignment_id = v_ta
      AND se.academic_year_id = v_year
      AND sh.name = 'Noche'
      AND ts.is_active
      AND ts.school_id = v_school
      AND se.day_of_week BETWEEN 1 AND 5;

    IF v_night <> 4 THEN
        RAISE EXCEPTION 'No todas las celdas son nocturnas de lunes a viernes (encontradas %)', v_night;
    END IF;

    SELECT COALESCE(SUM(EXTRACT(EPOCH FROM (ts.end_time - ts.start_time)) / 60), 0)
    INTO v_minutes
    FROM schedule_entries se
    JOIN time_slots ts ON ts.id = se.time_slot_id
    WHERE se.teacher_assignment_id = v_ta
      AND se.academic_year_id = v_year;

    IF v_minutes <> 200 THEN
        RAISE EXCEPTION 'Los minutos semanales no suman 200 (suma %)', v_minutes;
    END IF;

    SELECT COUNT(*) INTO v_teacher_conflict
    FROM (
        SELECT se.day_of_week, se.time_slot_id
        FROM schedule_entries se
        JOIN teacher_assignments ta ON ta.id = se.teacher_assignment_id
        WHERE ta.teacher_id = v_farid
          AND se.academic_year_id = v_year
        GROUP BY se.day_of_week, se.time_slot_id
        HAVING COUNT(*) > 1
    ) x;

    IF v_teacher_conflict <> 0 THEN
        RAISE EXCEPTION 'Conflicto de horario del docente (% solapes)', v_teacher_conflict;
    END IF;

    SELECT COUNT(*) INTO v_group_conflict
    FROM (
        SELECT se.day_of_week, se.time_slot_id
        FROM schedule_entries se
        JOIN teacher_assignments ta ON ta.id = se.teacher_assignment_id
        JOIN subject_assignments sa ON sa.id = ta.subject_assignment_id
        WHERE sa.group_id = v_group
          AND se.academic_year_id = v_year
        GROUP BY se.day_of_week, se.time_slot_id
        HAVING COUNT(*) > 1
    ) x;

    IF v_group_conflict <> 0 THEN
        RAISE EXCEPTION 'Conflicto de horario del grupo 10-A2 (% solapes)', v_group_conflict;
    END IF;

    SELECT COUNT(*) INTO v_original_ok
    FROM schedule_entries
    WHERE id = v_original
      AND teacher_assignment_id = v_ta
      AND academic_year_id = v_year
      AND time_slot_id = '0809045c-73f4-43fe-acd0-f2a2c38810ce'
      AND day_of_week = 4;

    IF v_original_ok <> 1 THEN
        RAISE EXCEPTION 'La celda original del jueves ya no está intacta';
    END IF;

    SELECT COUNT(*) INTO v_a2_untouched
    FROM teacher_assignments ta
    JOIN schedule_entries se ON se.teacher_assignment_id = ta.id
    WHERE ta.id = '9c2a33e7-9c8d-4381-a12c-c6fdc9b53544'
      AND ta.teacher_id = v_farid
      AND se.academic_year_id = '837b80b3-cb41-4913-b044-d16467e85fff';

    IF v_a2_untouched <> 6 THEN
        RAISE EXCEPTION 'Las 6 celdas de A2 de Farid no quedaron intactas (encontradas %)', v_a2_untouched;
    END IF;
END $$;

COMMIT;
