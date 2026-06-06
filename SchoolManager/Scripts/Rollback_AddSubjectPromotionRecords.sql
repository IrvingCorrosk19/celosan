-- Rollback Fase D: eliminar tabla subject_promotion_records
-- Ejecutar solo si no hay datos críticos o tras respaldo validado.

DROP TABLE IF EXISTS subject_promotion_records CASCADE;
