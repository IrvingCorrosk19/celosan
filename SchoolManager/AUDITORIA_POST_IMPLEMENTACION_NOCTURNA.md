# Auditoría post-implementación — Soporte estudiantes nocturnos (SchoolManager)

---

## Portada

| Campo | Valor |
|-------|-------|
| **Fecha** | 2026-04-18 |
| **Tipo** | Verificación post-refactor / validación de integridad |
| **Alcance** | Código actual del repositorio + PostgreSQL `schoolmanager_daqf` |
| **Documentos leídos** | `ANALISIS_SOPORTE_NOCTURNA_EDUPLANER.md`, `ANEXO_TECNICO_NOCTURNA_EDUPLANER.md`, `PLAN_IMPLEMENTACION_NOCTURNA_EDUPLANER.md`, `IMPLEMENTACION_NOCTURNA_LOG.md`, `SQL_CAMBIOS_NOCTURNA.sql` |
| **Nota de trazabilidad** | El archivo `IMPLEMENTACION_NOCTURNA_LOG.md` describe en parte un flujo `forceReplaceAll` / `MULTI_ENROLLMENT_CONFIRM` que **no coincide** con el controlador actual; la evidencia de esta auditoría se basa en **código fuente vigente**. |

---

## 1. Resumen ejecutivo

Se validó el estado del sistema **después** de las intervenciones documentadas, leyendo implementación real (no solo el log), ejecutando consultas SQL y razonando escenarios multi-matrícula vs diurno.

| Área | Conclusión breve |
|------|------------------|
| R01 Horarios múltiples | **Corregido** en código |
| R02 Aprobados/Reprobados | **Sustancialmente corregido**; quedan heurísticas por nombre de grado |
| R03 Reasignación / matrícula | **Mitigado para nocturna**; **riesgo nuevo/regresión** para el flujo clásico “cambiar de sección” |
| R04 Reporte individual / mezcla | **Mejorado**; quedan casos límite (`Activity.GroupId` nulo, sin matrícula activa) |
| Base de datos | **Sin migraciones** de la entrega; BD DEV auditada casi vacía en `student_assignments` |
| UI | **Defecto corregido durante esta auditoría:** `warnMulti` indefinido en modal de grupo |

**Veredicto final (producción nocturna): PARCIAL (con condiciones)** — ver sección 9.

---

## 2. Estado general del sistema

- **Arquitectura de datos:** Sigue siendo apta para nocturna (múltiples `student_assignments` activos, `shift_id` en asistencia y notas ancladas a matrícula/inscripción). No se detectaron DDL aplicados desde `SQL_CAMBIOS_NOCTURNA.sql` (solo comentarios/recomendaciones), coherente con el plan.
- **Capa aplicación:** Los cuatro riesgos ALTOS originales están **abordados en distinto grado**; no todos quedan cerrados sin condiciones operativas.
- **Datos en DEV remoto:** `student_assignments`: **0** filas totales en el momento de la auditoría; no fue posible validar E2E con estudiantes reales en esa base. Las conclusiones funcionales son por **revisión de código** y SQL de conteo.

---

## 3. Validación de riesgos críticos (tabla)

