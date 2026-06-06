-- Verificación post-aplicación (Render producción)
SELECT 'table_exists' AS check_type,
       EXISTS (
           SELECT 1 FROM information_schema.tables
           WHERE table_schema = 'public' AND table_name = 'subject_promotion_records'
       ) AS ok;

SELECT 'migration_history' AS check_type, "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20260606110441_AddSubjectPromotionRecords';

SELECT 'row_count' AS check_type, COUNT(*)::bigint AS total
FROM subject_promotion_records;

SELECT indexname
FROM pg_indexes
WHERE schemaname = 'public' AND tablename = 'subject_promotion_records'
ORDER BY indexname;
