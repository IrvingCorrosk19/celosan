# Plan de implementación — Soporte jornada nocturna (Eduplaner SchoolManager)

**Documento rector:** `ANALISIS_SOPORTE_NOCTURNA_EDUPLANER.md`, `ANEXO_TECNICO_NOCTURNA_EDUPLANER.md`  
**Fecha:** 2026-04-18  
**Alcance de esta entrega:** Camino A (riesgos ALTOS) + ajustes medios acotados documentados en auditoría.

---

## 1. Resumen del objetivo

Preparar la aplicación para estudiantes nocturnos con **múltiples matrículas activas**, **varios grupos/niveles**, **materias en varios contextos** y **reportes/horarios coherentes**, sin romper el flujo diurno tradicional.

---

## 2. Lista de hallazgos (tomados de la auditoría)

| ID | Hallazgo | Origen |
|----|----------|--------|
| H1 | `ScheduleService.GetByStudentUserAsync` usa una sola matrícula (`FirstOrDefault`) | Análisis §5.3, Anexo C.5 |
| H2 | Aprobados/Reprobados: grados, grupos y niveles hardcodeados | Análisis §7.3, §10, Anexo C.6–C.8 |
| H3 | `PrepararDatosParaReporteAsync` altera masivamente actividades (3T) y grupos con mapa fijo | Anexo C.8 |
| H4 | `UpdateGroupAndGrade` / `AssignAsync` pueden inactivar todas las matrículas | Análisis §4.2, Anexo C.1, C.11 |
| H5 | `StudentReportService` mezcla contextos (notas/asistencia sin acotar a grupos de matrícula) | Análisis §8.2, Anexo C.9 |
| H6 | `CalcularEstadisticasGrupoAsync` no acotaba notas al `GroupId` del grupo analizado | Hallazgo derivado en implementación |
| H7 | `BulkAssignFromFileAsync`: duplicados sin `ShiftId`; búsqueda de grupo rígida por `Grade` | Análisis §4.4, Anexo C.12 |

---

## 3. Matriz de riesgos (priorizada)

| Riesgo | Nivel | Mitigación en plan |
|--------|-------|---------------------|
| R01 Horario estudiante incompleto | **Alto** | Consolidar todas las matrículas activas en `GetByStudentUserAsync` |
| R02 Reporte aprobados/reprobados excluye nocturna | **Alto** | Niveles dinámicos + ramas `Todos` / `Nocturna` + grados desde BD |
| R03 Pérdida de matrículas al reasignar | **Alto** | Bloqueo API + confirmación UI + `AssignAsync` por defecto aditivo |
| R04 Reporte individual mezcla notas | **Alto** | Filtro por grupos de matrículas activas; encabezado multi-contexto |
| R05 Estadísticas por grupo contaminadas | **Medio** | Filtro `Activity.GroupId == grupoId` en cálculo |
| R06 Regresión diurna (un solo grupo) | **Medio** | Si no hay matrícula activa, filtros por grupo no aplican (comportamiento previo) |
| R07 Cambio de `PrepararDatos` (ya no toca 3T) | **Medio** | Documentado; operación ahora solo sincroniza `groups.grade` |

---

## 4. Orden de implementación (ejecutado / pendiente)

### Fase 3 — Riesgos altos (ejecutado en código)

1. `ScheduleService.cs` — horario multi-matrícula.  
2. `AprobadosReprobadosService.cs` — niveles/grados/grupos dinámicos; `PrepararDatosParaReporteAsync` seguro.  
3. `StudentAssignmentService.cs` + `StudentAssignmentController.cs` + `Views/StudentAssignment/Index.cshtml` — reemplazo controlado.  
4. `StudentReportService.cs` + `GradeDto.cs` — contexto académico.  
5. `IAprobadosReprobadosService`, `AprobadosReprobadosController`, vista `AprobadosReprobados/Index.cshtml`.

### Fase 4 — Expansión controlada (pendiente / siguiente iteración)

- Carnet multi-contexto completo (`StudentIdCardService`).  
- Unificación `Group.Shift` vs `ShiftId`.  
- Índice UNIQUE parcial opcional en `student_assignments` (script comentado en SQL).  
- Endpoint para poblar desplegable de grados desde BD en UI de Aprobados/Reprobados.