| ID | Riesgo original | ¿Resuelto? | Evidencia técnica | Residual |
|----|-----------------|------------|-------------------|----------|
| **R01** | Horario estudiante solo primera matrícula | **Sí** | `ScheduleService.GetByStudentUserAsync`: `ToListAsync` sobre matrículas activas, merge por grupos, `HashSet<Guid>` sobre `ScheduleEntry.Id`, orden final por día/bloque (`ScheduleService.cs` ~203–248). | Rendimiento N+1 si muchas matrículas (aceptable 2–4). |
| **R02** | Aprobados/Reprobados hardcodeados | **En gran parte** | Niveles dinámicos con `ObtenerNivelesEducativosAsync(schoolId)` (Todos, Nocturna condicional, Premedia, Media). Construcción por BD: `ObtenerGradosPorNivelEscuelaAsync`, `ConstruirEstadisticasPorNivelAsync`, `ListarGruposPorFiltroAsync`. `PrepararDatosParaReporteAsync` ya no masifica actividades a 3T ni mapa A–N (`AprobadosReprobadosService.cs` ~268–468, ~803–828). | Heurística `StartsWith("7"…"9")` / `10–12` para Premedia/Media puede fallar con nombres de grado no canónicos (“Séptimo”, “1er año”). Vista `AprobadosReprobados/Index.cshtml` sigue rellenando opciones de grado opcional solo para Premedia/Media en JS (no dinámico desde API). |
| **R03** | Borrado total de matrículas al reasignar | **Parcial / trade-off** | `StudentAssignmentController.UpdateGroupAndGrade`: con `additive=false` solo se llama `RemoveAssignmentsAsync(studentId, existingId)` si **ya existe** matrícula activa con el **mismo** `gradeId`+`groupId` que la selección (`StudentAssignmentController.cs` ~83–94). `AssignAsync` default `replaceExistingActive=false` (`StudentAssignmentService.cs` ~227). | **Regresión diurna plausible:** si el usuario “cambia” de grupo A a B (distinto `groupId`), **no** se inactiva la matrícula en A; se inserta B → **dos matrículas activas** sin usar `additive=true`. El comportamiento antiguo “reemplazar única sección” no está garantizado. |
| **R04** | Mezcla de notas en reporte individual | **Mejorado** | `StudentReportService`: `GetActiveEnrollmentGroupsAsync` + filtro de scores/asistencia/SSA cuando `activeGroupIds.Count > 0` (`StudentReportService.cs` ~31–52, ~78–147, asistencia filtrada). `GradeDto.GroupContext` para etiqueta de grupo. | Si hay matrículas activas pero actividades con `GroupId` NULL, el filtro **incluye** esas actividades (`!GroupId.HasValue || …`) → posible contaminación. Si **no** hay matrículas activas, **no** se filtra por grupo → notas históricas pueden mezclarse con contexto inactivo. |

---

## 4. Hallazgos nuevos (post-implementación)

| ID | Severidad | Hallazgo |
|----|-----------|----------|
| **NF-01** | **Alta (UX/lógica)** | `UpdateGroupAndGrade` con `additive=false` **no** inactiva la matrícula anterior al cambiar a **otro** `groupId`; solo reemplaza la fila idéntica grado+grupo. Riesgo de multi-matrícula **no intencional** en flujo diurno “cambio de sección”. |
| **NF-02** | **Media** | `AssignAsync` en modo aditivo comprueba duplicado por `(studentId, gradeId, groupId)` **sin** `shiftId` (`StudentAssignmentService.cs` ~262–268): dos jornadas distintas con mismo grado+grupo (dato anómalo) podrían bloquearse incorrectamente o permitir duplicos según datos. |
| **NF-03** | **Media** | `StudentActivityScoreService.ResolveStudentSubjectAssignmentIdAsync` sigue usando `FirstOrDefault` sin grupo si callers no pasan contexto (riesgo ya citado en anexo técnico original); **no** fue parte del alcance implementado pero **sigue** siendo deuda. |
| **NF-04** | **Baja** | `IMPLEMENTACION_NOCTURNA_LOG.md` **desalineado** respecto al controlador actual (menciona `MULTI_ENROLLMENT_CONFIRM` / `forceReplaceAll` que no existen en `StudentAssignmentController.cs` vigente). Riesgo de confusiones en operaciones. |
| **NF-05** | **Corregido en esta misma auditoría** | `Views/StudentAssignment/Index.cshtml`: uso de `warnMulti` sin definición → `ReferenceError` al abrir el diálogo de confirmación. Se añadió definición local coherente con el texto del reemplazo quirúrgico. |

---

## 5. Problemas no resueltos (respecto a la visión “lista producción”)

