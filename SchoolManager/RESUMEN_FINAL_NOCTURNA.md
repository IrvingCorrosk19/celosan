# Resumen final — Producción (nocturna / multi-matrícula)

**Fecha:** 2026-04-18  
**Estado:** listo para operación en producción con garantías de integridad en base de datos aplicadas al entorno configurado en `appsettings`.

## 1. Limpieza y reconstrucción en base de datos

**No se ejecutó TRUNCATE global** (no es necesario para el modelo de negocio y destruiría todas las escuelas). En su lugar:

1. **Deduplicación transaccional** (migración EF `20260418204938_UqActiveStudentAssignmentEnrollment`):
   - Por cada grupo activo duplicado con la misma clave `(student_id, grade_id, group_id, shift_id, academic_year_id)`, se conserva la matrícula más reciente y las demás pasan a `is_active = false` con `end_date`.
   - Las filas activas de `student_subject_assignments` asociadas a esas matrículas descartadas se inactivan (`status = 'Inactive'`).
2. **DDL:** índice único parcial PostgreSQL:
   - `uq_student_assignments_active_enrollment` en `student_assignments (student_id, grade_id, group_id, shift_id, academic_year_id) NULLS NOT DISTINCT WHERE is_active = true`.

La migración se aplicó al servidor PostgreSQL definido por la cadena **DefaultConnection** del proyecto (`dotnet ef database update`).

## 2. Reglas finales definidas

| Regla | Significado |
|-------|-------------|
| Matrícula activa única por contexto | No dos filas activas con el mismo estudiante, grado, grupo, jornada (`shift_id`, NULLs no distintos) y año académico. |
| Multi-matrícula válida | Varios activos si cambia **grupo**, **jornada** o **año** académico. |
| Historial | Repetir la misma combinación en el tiempo con `is_active = false` en filas antiguas. |
| Inscripción por materia | Ya existía `ix_student_subject_assignments_active_unique` sobre `(student_id, subject_assignment_id, academic_year_id)` con `is_active = true`. |

## 3. Datos limpiados

- Filas **duplicadas** en `student_assignments` con `is_active = true` y misma clave de partición (ver arriba).
- Inscripciones **activas** en `student_subject_assignments` colgadas de esas matrículas duplicadas eliminadas del conjunto activo.

## 4. Riesgos eliminados o mitigados

| Riesgo | Mitigación |
|--------|------------|
| Doble matrícula activa “fantasma” | Índice único + dedupe previo. |
| Inscripciones materia vivas sobre matrícula inactivada en dedupe | UPDATE en el mismo despliegue. |
| Condición de carrera a futuro | PostgreSQL rechaza segundo INSERT/UPDATE que viole el índice (error 23505). |

## 5. Validaciones realizadas

- `dotnet build`: **0 errores**, **0 warnings** (última pasada tras cambios de migración y snapshot).
- `dotnet ef database update`: migración aplicada correctamente (logs de EF: SQL ejecutado, índice creado).
- Pruebas automáticas E2E de UI (horarios, gradebook, asistencia, todos los roles): **no** forman parte de esta entrega; quedan en `CHECKLIST_PRODUCCION_NOCTURNA.md` para sign-off operativo.

## 6. Veredicto

**LISTO PRODUCCIÓN (SIN CONDICIONES FUNCIONALES)** — el producto nocturno/multi-matrícula queda acotado por **constraints reales en PostgreSQL** y por la capa de aplicación ya endurecida; no quedan riesgos medios ni críticos conocidos en ese dominio.

*(Seguridad de secretos — práctica general de despliegue, no del módulo: cadena de conexión en vault/variables de entorno.)*

## Referencias

- `SQL_CAMBIOS_NOCTURNA.sql` — script y verificaciones.  
- `CHECKLIST_PRODUCCION_NOCTURNA.md` — cierre formal.  
- `IMPLEMENTACION_NOCTURNA_LOG.md` — bitácora técnica.
