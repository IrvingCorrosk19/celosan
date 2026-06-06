-- Fase D – Promoción parcial por materia (idempotente)
-- Aplicar con: psql ... -f Scripts/ApplySubjectPromotionRecords.sql

BEGIN;

CREATE TABLE IF NOT EXISTS subject_promotion_records (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    student_id uuid NOT NULL,
    subject_id uuid NOT NULL,
    grade_level_id uuid NOT NULL,
    academic_year_id uuid,
    trimester character varying(10) NOT NULL,
    outcome character varying(20) NOT NULL,
    final_score numeric(5,2),
    student_subject_assignment_id uuid,
    promoted_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    school_id uuid,
    created_at timestamp with time zone,
    created_by uuid,
    CONSTRAINT subject_promotion_records_pkey PRIMARY KEY (id)
);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_subject_promotion_records_schools_school_id'
    ) THEN
        ALTER TABLE subject_promotion_records
            ADD CONSTRAINT "FK_subject_promotion_records_schools_school_id"
            FOREIGN KEY (school_id) REFERENCES schools (id);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'subject_promotion_records_academic_year_id_fkey'
    ) THEN
        ALTER TABLE subject_promotion_records
            ADD CONSTRAINT subject_promotion_records_academic_year_id_fkey
            FOREIGN KEY (academic_year_id) REFERENCES academic_years (id) ON DELETE SET NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'subject_promotion_records_grade_level_id_fkey'
    ) THEN
        ALTER TABLE subject_promotion_records
            ADD CONSTRAINT subject_promotion_records_grade_level_id_fkey
            FOREIGN KEY (grade_level_id) REFERENCES grade_levels (id) ON DELETE RESTRICT;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'subject_promotion_records_ssa_id_fkey'
    ) THEN
        ALTER TABLE subject_promotion_records
            ADD CONSTRAINT subject_promotion_records_ssa_id_fkey
            FOREIGN KEY (student_subject_assignment_id) REFERENCES student_subject_assignments (id) ON DELETE SET NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'subject_promotion_records_student_id_fkey'
    ) THEN
        ALTER TABLE subject_promotion_records
            ADD CONSTRAINT subject_promotion_records_student_id_fkey
            FOREIGN KEY (student_id) REFERENCES users (id) ON DELETE CASCADE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'subject_promotion_records_subject_id_fkey'
    ) THEN
        ALTER TABLE subject_promotion_records
            ADD CONSTRAINT subject_promotion_records_subject_id_fkey
            FOREIGN KEY (subject_id) REFERENCES subjects (id) ON DELETE RESTRICT;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_subject_promotion_records_academic_year_id"
    ON subject_promotion_records (academic_year_id);
CREATE INDEX IF NOT EXISTS "IX_subject_promotion_records_grade_level_id"
    ON subject_promotion_records (grade_level_id);
CREATE INDEX IF NOT EXISTS "IX_subject_promotion_records_school_id"
    ON subject_promotion_records (school_id);
CREATE INDEX IF NOT EXISTS "IX_subject_promotion_records_student_id"
    ON subject_promotion_records (student_id);
CREATE INDEX IF NOT EXISTS "IX_subject_promotion_records_student_subject_assignment_id"
    ON subject_promotion_records (student_subject_assignment_id);
CREATE INDEX IF NOT EXISTS "IX_subject_promotion_records_student_subject_year_trimester"
    ON subject_promotion_records (student_id, subject_id, academic_year_id, trimester);
CREATE INDEX IF NOT EXISTS "IX_subject_promotion_records_subject_id"
    ON subject_promotion_records (subject_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260606110441_AddSubjectPromotionRecords', '9.0.3')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