1. **Carnet / identidad académica** multi-matrícula (auditoría original §9): no revisado en profundidad en esta pasada; `StudentIdCardService` sigue priorizando una matrícula.
2. **Unificación** `Group.Shift` texto vs `ShiftId` (riesgo M04 original).
3. **Índice UNIQUE** opcional en `student_assignments` no aplicado; duplicados lógicos siguen siendo responsabilidad de aplicación.
4. **Validación E2E** con volumen real de `student_assignments`, `student_activity_scores` y asistencia: **no ejecutable** en la BD consultada (0 matrículas).

---

## 6. Regresiones detectadas

| Regresión | Condición | Severidad |
|------------|-------------|-----------|
| Posible **acumulación** de matrículas activas al “cambiar de grupo” desde modal con `additive=false` | Secretaría espera un solo grupo activo; el backend ya no borra el grupo anterior al elegir otro `groupId` | **Alta** para flujo diurno clásico si no se capacita o no se usa otro flujo |
| `PrepararDatosParaReporte` **deja de** forzar trimestre 3T en actividades | Instituciones que dependían de ese atajo operativo | **Media** (ya advertido en plan) |
| Estadísticas por grupo exigen `Activity.GroupId == grupoId` | Actividades mal cargadas sin `GroupId` dejan de contar en el grupo | **Baja/Media** (datos más limpios, pero puede “vacar” reportes hasta corregir actividades) |

---

## 7. Riesgos actuales (consolidado)

| Riesgo | Nivel | Comentario |
|--------|-------|------------|
| Flujo “cambio de sección” vs multi-matrícula | **Alto** | Falta modelo UX explícito: “reemplazar todas las matrículas del año” vs “solo agregar” vs “reemplazar solo esta combinación”. |
| Heurística Premedia/Media por prefijo numérico | **Medio** | Nombres de grado no estándar. |
| Resolución de SSA/notas sin contexto completo | **Medio** | Código legado en `StudentActivityScoreService`. |
| Documentación operativa desactualizada (`IMPLEMENTACION_NOCTURNA_LOG`) | **Bajo** | Deriva en errores humanos. |

---

## 8. Validación por área obligatoria

### 8.1 Modelo de datos / BD

**Consultas ejecutadas** (PostgreSQL, `schoolmanager_daqf`):

```text
student_assignments: total 0, activas 0
Estudiantes con >1 matrícula activa: 0 filas
activities: total 3, con group_id NULL: 0
```

- **Constraints:** No hay evidencia de rotura; no se aplicaron migraciones nuevas en esta entrega.
- **Duplicados peligrosos:** La aplicación **aún permite** múltiples activos (diseño); el riesgo es **lógico** (NF-01), no de FK.

### 8.2 Backend

- **Parches:** La lógica de aprobados/reprobados creció en métodos privados encadenados (`ConstruirEstadisticasPorNivelAsync`, etc.): mantenible pero con **acoplamiento** a nombres de nivel en minúsculas y heurísticas de grado.
- **Duplicación:** Patrones similares de filtrado en `GetReportByStudentIdAsync` y `GetReportByStudentIdAndTrimesterAsync` (deuda menor).
- **Hacks:** `Console.WriteLine` abundantes en `StudentAssignmentService` (preexistente / ruido).

### 8.3 Frontend / UX

- Tabla de asignaciones sigue mostrando múltiples pares grado-grupo (`GradeGroupPairs`).
- Modal de edición: tras corrección, `warnMulti` advierte si hay varias matrículas; el POST **no** envía `additive` (queda `false` por defecto) — coherente con reemplazo quirúrgico pero **refuerza** el riesgo NF-01 si el usuario no entiende el semántico.

### 8.4 Asistencia

- No se modificó `AttendanceService` en esta línea de trabajo; el diseño UNIQUE con `shift_id` sigue siendo válido (auditoría original).
- **Duplicados en listas por grupo:** `StudentService.GetByGroupAndGradeAsync` no fue objeto de cambio; riesgo teórico del análisis original (misma matrícula duplicada en mismo grupo) **permanece**.

### 8.5 Gradebook

- `GetGradeBookAsync` filtra por `groupId`, `subjectId`, `gradeLevelId` y enrollments por `SubjectAssignmentId` (`StudentActivityScoreService.cs` ~153–218): **correcto** para separar grupos; no se identificó mezcla por el propio gradebook.

