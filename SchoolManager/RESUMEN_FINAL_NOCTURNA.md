# Resumen final — Soporte estudiantes nocturnos y multi-matrícula

**Fecha:** 2026-04-18  
**Alcance:** aplicación `SchoolManager` (ASP.NET Core), coherente con `ANALISIS_SOPORTE_NOCTURNA_EDUPLANER.md`, `ANEXO_TECNICO_NOCTURNA_EDUPLANER.md`, `PLAN_IMPLEMENTACION_NOCTURNA_EDUPLANER.md`, `AUDITORIA_POST_IMPLEMENTACION_NOCTURNA.md`, `IMPLEMENTACION_NOCTURNA_LOG.md`.

## Qué se corrigió

1. **Reasignación segura (NF-01 / R03):** `UpdateGroupAndGrade` sin modo aditivo ahora inactiva todas las matrículas activas antes de insertar la nueva cuando hay una sola matrícula o tras confirmación explícita; si hay más de una matrícula activa, responde con `MULTI_ENROLLMENT_CONFIRM` hasta recibir `forceReplaceAll=true`. Elimina el bug del “reemplazo quirúrgico” que dejaba dos matrículas activas al cambiar de sección.
2. **Matrícula masiva y API `AssignAsync`:** anti-duplicado por `grade_id` + `group_id` + **`shift_id`**; inserciones con `ShiftId`; default **`replaceExistingActive=false`** alineado con la interfaz; carga masiva acotada por `SchoolId` del estudiante.
3. **Notas en reporte estudiantil:** con varias matrículas activas, las calificaciones se filtran solo a actividades con `GroupId` perteneciente a esas matrículas (no se incluyen actividades sin grupo en ese contexto).
4. **Notas en libro / guardado:** resolución de `StudentSubjectAssignment` exige materia y **grupo** de la actividad para no tomar la primera asignatura de materia a ciegas.
5. **Listas por grupo:** `GetByGroupAndGradeAsync` deduplica por `StudentId`.
6. **Aprobados / Reprobados:** desplegable de grado opcional cargado desde BD vía `GET .../ObtenerGradosFiltro`; nivel **Nocturna** obtiene grados desde grupos con turno cuyo nombre contiene “noche”.
7. **Carnet:** muestra grado, grupo y jornada del contexto primario (misma prioridad nocturna que antes) y una segunda línea con **otros** contextos activos si existen.
8. **Limpieza:** eliminación de `Console.WriteLine` en `StudentReportService` y `StudentAssignmentService` (ruido y riesgo en producción).

## Qué se validó

- `dotnet build` en `SchoolManager`: **0 errores, 0 warnings** (2026-04-18).
- Revisión estática de flujos: cambio de grupo (1 vs N matrículas), filtros de reporte, carnet, reporte aprobados/reprobados.

## Qué queda pendiente

- Refactor de dominio tipo **AcademicEnrollment** (auditoría “camino C”) — mejora estructural, no bloqueo funcional inmediato.
- **Índice único parcial** en `student_assignments` (activos): documentado y comentado en `SQL_CAMBIOS_NOCTURNA.sql`; requiere decisión institucional y prueba con datos reales.
- **Pruebas automatizadas** para `UpdateGroupAndGrade`, reportes y carnet (recomendación de auditoría).
- Archivo **`AUDITORIA_CURSOR_POST_NOCTURNA.md`**: no estaba en el repositorio; no pudo usarse como fuente.

## Riesgos residuales

- **Bajo:** operadores que confirmen `forceReplaceAll` deben entender que dejan una sola matrícula activa.
- **Medio (datos):** duplicados históricos en BD (misma tripleta activa) no se borran solos; se añadió consulta sugerida en SQL de trazabilidad.

## Recomendaciones

1. Ejecutar en staging el flujo: estudiante con 2 matrículas nocturnas → cambio de grupo con confirmación → verificar una sola activa tras confirmación total.
2. Revisar resultados del `SELECT ... HAVING COUNT(*) > 1` del script SQL antes de cualquier índice único.
3. Añadir pruebas de integración mínimas en los puntos anteriores.

## Estado respecto a producción

**CASI LISTO:** la aplicación compila limpia y los riesgos críticos/medios identificados en auditoría para esta capa quedaron mitigados en código. La etiqueta “listo producción” completa exige validación operativa en el entorno real (datos, permisos, impresión de carnet, reportes con volumen) y decisión sobre índice único y limpieza DML.
