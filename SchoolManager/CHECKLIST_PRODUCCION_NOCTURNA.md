# Checklist — Producción (nocturna / multi-matrícula)

**Versión:** 2026-04-18  
**Uso:** marcar ítems antes de declarar corte a producción.

## Base de datos

- [ ] Migración `20260418204938_UqActiveStudentAssignmentEnrollment` aplicada en el entorno objetivo (`dotnet ef database update` o pipeline CI/CD).
- [ ] Consulta de verificación sin duplicados activos (ver `SQL_CAMBIOS_NOCTURNA.sql`, sección verificación).
- [ ] Índice `uq_student_assignments_active_enrollment` presente (`\d student_assignments` en psql).
- [ ] Respaldo completo (dump lógico) tomado **antes** de la primera aplicación en producción.
- [ ] Cadena de conexión fuera del control de versiones en producción (secret manager / variables de entorno).

## Aplicación

- [ ] `dotnet build` sin errores; warnings revisados si aplica política de equipo.
- [ ] Flujo secretaría: cambio de grupo con **una** matrícula activa (diurna clásica).
- [ ] Flujo secretaría: estudiante con **dos** matrículas activas → intento de reemplazo → `MULTI_ENROLLMENT_CONFIRM` → confirmación → una sola activa.
- [ ] Flujo **aditivo** (`additive=true` o `AddEnrollment`): no borra otras matrículas; respeta duplicado por turno (`ExistsWithShiftAsync`).
- [ ] Horario estudiante con dos grupos: agenda unificada sin pérdida de bloques.
- [ ] Reporte estudiantil: notas y asistencia acotadas a grupos de matrícula activa.
- [ ] Aprobados/reprobados: nivel Nocturna + filtro de grado desde API.
- [ ] Carnet: muestra contexto primario y línea de contextos adicionales si hay más de uno.

## Regresión diurna

- [ ] Matrícula única tradicional (un grado/un grupo) sin errores.
- [ ] Asistencia y gradebook en grupo diurno.
- [ ] Reportes existentes nocturnos + diurnos con datos reales de una escuela piloto.

## Post-deploy

- [ ] Monitoreo de errores 48 h (duplicados de matrícula deberían aparecer como `23505` único si algo bypass la app).
- [ ] Documentación interna a secretaría: significado de “reemplazar todas las matrículas”.