### Fase 5 — Documentación

- `IMPLEMENTACION_NOCTURNA_LOG.md`  
- `SQL_CAMBIOS_NOCTURNA.sql` (sin DDL obligatorio en esta fase)

---

## 5. Módulos impactados

| Módulo | Archivos |
|--------|----------|
| Horarios | `ScheduleService.cs`, `IScheduleService.cs` |
| Reportes institucionales | `AprobadosReprobadosService.cs`, `IAprobadosReprobadosService.cs`, `AprobadosReprobadosController.cs`, `Views/AprobadosReprobados/Index.cshtml` |
| Matrícula | `StudentAssignmentService.cs`, `StudentAssignmentController.cs`, `Views/StudentAssignment/Index.cshtml`, `IStudentAssignmentService.cs` |
| Reporte estudiante | `StudentReportService.cs`, `Dtos/GradeDto.cs` |

---

## 6. Cambios en base de datos

**Ningún cambio estructural obligatorio** en esta fase (la auditoría indica que el modelo ya soporta nocturna).  
Script `SQL_CAMBIOS_NOCTURNA.sql` documenta recomendaciones opcionales y trazabilidad.

---

## 7. Cambios backend (resumen)

- Horarios: unión de entradas por todos los `StudentAssignment` activos del año, deduplicación por `ScheduleEntry.Id`.  
- Aprobados/Reprobados: construcción de estadísticas desde grupos reales; filtro de notas por grupo en estadísticas.  
- Matrícula: `AssignAsync` default `replaceExistingActive=false`; carga masiva con `ShiftId` en duplicados y en inserción; API de cambio de grupo con código `MULTI_ENROLLMENT_CONFIRM`.  
- Reporte estudiante: notas, asistencia y SSA pendientes acotados a grupos de matrículas activas; etiqueta de grado compuesta.

---

## 8. Cambios frontend

- Confirmación en dos pasos cuando hay varias matrículas y se intenta reemplazo.  
- Texto de ayuda en reporte Aprobados/Reprobados alineado al nuevo comportamiento.

---

## 9. Riesgos de regresión

- Secretaría que dependía de **reemplazo total implícito** en `AssignAsync` debe pasar `replaceExistingActive: true` explícitamente (no hay callers en código actual fuera del servicio).  
- `PrepararDatosParaReporte` ya **no** fuerza actividades a 3T: si algún proceso dependía de ello, debe corregirse en configuración de trimestres/actividades.  
- Estadísticas por grupo ahora exigen `Activity.GroupId` coincidente: actividades sin grupo no cuentan en ese grupo (comportamiento más estricto y correcto).

---

## 10. Estrategia de pruebas

| Escenario | Cómo verificar |
|-----------|----------------|
| 1 matrícula nocturna | Horario y reporte estudiante iguales a antes |
| 2+ matrículas | Horario muestra bloques de ambos grupos (sin duplicar `Id`); reporte estudiante solo notas de esos grupos |
| Reasignación desde modal | Primera respuesta `MULTI_ENROLLMENT_CONFIRM`; segunda con confirmación persiste |
| Aprobados/Reprobados | Nivel `Todos` o `Nocturna` incluye ESP7/ESP8/etc. |
| Carga masiva Excel | Mismo grado/grupo en dos jornadas no marca duplicado erróneo si `ShiftId` difiere |

**Evidencia DB:** `psql` contra `schoolmanager_daqf` — consulta `shifts` (3 filas: Mañana, Tarde, Noche), 2026-04-18.

---

## 11. Checklist

- [x] Horarios multi-matrícula  
- [x] Aprobados/Reprobados sin mapas fijos de grupos para preparación de datos  
- [x] Protección multi-matrícula en UI/API  
- [x] Reporte estudiante por contexto de grupos activos  
- [x] `dotnet build` sin errores  
- [x] Conexión PostgreSQL verificada  
- [ ] Pruebas manuales en UI (secretaría / estudiante / director)  
- [ ] Índice UNIQUE opcional en `student_assignments` (evaluar con datos reales)

---

*Fin del plan de implementación.*