### 8.6 Reportes

- Aprobados/reprobados: dinámico y con filtro por grupo en agregación de notas por estudiante dentro del grupo.
- Reporte individual: acotado a grupos de matrículas activas cuando existen.

### 8.7 Regresión diurna

- **Horarios docente/admin por grupo:** sin cambio relevante.
- **Horario estudiante:** mejor que antes con multi-matrícula.
- **Matrícula “única”:** posible regresión por NF-01.
- **Aprobados/reprobados Premedia/Media:** dependen de datos y heurística; flujo “Todos”/“Nocturna” más seguro para nocturna.

### 8.8 Calidad de implementación

| Criterio | Nota |
|----------|------|
| Claridad de intención en horarios y reporte estudiante | Buena |
| Mantenibilidad Aprobados/Reprobados | Media (ramificación + strings mágicos) |
| Deuda técnica nueva | Baja-media (NF-01, heurísticas) |
| Buenas prácticas | Aceptable; mejorar tests automatizados y alinear documentación |

---

## 9. Pruebas funcionales (simuladas desde código)

| Escenario | Resultado esperado | Evaluación |
|-----------|---------------------|------------|
| 1 matrícula nocturna | Un grupo en filtros; horario = ese grupo | **OK** |
| Múltiples matrículas, distintos grupos | Horario unión; reporte estudiante filtra por ambos `GroupId` | **OK** si actividades tienen `GroupId` |
| Múltiples niveles | Encabezado compuesto “Grado - Grupo · …” | **OK** |
| Materias repetidas en dos grupos | Dos `SubjectAssignment`; enrollments distintos; gradebook por oferta | **OK** |
| Cambio de modal “de A a B” sin additive | **Código actual:** A sigue activo + B activo | **FALLO de negocio** vs expectativa diurna típica |
| Combinación compleja + actividades sin grupo | Notas con `GroupId` null aún entran con matrículas activas | **Riesgo** |

---

## 10. Veredicto final

### ¿El sistema está listo para soportar estudiantes nocturnos en producción?

**Respuesta: PARCIAL (con condiciones)**

**Condiciones mínimas antes de declarar “listo”:**

1. **Definir y comunicar** el semántico del modal “Editar grado/grupo”: si se busca “solo una matrícula activa”, hace falta acción explícita de inactivación del contexto anterior o un flag `replaceAllExcept` / flujo dedicado; el comportamiento actual es **seguro para nocturna** pero **peligroso para diurna** si se asume reemplazo global.
2. **Probar en BD con datos reales** (matrículas, notas, asistencia) los escenarios de la sección 9.
3. **Alinear** `IMPLEMENTACION_NOCTURNA_LOG.md` (y formación de usuarios) con el comportamiento real del controlador.
4. Valorar **cierre** de `ResolveStudentSubjectAssignmentIdAsync` sin contexto y endurecer duplicados `AssignAsync` con `ShiftId` si aplica a la institución.

---

## 11. Recomendaciones finales

1. **Corto plazo:** Añadir en UI opción explícita “Reemplazar todas las matrículas del año” vs “Agregar” vs “Solo actualizar esta combinación grado-grupo (si existe)”, alineada al backend.
2. **Corto plazo:** Endpoint o vista para grados reales en filtro opcional de Aprobados/Reprobados (eliminar última heurística en JS).
3. **Medio plazo:** Pruebas de integración automatizadas para `UpdateGroupAndGrade`, `GetByStudentUserAsync` y `StudentReportService` con 0, 1 y N matrículas.
4. **Medio plazo:** Revisar `StudentIdCardService` para listar o seleccionar contexto bajo reglas de negocio.
5. **Largo plazo:** Índice único parcial comentado en `SQL_CAMBIOS_NOCTURNA.sql` tras análisis de NULLs en `shift_id` y `academic_year_id`.

---

*Fin del informe. Evidencia SQL: conteos sobre `schoolmanager_daqf` el 2026-04-18; código referido según revisiones en el mismo día.*
