ALTER TABLE school_schedule_configurations ADD COLUMN IF NOT EXISTS night_start_time time;
ALTER TABLE school_schedule_configurations ADD COLUMN IF NOT EXISTS night_block_duration_minutes integer;
ALTER TABLE school_schedule_configurations ADD COLUMN IF NOT EXISTS night_block_count integer;

ALTER TABLE attendance ADD COLUMN IF NOT EXISTS shift_id uuid;
ALTER TABLE attendance ADD COLUMN IF NOT EXISTS student_assignment_id uuid;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'attendance_shift_id_fkey') THEN
        ALTER TABLE attendance
            ADD CONSTRAINT attendance_shift_id_fkey
            FOREIGN KEY (shift_id)
            REFERENCES shifts(id)
            ON DELETE SET NULL;
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'attendance_student_assignment_id_fkey') THEN
        ALTER TABLE attendance
            ADD CONSTRAINT attendance_student_assignment_id_fkey
            FOREIGN KEY (student_assignment_id)
            REFERENCES student_assignments(id)
            ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_attendance_shift_id" ON attendance(shift_id);
CREATE INDEX IF NOT EXISTS "IX_attendance_student_assignment_id" ON attendance(student_assignment_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_attendance_student_date_group_grade_shift
    ON attendance(student_id, date, group_id, grade_id, shift_id);

UPDATE attendance a
SET
    student_assignment_id = sa.id,
    shift_id = COALESCE(a.shift_id, sa.shift_id)
FROM student_assignments sa
WHERE sa.student_id = a.student_id
  AND sa.group_id = a.group_id
  AND sa.grade_id = a.grade_id
  AND sa.is_active = true
  AND a.student_assignment_id IS NULL;
