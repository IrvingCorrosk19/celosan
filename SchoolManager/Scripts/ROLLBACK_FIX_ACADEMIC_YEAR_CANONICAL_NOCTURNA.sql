-- Rollback Fase 2: reactivar años académicos 2026 duplicados de CELOSAM.
-- No restaura updated_at/description previos; para restauración exacta usar backup/snapshot.
-- Seguro: no elimina registros ni modifica matrículas.

BEGIN;

UPDATE academic_years
SET is_active = true,
    updated_at = CURRENT_TIMESTAMP
WHERE school_id = '6e42399f-6f17-4585-b92e-fa4fff02cb65'
  AND name = '2026'
  AND id IN (
      'e3e6c927-a568-49b5-af56-e269dcc11a83',
      'e1c4adc1-bf9b-4739-8094-6c2cc8312920',
      '52b171d4-edd5-4b6d-a814-d7555e0dbfca',
      '5c23c44c-0c99-4e3a-ab52-d4162e3964bb',
      '3629e8ea-584c-4424-b1ca-9c318a899457',
      '11ee4860-59e8-4be2-8e39-b52d67e6f938',
      '8b599fdf-ebe4-4524-a01e-de29931e040c',
      '7a05e3c2-3d97-4fc0-a430-e66998e97417',
      '13eeea1d-291d-4eee-8d6a-d89c95379ede',
      '160c30f0-1d9f-4a36-a3ee-3dc69116b0ee',
      '3159b2e9-a7ba-4dc1-9a20-7c4378da5869',
      '4c2b37dc-56ac-4acd-b4ce-09cea8b6003c'
  );

SELECT id, name, is_active, start_date, end_date, created_at, updated_at
FROM academic_years
WHERE school_id = '6e42399f-6f17-4585-b92e-fa4fff02cb65'
ORDER BY created_at ASC;

COMMIT;
