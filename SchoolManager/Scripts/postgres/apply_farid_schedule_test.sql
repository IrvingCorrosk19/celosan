-- Prueba local reversible: Julio → Farid Alain
-- Tecnología de la Información / 10-A2 / Autotrónica 10° / CELO San Miguelito
-- No contiene contraseñas. No toca A2 ni otras celdas.
-- Si cualquier validación falla, RAISE EXCEPTION aborta la transacción (ROLLBACK).

\set ON_ERROR_STOP on

BEGIN;

DO $$
DECLARE
    v_ta_target integer;
    v_farid_same_sa integer;
    v_julio_user integer;
    v_farid_user integer;
    v_sa integer;
    v_entries integer;
    v_night_entries integer;
    v_school uuid := '6e42399f-6f17-4585-b92e-fa4fff02cb65';
    v_ta uuid := 'c6a89793-8264-421a-afdc-d8562a60edac';
    v_sa_id uuid := 'ac3d5f8c-2a0a-4875-ae8e-3365e847e669';
    v_julio uuid := '81751dc8-ddcf-4d11-9cea-ec5bd34792d0';
    v_farid uuid := 'f222d4db-ea3b-43a5-8a15-300d2944c051';
    v_year uuid := 'f7ccb57f-fa3e-4d9f-973b-552030c9852d';
    v_entry uuid := 'b48ef295-8d6a-4f40-8391-7e716f9673db';
BEGIN
    SELECT COUNT(*) INTO v_julio_user
    FROM users
    WHERE id = v_julio
      AND school_id = v_school
      AND role = 'teacher';

    IF v_julio_user <> 1 THEN
        RAISE EXCEPTION 'Julio no encontrado como teacher de la escuela local (encontrados %)', v_julio_user;
    END IF;

    SELECT COUNT(*) INTO v_farid_user
    FROM users
    WHERE id = v_farid
      AND school_id = v_school
      AND role = 'teacher';

    IF v_farid_user <> 1 THEN
        RAISE EXCEPTION 'Farid no encontrado como teacher de la escuela local (encontrados %)', v_farid_user;
    END IF;

    SELECT COUNT(*) INTO v_sa
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

    IF v_sa <> 1 THEN
        RAISE EXCEPTION 'SubjectAssignment objetivo no coincide (encontrados %)', v_sa;
    END IF;

    SELECT COUNT(*) INTO v_ta_target
    FROM teacher_assignments
    WHERE id = v_ta
      AND subject_assignment_id = v_sa_id
      AND teacher_id = v_julio;

    IF v_ta_target <> 1 THEN
        RAISE EXCEPTION 'Se esperaba exactamente 1 TeacherAssignment de Julio. Encontradas %', v_ta_target;
    END IF;

    SELECT COUNT(*) INTO v_farid_same_sa
    FROM teacher_assignments
    WHERE teacher_id = v_farid
      AND subject_assignment_id = v_sa_id;

    IF v_farid_same_sa <> 0 THEN
        RAISE EXCEPTION 'Farid ya tiene % TeacherAssignment para el mismo SubjectAssignment', v_farid_same_sa;
    END IF;

    SELECT COUNT(*) INTO v_entries
    FROM schedule_entries
    WHERE teacher_assignment_id = v_ta
      AND id = v_entry
      AND academic_year_id = v_year;

    IF v_entries <> 1 THEN
        RAISE EXCEPTION 'ScheduleEntry oficial no coincide (encontradas %)', v_entries;
    END IF;

    SELECT COUNT(*) INTO v_night_entries
    FROM schedule_entries se
    JOIN time_slots ts ON ts.id = se.time_slot_id
    JOIN shifts sh ON sh.id = ts.shift_id
    WHERE se.id = v_entry
      AND sh.name = 'Noche'
      AND ts.school_id = v_school;

    IF v_night_entries <> 1 THEN
        RAISE EXCEPTION 'La celda oficial no es un bloque nocturno de la escuela local';
    END IF;
END $$;

DO $$
DECLARE
    v_updated integer;
    v_ta uuid := 'c6a89793-8264-421a-afdc-d8562a60edac';
    v_sa_id uuid := 'ac3d5f8c-2a0a-4875-ae8e-3365e847e669';
    v_julio uuid := '81751dc8-ddcf-4d11-9cea-ec5bd34792d0';
    v_farid uuid := 'f222d4db-ea3b-43a5-8a15-300d2944c051';
BEGIN
    UPDATE teacher_assignments
    SET teacher_id = v_farid
    WHERE id = v_ta
      AND subject_assignment_id = v_sa_id
      AND teacher_id = v_julio;

    GET DIAGNOSTICS v_updated = ROW_COUNT;

    IF v_updated <> 1 THEN
        RAISE EXCEPTION 'El UPDATE cambió % filas; se esperaba 1', v_updated;
    END IF;
END $$;

DO $$
DECLARE
    v_farid_owns integer;
    v_julio_owns integer;
    v_entry_ok integer;
    v_a2_untouched integer;
    v_ta uuid := 'c6a89793-8264-421a-afdc-d8562a60edac';
    v_sa_id uuid := 'ac3d5f8c-2a0a-4875-ae8e-3365e847e669';
    v_julio uuid := '81751dc8-ddcf-4d11-9cea-ec5bd34792d0';
    v_farid uuid := 'f222d4db-ea3b-43a5-8a15-300d2944c051';
    v_year uuid := 'f7ccb57f-fa3e-4d9f-973b-552030c9852d';
    v_entry uuid := 'b48ef295-8d6a-4f40-8391-7e716f9673db';
    v_a2_ta uuid := '9c2a33e7-9c8d-4381-a12c-c6fdc9b53544';
BEGIN
    SELECT COUNT(*) INTO v_farid_owns
    FROM teacher_assignments
    WHERE id = v_ta
      AND teacher_id = v_farid
      AND subject_assignment_id = v_sa_id;

    IF v_farid_owns <> 1 THEN
        RAISE EXCEPTION 'Tras el UPDATE, Farid no es dueño de la TeacherAssignment objetivo';
    END IF;

    SELECT COUNT(*) INTO v_julio_owns
    FROM teacher_assignments
    WHERE id = v_ta
      AND teacher_id = v_julio;

    IF v_julio_owns <> 0 THEN
        RAISE EXCEPTION 'Julio sigue figurando en la TeacherAssignment objetivo';
    END IF;

    SELECT COUNT(*) INTO v_entry_ok
    FROM schedule_entries
    WHERE id = v_entry
      AND teacher_assignment_id = v_ta
      AND academic_year_id = v_year;

    IF v_entry_ok <> 1 THEN
        RAISE EXCEPTION 'La ScheduleEntry oficial ya no está vinculada a la misma TeacherAssignment/año';
    END IF;

    SELECT COUNT(*) INTO v_a2_untouched
    FROM teacher_assignments ta
    JOIN schedule_entries se ON se.teacher_assignment_id = ta.id
    WHERE ta.id = v_a2_ta
      AND ta.teacher_id = v_farid
      AND se.academic_year_id = '837b80b3-cb41-4913-b044-d16467e85fff';

    IF v_a2_untouched <> 6 THEN
        RAISE EXCEPTION 'Las celdas de A2 de Farid no quedaron intactas (encontradas %)', v_a2_untouched;
    END IF;
END $$;

COMMIT;
