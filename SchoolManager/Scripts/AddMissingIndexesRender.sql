-- =============================================================================
-- Índices faltantes en RENDER (homologar con LOCAL)
-- Ejecutar en la base de datos de Render (schoolmanager_daqf).
-- Usar CONCURRENTLY para no bloquear escrituras durante la creación.
--
-- IMPORTANTE: Cada CREATE INDEX CONCURRENTLY debe ejecutarse fuera de una
-- transacción. En pgAdmin o psql, ejecutar uno por uno o en sesiones separadas.
-- En algunos clientes, ejecutar todo el archivo puede fallar; en ese caso
-- ejecutar cada sentencia por separado.
-- =============================================================================

-- 1. groups.shift_id (FK a shifts)
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_groups_shift_id ON groups(shift_id);

-- 2. payment_concepts (auditoría)
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_payment_concepts_created_by ON payment_concepts(created_by);
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_payment_concepts_updated_by ON payment_concepts(updated_by);

-- 3. payments (FKs)
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_payments_payment_concept_id ON payments(payment_concept_id);
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_payments_student_id ON payments(student_id);

-- 4. shifts.name (búsquedas por nombre)
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_shifts_name ON shifts(name);

-- 5. student_assignments.shift_id (FK a shifts)
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_student_assignments_shift_id ON student_assignments(shift_id);
