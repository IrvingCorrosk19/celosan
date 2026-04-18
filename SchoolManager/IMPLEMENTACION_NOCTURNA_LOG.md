# Log de implementación — Jornada nocturna

**Auditoría de referencia:** `ANALISIS_SOPORTE_NOCTURNA_EDUPLANER.md`, `ANEXO_TECNICO_NOCTURNA_EDUPLANER.md`  
**Fecha:** 2026-04-18

| Fecha | Archivo | Problema (auditoría) | Solución | Impacto | Riesgo | Pruebas |
|-------|---------|----------------------|----------|---------|--------|---------|
| 2026-04-18 | `Services/Implementations/ScheduleService.cs` | R01: solo primera matrícula para horario estudiante | Se cargan todas las matrículas activas del año, se obtiene horario por cada `GroupId`, se unen y deduplican por `ScheduleEntry.Id`, orden día/bloque | Estudiantes con varios grupos ven agenda completa | Bajo: mismo modelo de datos | `dotnet build` OK |
| 2026-04-18 | `Services/Interfaces/IScheduleService.cs` | Documentación desactualizada | Comentario XML actualizado a multi-matrícula | Documentación | Bajo | — |
| 2026-04-18 | `Services/Implementations/AprobadosReprobadosService.cs` | R02/R03: hardcodes; H6 | `ConstruirEstadisticasPorNivelAsync`: niveles `Todos`/`Nocturna`/`Premedia`/`Media` con datos de BD; `CalcularEstadisticasGrupoAsync` filtra notas por `Activity.GroupId == grupoId`; `PrepararDatosParaReporteAsync` solo rellena `groups.grade` desde `subject_assignments` (sin tocar actividades ni mapa A–N) | Nocturna incluida; estadísticas por grupo más fieles | Medio: quien dependía del masivo 3T debe ajustar trimestres en datos | `dotnet build` OK |
| 2026-04-18 | `Services/Interfaces/IAprobadosReprobadosService.cs` | Niveles sin escuela | `ObtenerNivelesEducativosAsync(Guid schoolId)` | Selector con `Todos` y `Nocturna` si aplica | Bajo | — |
| 2026-04-18 | `Controllers/AprobadosReprobadosController.cs` | Firma niveles | Pasa `SchoolId` al servicio | Coherencia multi-tenant | Bajo | — |
| 2026-04-18 | `Views/AprobadosReprobados/Index.cshtml` | Texto / UX desalineados | Ayuda actualizada; comentario JS para Todos/Nocturna | UX | Bajo | Manual UI |
| 2026-04-18 | `Controllers/StudentAssignmentController.cs` | R04: borrado total implícito | Si `!additive` y >1 matrícula activa y `!forceReplaceAll` → JSON `MULTI_ENROLLMENT_CONFIRM` | Evita pérdida accidental | Bajo: un clic extra | Manual UI |
| 2026-04-18 | `Views/StudentAssignment/Index.cshtml` | Sin confirmación | `data-active-enrollment-count`; flujo `postUpdate` con segundo diálogo y `forceReplaceAll` | Trazabilidad usuario | Bajo | Manual UI |
| 2026-04-18 | `Services/Implementations/StudentAssignmentService.cs` | H7; R03 default | `AssignAsync` default `replaceExistingActive=false`; bulk: `ShiftId` en anti-duplicado y en insert; resolución de grupo por `SchoolId` y `Grade` opcional | Menos colisiones entre jornadas; modo aditivo por defecto | Medio: integraciones que asumían replace=true | `dotnet build` OK |
| 2026-04-18 | `Services/Interfaces/IStudentAssignmentService.cs` | Firma `AssignAsync` | Default false documentado | API | Medio | — |
| 2026-04-18 | `Services/Implementations/StudentReportService.cs` | R05: mezcla de contextos | `GetActiveEnrollmentGroupsAsync`; filtros a notas/asistencia/SSA; encabezado multi-matrícula; pendientes con `sa.IsActive` | Reporte acotado a matrículas activas | Medio: actividades sin `GroupId` quedan fuera si hay matrículas | `dotnet build` OK |
| 2026-04-18 | `Dtos/GradeDto.cs` | Sin visibilidad de grupo | `GroupContext` opcional | Mejor lectura en UI/API | Bajo | — |

## Hallazgo adicional (implementación)

- **H6 (derivado):** Las estadísticas de aprobados/reprobados por grupo podían incluir calificaciones de actividades de otros grupos del mismo estudiante. Se corrigió filtrando por `Activity.GroupId`.

## Pendiente explícito (fuera de alcance / largo plazo)

- Refactor formal `AcademicEnrollment` (Camino C de la auditoría).  
- Índice UNIQUE opcional en `student_assignments` (evaluar con datos reales; ver `SQL_CAMBIOS_NOCTURNA.sql`).

## Evidencia

- Compilación: `dotnet build` en `SchoolManager` — 0 errores / 0 warnings, 2026-04-18 (pasada final).  
- PostgreSQL: `psql` a `schoolmanager_daqf` — `SELECT name FROM shifts` → Mañana, Tarde, Noche.

## Cierre final (2026-04-18) — alineación código ↔ auditorías

| Archivo | Cambio |
|---------|--------|
| `Controllers/StudentAssignmentController.cs` | `UpdateGroupAndGrade`: `forceReplaceAll`; si `!additive` y varias matrículas activas → `MULTI_ENROLLMENT_CONFIRM`; reemplazo simple inactiva todas las activas y crea la nueva (corrige NF-01 / R03). |
| `Views/StudentAssignment/Index.cshtml` | Segundo diálogo y `forceReplaceAll`; texto de advertencia alineado al comportamiento real. |
| `Services/Implementations/StudentAssignmentService.cs` | `AssignAsync` default `replaceExistingActive=false`; duplicados por `ShiftId`; nuevas filas con `ShiftId`; `BulkAssignFromFileAsync` por escuela + `ShiftId`; limpieza de `Console.WriteLine`; `AssignStudentAsync` usa `ExistsWithShiftAsync` y `ShiftId`. |
| `Services/Implementations/StudentReportService.cs` | Con matrículas activas, notas solo de actividades con `GroupId` en matrículas activas (sin mezclar actividades sin grupo); eliminación de `Console.WriteLine`. |
| `Services/Implementations/StudentActivityScoreService.cs` | `ResolveStudentSubjectAssignmentIdAsync` exige `subjectId` y `activityGroupId` para no resolver mal en multi-matrícula. |
| `Services/Implementations/StudentService.cs` | `GetByGroupAndGradeAsync`: una fila por `StudentId` (anti-duplicados listas). |
| `AprobadosReprobadosService` + interfaz + controlador + vista | Grados para filtro desde BD (`Nocturna` por grupos con turno noche); endpoint `ObtenerGradosFiltro`. |
| `Helpers/ActiveStudentAssignmentHelper.cs` + carnet | `BuildMultiEnrollmentSummary`; DTO y vista muestran contexto primario + otros contextos. |
| `SQL_CAMBIOS_NOCTURNA.sql` | Sección de cierre y consultas DML sugeridas (solo comentadas). |

**Documento solicitado no presente en el repositorio:** `AUDITORIA_CURSOR_POST_NOCTURNA.md` (no se localizó bajo `EduplanerNoche`).
