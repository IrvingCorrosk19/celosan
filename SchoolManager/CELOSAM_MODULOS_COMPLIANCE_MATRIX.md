# Matriz de cumplimiento - `CelosanModulos.txt`

Fecha: 2026-06-22  
Rama: `fix/nocturna-matricula-prematricula-completa`

## Resumen

El sistema ya cubre el flujo central de prematrícula nocturna modular: período activo, malla curricular, niveles, materias, cupos, selección, eliminación, finalización y comprobante PDF.

Para declarar 100% operativo en producción faltan datos académicos/operativos reales que no deben inventarse:

- horarios publicados (`schedule_entries` está vacío);
- docentes asignados para todos los grupos/materias;
- prerrequisitos oficiales por materia/nivel;
- carga real de créditos/convalidaciones de estudiantes;
- documentos de identidad de estudiantes.

El código y las pantallas para esas piezas existen y quedan expuestas en menú para admin/secretaría/director según corresponda. Si no hay datos reales, el sistema muestra estados vacíos o bloqueados hasta que se carguen.

## Cumplimiento por sección

| # | Requisito | Estado | Evidencia / Pendiente |
|---:|---|---|---|
| 1 | Objetivo: seleccionar asignaturas según plan, historial, convalidaciones, cupos y horarios | Parcial alto | Plan/cupos/convalidaciones soportados. Horarios y prerequisitos requieren carga real. |
| 2 | Roles involucrados | Cumplido operativo | Estudiante, admin/secretaría, profesor y director tienen flujos y accesos visibles. |
| 3 | Configuración período | Cumplido | `prematriculation_periods` activo creado; UI `PrematriculationPeriod`. |
| 4 | Plan de estudio | Cumplido técnico | `curriculum_track` y 285 `curriculum_subjects` creados desde `subject_assignments`. Prerrequisitos oficiales pendientes de cargar. |
| 5 | Regla aprobación >= 3.0 | Cumplido | `MinimumPassingScore`, créditos válidos y promoción usan 3.0. |
| 6 | Estudiantes nuevos y convalidación masiva | Parcial | `Celosan/BulkCredits` y `Equivalencies` existen. Falta cargar datos reales y probar flujo UI completo. |
| 7 | Estudiantes activos con historial acumulado | Parcial | Créditos, reprobadas, retiradas y convalidadas están modeladas; falta historial real completo. |
| 8 | Materias disponibles según reglas | Parcial alto | Se muestran materias por malla, créditos, reprobadas/retiradas y prerequisitos. Falta sembrar prerequisitos oficiales. |
| 9 | Selección de materias, cupos, horarios, choques | Cumplido operativo | Las materias se muestran; si faltan horarios quedan bloqueadas hasta publicar `schedule_entries`, evitando choques no verificables. |
| 10 | Límite de materias | Cumplido | `MaxSubjectsAllowed` en período y validación en `SelectSubjectAsync`/`FinalizeAsync`. |
| 11 | Horarios, grupos y cupos | Cumplido operativo | Pantalla de horarios editable por admin/director/profesor y cupos por grupo. Si no hay horarios/docentes reales, queda pendiente de carga operativa. |
| 12 | Asignación automática de grupos | Cumplido técnico | `ResolveBestSubjectAssignmentAsync` asigna por cupo y conflictos de horario si hay horarios. |
| 13 | Finalizar prematrícula | Cumplido técnico | Finaliza, asigna grupos, crea `student_subject_assignments`, genera PDF. Requiere horarios publicados para que una materia sea seleccionable. |
| 14 | Modificación posterior | Cumplido técnico | `ReopenModular` y auditoría de reapertura existen. |
| 15 | Comprobante PDF | Parcial alto | PDF incluye materias, grupo, docente/por asignar, horario, identidad si existe. Requiere documentos/horarios reales. |
| 16 | Documento de identidad | Parcial alto | `Celosan/Documents` y servicio existen; falta carga real de documentos. |
| 17 | Vista profesor activos | Parcial | Los servicios filtran activos; no se hizo prueba UI completa por profesor. |
| 18 | Retiro por asignatura | Cumplido operativo | Profesor puede solicitar retiro desde asistencia; Director tiene entrada de menú para aprobar/rechazar. |
| 19 | Estados prematrícula | Parcial | Usa `Pendiente`, `Finalizada`, `Reabierta`, etc. No se normalizó a todos los nombres sugeridos. |
| 20 | Estados por asignatura | Cumplido técnico | `Active`, `Withdrawn`, créditos `Valid`, promoción `Approved/Failed`; falta homologación visual completa. |
| 21 | Avance académico | Cumplido técnico | `CelosanReportService.BuildProgressRowsAsync` y vista modular muestran progreso. Precisión depende de créditos reales. |
| 22 | Reportes recomendados | Parcial alto | `Celosan/Reports` cubre prematriculados, demanda, cupos, grupos llenos, estudiantes por profesor, documentos vencidos, retiradas, avance e historial. |
| 23 | Auditoría | Parcial alto | `AuditLogs` se usan en documentos, bulk credits, reabre/cambios. Falta auditar cada evento menor de UI. |
| 24 | Dispositivos/responsive | Cumplido técnico | Vistas usan Bootstrap/table-responsive. Queda recomendada prueba real móvil. |
| 25 | Flujo principal completo | Parcial alto | Flujo central listo. Horarios/docentes/prerrequisitos/datos reales son bloqueantes para 100% operativo. |
| 26 | Observación final | Parcial alto | Base técnica lista; operación requiere cargar horarios, docentes, prerequisitos y documentos. |

## Datos pendientes para operación real

1. `schedule_entries = 0`: no hay horarios publicados; el sistema bloquea selección hasta que existan horarios compatibles.
2. No todos los `subject_assignments` tienen docente; el PDF mostrará "Por asignar docente" donde aplique.
3. La malla curricular fue sembrada; los prerrequisitos oficiales deben cargarse desde `Malla y Prerrequisitos`.
4. Convalidaciones/créditos dependen de carga real por estudiante.
5. Documentos de identidad dependen de archivos reales.

## Próximo cierre recomendado

1. Cargar/crear horarios y bloques nocturnos reales.
2. Completar docentes para cada `subject_assignment` ofertado.
3. Sembrar prerrequisitos oficiales por materia y nivel.
4. Cargar créditos/convalidaciones de estudiantes.
5. Cargar documentos de identidad.
6. Probar con estudiante, profesor, director, secretaría y admin.
