-- Homologa en Render SOLO la estructura de AddCurriculumLoadStructure.
-- No copia horas ni CLS de la base local.
-- Idempotente. Si cualquier chequeo falla, ROLLBACK.

\set ON_ERROR_STOP on
SET client_encoding TO 'UTF8';

BEGIN;

DO $h$
DECLARE
    v_mig text := '20260905144243_AddCurriculumLoadStructure';
    v_cls integer;
BEGIN
    IF current_database() <> 'schoolmanager_daqf' THEN
        RAISE EXCEPTION 'Base inesperada: %', current_database();
    END IF;

    IF EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = v_mig)
       AND to_regclass('public.curriculum_blocks') IS NOT NULL
       AND to_regclass('public.curriculum_load_subjects') IS NOT NULL
       AND to_regclass('public.curriculum_load_hours') IS NOT NULL
       AND EXISTS (
           SELECT 1 FROM information_schema.columns
           WHERE table_schema = 'public' AND table_name = 'users'
             AND column_name = 'can_edit_curriculum_load') THEN
        RAISE NOTICE 'Estructura ya homologada. Nada que hacer.';
        RETURN;
    END IF;

    IF EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = v_mig) THEN
        RAISE EXCEPTION 'La migracion % ya esta en historial pero faltan objetos.', v_mig;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'users'
          AND column_name = 'can_edit_curriculum_load') THEN
        ALTER TABLE users
            ADD COLUMN can_edit_curriculum_load boolean NULL DEFAULT false;
    END IF;

    CREATE TABLE IF NOT EXISTS curriculum_blocks (
        id uuid NOT NULL DEFAULT gen_random_uuid(),
        code character varying(8) NOT NULL,
        name character varying(40) NOT NULL,
        sort_order integer NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
        CONSTRAINT curriculum_blocks_pkey PRIMARY KEY (id)
    );

    CREATE UNIQUE INDEX IF NOT EXISTS uq_curriculum_blocks_code
        ON curriculum_blocks (code);

    CREATE TABLE IF NOT EXISTS curriculum_load_subjects (
        id uuid NOT NULL DEFAULT gen_random_uuid(),
        school_id uuid NOT NULL,
        specialty_id uuid NOT NULL,
        grade_level_id uuid NOT NULL,
        area_id uuid NOT NULL,
        subject_id uuid NOT NULL,
        is_active boolean NOT NULL DEFAULT true,
        created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
        updated_at timestamp with time zone NULL,
        created_by uuid NULL,
        updated_by uuid NULL,
        CONSTRAINT curriculum_load_subjects_pkey PRIMARY KEY (id),
        CONSTRAINT curriculum_load_subjects_area_id_fkey
            FOREIGN KEY (area_id) REFERENCES area(id) ON DELETE RESTRICT,
        CONSTRAINT curriculum_load_subjects_created_by_fkey
            FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE SET NULL,
        CONSTRAINT curriculum_load_subjects_grade_level_id_fkey
            FOREIGN KEY (grade_level_id) REFERENCES grade_levels(id) ON DELETE RESTRICT,
        CONSTRAINT curriculum_load_subjects_school_id_fkey
            FOREIGN KEY (school_id) REFERENCES schools(id) ON DELETE RESTRICT,
        CONSTRAINT curriculum_load_subjects_specialty_id_fkey
            FOREIGN KEY (specialty_id) REFERENCES specialties(id) ON DELETE RESTRICT,
        CONSTRAINT curriculum_load_subjects_subject_id_fkey
            FOREIGN KEY (subject_id) REFERENCES subjects(id) ON DELETE RESTRICT,
        CONSTRAINT curriculum_load_subjects_updated_by_fkey
            FOREIGN KEY (updated_by) REFERENCES users(id) ON DELETE SET NULL
    );

    CREATE INDEX IF NOT EXISTS ix_cls_grade ON curriculum_load_subjects (grade_level_id);
    CREATE INDEX IF NOT EXISTS ix_cls_school_specialty ON curriculum_load_subjects (school_id, specialty_id);
    CREATE INDEX IF NOT EXISTS ix_cls_subject ON curriculum_load_subjects (subject_id);
    CREATE INDEX IF NOT EXISTS "IX_curriculum_load_subjects_area_id" ON curriculum_load_subjects (area_id);
    CREATE INDEX IF NOT EXISTS "IX_curriculum_load_subjects_created_by" ON curriculum_load_subjects (created_by);
    CREATE INDEX IF NOT EXISTS "IX_curriculum_load_subjects_specialty_id" ON curriculum_load_subjects (specialty_id);
    CREATE INDEX IF NOT EXISTS "IX_curriculum_load_subjects_updated_by" ON curriculum_load_subjects (updated_by);
    CREATE UNIQUE INDEX IF NOT EXISTS uq_curriculum_load_subjects_key
        ON curriculum_load_subjects (school_id, specialty_id, grade_level_id, area_id, subject_id);

    CREATE TABLE IF NOT EXISTS curriculum_load_hours (
        id uuid NOT NULL DEFAULT gen_random_uuid(),
        curriculum_load_subject_id uuid NOT NULL,
        curriculum_block_id uuid NOT NULL,
        hours numeric(5,2) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
        updated_at timestamp with time zone NULL,
        created_by uuid NULL,
        updated_by uuid NULL,
        CONSTRAINT curriculum_load_hours_pkey PRIMARY KEY (id),
        CONSTRAINT ck_curriculum_load_hours_non_negative CHECK (hours >= 0),
        CONSTRAINT curriculum_load_hours_block_id_fkey
            FOREIGN KEY (curriculum_block_id) REFERENCES curriculum_blocks(id) ON DELETE RESTRICT,
        CONSTRAINT curriculum_load_hours_created_by_fkey
            FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE SET NULL,
        CONSTRAINT curriculum_load_hours_subject_id_fkey
            FOREIGN KEY (curriculum_load_subject_id) REFERENCES curriculum_load_subjects(id) ON DELETE CASCADE,
        CONSTRAINT curriculum_load_hours_updated_by_fkey
            FOREIGN KEY (updated_by) REFERENCES users(id) ON DELETE SET NULL
    );

    CREATE INDEX IF NOT EXISTS ix_clh_block ON curriculum_load_hours (curriculum_block_id);
    CREATE INDEX IF NOT EXISTS "IX_curriculum_load_hours_created_by" ON curriculum_load_hours (created_by);
    CREATE INDEX IF NOT EXISTS "IX_curriculum_load_hours_updated_by" ON curriculum_load_hours (updated_by);
    CREATE UNIQUE INDEX IF NOT EXISTS uq_curriculum_load_hours_subject_block
        ON curriculum_load_hours (curriculum_load_subject_id, curriculum_block_id);

    INSERT INTO curriculum_blocks (id, code, name, sort_order, created_at)
    VALUES
        (gen_random_uuid(), 'B1', 'Bloque 1', 1, CURRENT_TIMESTAMP),
        (gen_random_uuid(), 'B2', 'Bloque 2', 2, CURRENT_TIMESTAMP)
    ON CONFLICT (code) DO NOTHING;

    SELECT COUNT(*) INTO v_cls FROM curriculum_load_subjects;
    IF v_cls = 0 THEN
        WITH grade_nums AS (
            SELECT gl.id,
                   NULLIF(regexp_replace(gl.name, '\D', '', 'g'), '')::int AS grade_num
            FROM grade_levels gl
        ),
        premedia_specialties AS (
            SELECT DISTINCT sa.specialty_id
            FROM subject_assignments sa
            JOIN grade_nums g ON g.id = sa.grade_level_id
            WHERE g.grade_num IN (7, 8, 9)
              AND sa.specialty_id IS NOT NULL
        )
        INSERT INTO curriculum_load_subjects (
            id, school_id, specialty_id, grade_level_id, area_id, subject_id,
            is_active, created_at, updated_at
        )
        SELECT
            gen_random_uuid(),
            src.school_id,
            src.specialty_id,
            src.grade_level_id,
            src.area_id,
            src.subject_id,
            TRUE,
            CURRENT_TIMESTAMP,
            CURRENT_TIMESTAMP
        FROM (
            SELECT DISTINCT
                sa."SchoolId" AS school_id,
                sa.specialty_id,
                sa.grade_level_id,
                sa.area_id,
                sa.subject_id
            FROM subject_assignments sa
            JOIN grade_nums g ON g.id = sa.grade_level_id
            LEFT JOIN premedia_specialties ps ON ps.specialty_id = sa.specialty_id
            WHERE sa.specialty_id IS NOT NULL
              AND sa."SchoolId" IS NOT NULL
              AND sa.area_id IS NOT NULL
              AND sa.subject_id IS NOT NULL
              AND (
                  (ps.specialty_id IS NOT NULL AND g.grade_num IN (7, 8, 9))
                  OR
                  (ps.specialty_id IS NULL AND g.grade_num IN (10, 11, 12))
              )
        ) src;
    END IF;

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES (v_mig, '9.0.3');
END
$h$;

COMMIT;
