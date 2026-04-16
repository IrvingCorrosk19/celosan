INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260416105628_SyncNocturnalSchemaSnapshot', '8.0.4'
WHERE NOT EXISTS (
    SELECT 1
    FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260416105628_SyncNocturnalSchemaSnapshot'
);
