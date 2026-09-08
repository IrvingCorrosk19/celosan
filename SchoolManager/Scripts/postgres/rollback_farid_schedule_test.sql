-- Rollback completo de la prueba Farid / 10-A2 / TI
-- 1) Elimina únicamente las 3 ScheduleEntries nuevas (GUID explícitos).
-- 2) No elimina la celda original b48ef295-8d6a-4f40-8391-7e716f9673db.
-- 3) Devuelve la TeacherAssignment a Julio.
-- No contiene contraseñas. No ejecutar salvo para revertir la prueba.

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
    v_new_mon uuid := '9666022e-0a5a-4485-a5d8-475c533687b5';
    v_new_tue uuid := '43e28d9d-091c-480c-b0d1-cd7120aa0b29';
    v_new_wed uuid := '306ce1c3-2916-4e80-bf0b-85a2cc7a7a5b';
    v_owned integer;
    v_deleted integer;
    v_original_ok integer;
BEGIN
    SELECT COUNT(*) INTO v_owned
    FROM schedule_entries se
    JOIN teacher_assignments ta ON ta.id = se.teacher_assignment_id
    JOIN subject_assignments sa ON sa.id = ta.subject_assignment_id
    JOIN groups g ON g.id = sa.group_id
    JOIN subjects sub ON sub.id = sa.subject_id
    WHERE se.id IN (v_new_mon, v_new_tue, v_new_wed)
      AND ta.id = v_ta
      AND ta.teacher_id = v_farid
      AND sa.id = v_sa_id
      AND se.academic_year_id = v_year
      AND g.name = '10-A2'
      AND sub.name = 'TECNOLOGÍA DE LA INFORMACIÓN'
      AND g.school_id = v_school
      AND se.id <> v_original;

    IF v_owned <> 3 THEN
        RAISE EXCEPTION 'Las 3 filas nuevas no coinciden con Farid / 10-A2 / TI / año oficial (encontradas %)', v_owned;
    END IF;

    DELETE FROM schedule_entries se
    WHERE se.id IN (v_new_mon, v_new_tue, v_new_wed)
      AND se.id <> v_original
      AND se.teacher_assignment_id = v_ta
      AND se.academic_year_id = v_year
      AND EXISTS (
          SELECT 1
          FROM teacher_assignments ta
          JOIN subject_assignments sa ON sa.id = ta.subject_assignment_id
          JOIN groups g ON g.id = sa.group_id
          JOIN subjects sub ON sub.id = sa.subject_id
          WHERE ta.id = se.teacher_assignment_id
            AND ta.teacher_id = v_farid
            AND sa.id = v_sa_id
            AND g.name = '10-A2'
            AND sub.name = 'TECNOLOGÍA DE LA INFORMACIÓN'
      );

    GET DIAGNOSTICS v_deleted = ROW_COUNT;

    IF v_deleted <> 3 THEN
        RAISE EXCEPTION 'El DELETE de las celdas nuevas afectó % filas; se esperaban 3', v_deleted;
    END IF;

    SELECT COUNT(*) INTO v_original_ok
    FROM schedule_entries
    WHERE id = v_original
      AND teacher_assignment_id = v_ta
      AND academic_year_id = v_year;

    IF v_original_ok <> 1 THEN
        RAISE EXCEPTION 'La celda original del jueves no debe eliminarse y ya no está intacta';
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
    SET teacher_id = v_julio
    WHERE id = v_ta
      AND subject_assignment_id = v_sa_id
      AND teacher_id = v_farid;

    GET DIAGNOSTICS v_updated = ROW_COUNT;

    IF v_updated <> 1 THEN
        RAISE EXCEPTION 'El ROLLBACK de TeacherAssignment cambió % filas; se esperaba 1', v_updated;
    END IF;
END $$;

DO $$
DECLARE
    v_julio_owns integer;
    v_entry_ok integer;
    v_new_left integer;
    v_ta uuid := 'c6a89793-8264-421a-afdc-d8562a60edac';
    v_julio uuid := '81751dc8-ddcf-4d11-9cea-ec5bd34792d0';
    v_entry uuid := 'b48ef295-8d6a-4f40-8391-7e716f9673db';
    v_new_mon uuid := '9666022e-0a5a-4485-a5d8-475c533687b5';
    v_new_tue uuid := '43e28d9d-091c-480c-b0d1-cd7120aa0b29';
    v_new_wed uuid := '306ce1c3-2916-4e80-bf0b-85a2cc7a7a5b';
BEGIN
    SELECT COUNT(*) INTO v_julio_owns
    FROM teacher_assignments
    WHERE id = v_ta
      AND teacher_id = v_julio;

    IF v_julio_owns <> 1 THEN
        RAISE EXCEPTION 'Tras restaurar, Julio no es dueño de la TeacherAssignment';
    END IF;

    SELECT COUNT(*) INTO v_entry_ok
    FROM schedule_entries
    WHERE id = v_entry
      AND teacher_assignment_id = v_ta;

    IF v_entry_ok <> 1 THEN
        RAISE EXCEPTION 'La ScheduleEntry oficial ya no está vinculada';
    END IF;

    SELECT COUNT(*) INTO v_new_left
    FROM schedule_entries
    WHERE id IN (v_new_mon, v_new_tue, v_new_wed);

    IF v_new_left <> 0 THEN
        RAISE EXCEPTION 'Quedaron % celdas nuevas; debían eliminarse las 3', v_new_left;
    END IF;
END $$;

COMMIT;
