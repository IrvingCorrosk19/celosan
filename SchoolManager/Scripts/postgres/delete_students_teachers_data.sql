-- Elimina usuarios con rol estudiante/profesor y datos ligados (BD schoolmanager / pruebas).
-- Orden respetando FKs. Ejecutar con psql contra la base objetivo.

BEGIN;

CREATE TEMP TABLE del_users ON COMMIT DROP AS
SELECT id
FROM users
WHERE lower(role) IN ('estudiante', 'student', 'teacher');

-- Notas (matrícula y/o alumno y/o actividad del docente)
DELETE FROM student_activity_scores sas
WHERE sas.student_id IN (SELECT id FROM del_users)
   OR sas.student_assignment_id IN (
        SELECT sa.id FROM student_assignments sa WHERE sa.student_id IN (SELECT id FROM del_users))
   OR sas.activity_id IN (
        SELECT a.id FROM activities a WHERE a.teacher_id IN (SELECT id FROM del_users));

DELETE FROM student_assignments WHERE student_id IN (SELECT id FROM del_users);

DELETE FROM schedule_entries
WHERE teacher_assignment_id IN (
    SELECT ta.id FROM teacher_assignments ta WHERE ta.teacher_id IN (SELECT id FROM del_users));

DELETE FROM teacher_assignments WHERE teacher_id IN (SELECT id FROM del_users);

DELETE FROM activities WHERE teacher_id IN (SELECT id FROM del_users);

DELETE FROM attendance
WHERE student_id IN (SELECT id FROM del_users) OR teacher_id IN (SELECT id FROM del_users);

DELETE FROM discipline_reports
WHERE student_id IN (SELECT id FROM del_users) OR teacher_id IN (SELECT id FROM del_users);

DELETE FROM orientation_reports
WHERE student_id IN (SELECT id FROM del_users)
   OR teacher_id IN (SELECT id FROM del_users)
   OR created_by IN (SELECT id FROM del_users)
   OR updated_by IN (SELECT id FROM del_users);

DELETE FROM scan_logs WHERE student_id IN (SELECT id FROM del_users);

DELETE FROM student_id_cards WHERE student_id IN (SELECT id FROM del_users);
DELETE FROM student_qr_tokens WHERE student_id IN (SELECT id FROM del_users);

DELETE FROM counselor_assignments WHERE user_id IN (SELECT id FROM del_users);
DELETE FROM email_queues WHERE user_id IN (SELECT id FROM del_users);

DELETE FROM user_grades WHERE user_id IN (SELECT id FROM del_users);
DELETE FROM user_groups WHERE user_id IN (SELECT id FROM del_users);
DELETE FROM user_subjects WHERE user_id IN (SELECT id FROM del_users);

DELETE FROM teacher_work_plan_review_logs
WHERE performed_by_user_id IN (SELECT id FROM del_users);

DELETE FROM teacher_work_plans WHERE teacher_id IN (SELECT id FROM del_users);

DELETE FROM schedule_entries WHERE created_by IN (SELECT id FROM del_users);

UPDATE prematriculations SET parent_id = NULL WHERE parent_id IN (SELECT id FROM del_users);
UPDATE prematriculations SET confirmed_by = NULL WHERE confirmed_by IN (SELECT id FROM del_users);
UPDATE prematriculations SET cancelled_by = NULL WHERE cancelled_by IN (SELECT id FROM del_users);
UPDATE prematriculations SET rejected_by = NULL WHERE rejected_by IN (SELECT id FROM del_users);

DELETE FROM payments
WHERE prematriculation_id IN (SELECT id FROM prematriculations WHERE student_id IN (SELECT id FROM del_users))
   OR student_id IN (SELECT id FROM del_users);

DELETE FROM prematriculations WHERE student_id IN (SELECT id FROM del_users);

DELETE FROM student_payment_access WHERE student_id IN (SELECT id FROM del_users);

DELETE FROM messages
WHERE sender_id IN (SELECT id FROM del_users) OR recipient_id IN (SELECT id FROM del_users);

DELETE FROM email_jobs WHERE created_by_user_id IN (SELECT id FROM del_users);

DELETE FROM audit_logs WHERE user_id IN (SELECT id FROM del_users);

DELETE FROM students
WHERE id IN (SELECT id FROM del_users) OR parent_id IN (SELECT id FROM del_users);

DELETE FROM users WHERE id IN (SELECT id FROM del_users);

COMMIT;
