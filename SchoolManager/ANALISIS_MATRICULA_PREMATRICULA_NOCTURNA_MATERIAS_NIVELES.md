# Análisis matrícula/prematrícula nocturna: materias y niveles

Fecha de análisis: 2026-06-22  
Proyecto: `C:\Proyectos\EduplanerNoche\SchoolManager`  
Base revisada: Render Producción  
Modo de trabajo: solo lectura en base de datos (`SELECT`). No se insertaron, actualizaron ni eliminaron registros.

## 1. Resumen ejecutivo

El problema principal no está en la existencia de materias base ni en `subject_assignments`: producción sí tiene materias y asignaciones nocturnas para la escuela CELOSAM. Para los estudiantes revisados, el catálogo legacy de `StudentAssignment` tendría materias disponibles:

- `8-1084-5433@celosam.com`: 9 / 9-A / Noche, con 16 `SubjectAssignment` en el mismo grado-grupo.
- `5-720-332@celosam.com`: 10 / 10-A / Noche, con 20 `SubjectAssignment` en el mismo grado-grupo.

La causa demostrable del vacío en prematrícula/matrícula modular es que el flujo de prematrícula nocturna modular espera datos en `curriculum_tracks` y `curriculum_subjects`, pero ambas tablas están vacías en producción. Además, `prematriculation_periods` está vacía, por lo que el formulario estándar de prematrícula no debería habilitar creación normal de prematrícula.

También hay una inconsistencia crítica de año académico: existen 13 registros activos para 2026 en la misma escuela, y las matrículas activas existentes apuntan a 3 `academic_year_id` distintos. El servicio `AcademicYearService.GetActiveAcademicYearAsync` escoge el más reciente por `created_at`, que no coincide con los años usados por las matrículas activas existentes. Esto puede romper filtros futuros por año académico, especialmente si se crean períodos/tracks asociados a uno de esos IDs.

## 2. Flujo actual de carga de niveles y materias

### Flujo A: prematrícula clásica

Ruta de entrada:

- `GET /Prematriculation/Create`
- Vista: `Views/Prematriculation/Create.cshtml`
- Controlador: `Controllers/PrematriculationController.cs`

Carga de niveles:

- `PrematriculationController.Create()` llama a `_gradeLevelService.GetAllAsync()`.
- `GradeLevelService.GetAllAsync()` retorna todos los registros de `grade_levels` sin filtro por `school_id`.
- Si el usuario es estudiante, se obtiene su grado actual desde `student_assignments` y se permite solo el mismo grado o el siguiente.

Carga de grupos:

- La vista `Create.cshtml` usa AJAX:
  - `GET /Prematriculation/GetAvailableGroups?gradeId=...`
- `PrematriculationController.GetAvailableGroups()` llama a `PrematriculationService.GetAvailableGroupsAsync(schoolId, gradeId)`.
- `GetAvailableGroupsAsync` filtra `groups` por `school_id`.
- Si hay `gradeId`, solo muestra grupos que tienen alguna fila en `subject_assignments` con ese `grade_level_id` y `group_id`.

Resultado esperado:

`GradeLevel -> SubjectAssignment(GradeLevelId, GroupId) -> Group`

Este flujo no carga materias para selección directa. Solo permite crear una prematrícula con grado/grupo.

### Flujo B: listado de prematrículas y selección modular de materias

Ruta de entrada:

- `GET /Prematriculation/MyPrematriculations`
- Vista: `Views/Prematriculation/MyPrematriculations.cshtml`

Desde cada prematrícula, la vista muestra el botón:

- `GET /Prematriculation/ModularSubjects/{id}`

Carga de materias:

- Controlador: `PrematriculationController.ModularSubjects(Guid id)`
- Servicio: `CelosamPrematriculationModuleService.GetDashboardAsync(prematriculationId)`
- El servicio resuelve una malla activa:
  - `ResolveTrackAsync(prematriculation.SchoolId, period.AcademicYearId)`
  - Tabla: `curriculum_tracks`
- Luego carga materias:
  - Tabla: `curriculum_subjects`
  - Filtro: `CurriculumTrackId == track.Id && IsActive`
- Para habilitar selección, calcula cupos usando:
  - `subject_assignments`
  - `groups`
  - `student_subject_assignments`
  - `student_prematriculation_subject_selections`

Resultado esperado:

`PrematriculationPeriod -> AcademicYear -> CurriculumTrack -> CurriculumSubjects -> SubjectAssignment -> Group`

Este es el flujo que debería permitir ver y seleccionar materias modulares. En producción no puede funcionar porque no hay `curriculum_tracks` ni `curriculum_subjects`.

### Flujo C: asignación manual de materias a estudiante

Ruta:

- `GET /StudentAssignment/GetAvailableSubjectCatalog?studentId=...`
- Controlador: `StudentAssignmentController.GetAvailableSubjectCatalog`
- Servicio: `StudentAssignmentService.GetAvailableSubjectCatalogAsync`

Cadena usada:

`StudentAssignments activos -> SubjectAssignments de grupos de la escuela -> Subjects/GradeLevels/Groups`

Este flujo sí tiene datos disponibles en producción. No depende de `curriculum_tracks`.

## 3. Tablas involucradas

Tablas principales:

- `schools`
- `academic_years`
- `prematriculation_periods`
- `prematriculations`
- `student_prematriculation_subject_selections`
- `prematriculation_receipts`
- `grade_levels`
- `groups`
- `shifts`
- `subjects`
- `subject_assignments`
- `teacher_assignments`
- `schedule_entries`
- `student_assignments`
- `student_subject_assignments`
- `trimester`
- `curriculum_tracks`
- `curriculum_subjects`
- `curriculum_subject_prerequisites`
- `student_academic_credits`
- `subject_promotion_records`
- `student_academic_period_enrollments`

## 4. Controladores involucrados

### `Controllers/PrematriculationController.cs`

Acciones relevantes:

- `MyPrematriculations()`: lista prematrículas del estudiante/acudiente.
- `Create()` GET: carga período activo, estudiante, grados disponibles.
- `Create(PrematriculationCreateDto dto)` POST: crea la prematrícula.
- `GetAvailableGrades(Guid? studentId)`: endpoint AJAX para grados.
- `GetAvailableGroups(Guid? gradeId)`: endpoint AJAX para grupos.
- `ModularSubjects(Guid id)`: pantalla para seleccionar materias modulares.
- `SelectModularSubject(...)`: agrega materia curricular a selección.
- `FinalizeModular(Guid id)`: finaliza prematrícula modular y crea matrícula/materias.

### `Controllers/StudentAssignmentController.cs`

Acciones relevantes:

- `GetAvailableGradeGroups()`: usa combinaciones de `subject_assignments`.
- `GetGradeGroupByStudent(Guid studentId)`: devuelve matrículas activas del estudiante.
- `GetSubjectEnrollmentsByStudent(Guid studentId)`: devuelve materias activas ya asignadas.
- `GetAvailableSubjectCatalog(Guid studentId)`: catálogo legacy/avanzado de materias disponibles.
- `AddSubjectEnrollment(...)`: agrega materia usando flujo modular si está activo o legacy si no aplica.

### `Controllers/AcademicCatalogController.cs`

Acciones relevantes:

- `Index()`: carga catálogos base: especialidades, áreas, materias, grados, grupos, trimestres, jornadas.
- `SaveCatalog(...)`: carga masiva nocturna. Crea `SubjectAssignment`.
- `GuardarTrimestres(...)`, `ActivarTrimestre(...)`, `DesactivarTrimestre(...)`: configuración de trimestres.

### `Controllers/SubjectAssignmentController.cs`

Acciones relevantes:

- `Index()`: lista `SubjectAssignment` filtrado por `SchoolId`.
- `GetDropdownData()`: carga catálogos para administración.
- `Create(...)`: crea `SubjectAssignment`.

### `Controllers/TeacherAssignmentController.cs`

Usa `SubjectAssignment` para asociar docentes. No es requerido para que `ModularSubjects` muestre materias, pero sí afecta visualización posterior de docente/horario.

### `Controllers/ModularAcademicAuditController.cs`

Auditoría superadmin del modelo modular. Muestra indicadores de currículo, trimestres, créditos y equivalencias.

## 5. Servicios involucrados

### `PrematriculationPeriodService`

Método crítico:

- `GetActivePeriodAsync(Guid schoolId)`

Filtro:

- `SchoolId == schoolId`
- `IsActive == true`
- `StartDate <= now`
- `EndDate >= now`

Resultado en producción: no devuelve nada porque `prematriculation_periods` está vacía.

### `PrematriculationService`

Métodos críticos:

- `CreatePrematriculationAsync`
- `GetAvailableGroupsAsync`
- `AutoAssignGroupAsync`

`GetAvailableGroupsAsync` solo muestra grupos si existe `SubjectAssignment` para el grado y grupo seleccionados. No filtra explícitamente por jornada nocturna en la consulta de `SubjectAssignment`, pero sí parte de grupos de la escuela.

### `CelosamPrematriculationModuleService`

Métodos críticos:

- `GetDashboardAsync`
- `ResolveTrackAsync`
- `CountAvailableGroupsBySubjectAsync`
- `SelectSubjectAsync`
- `FinalizeAsync`
- `ResolveBestSubjectAssignmentAsync`

Filtros críticos:

- `curriculum_tracks.IsActive`
- `curriculum_tracks.SchoolId == null || SchoolId == schoolId`
- `curriculum_tracks.AcademicYearId == null || AcademicYearId == period.AcademicYearId`
- `curriculum_subjects.CurriculumTrackId == track.Id`
- `curriculum_subjects.IsActive`
- `subject_assignments.SubjectId == curriculum_subject.SubjectId`
- `subject_assignments.GradeLevelId == curriculum_subject.GradeLevelId` si el currículo tiene grado
- `subject_assignments.SchoolId == null || SchoolId == schoolId`
- `subject_assignments.Status != "Closed"`

Resultado en producción: no hay track ni materias curriculares.

### `StudentAssignmentService`

Métodos críticos:

- `GetAvailableSubjectCatalogAsync`
- `AddSubjectEnrollmentAsync`
- `EnsureEnrollmentBaseAsync`

Este servicio sí puede leer el catálogo existente en `subject_assignments`. No depende de `curriculum_tracks` para listar el catálogo legacy.

### `SubjectAssignmentService`

Métodos críticos:

- `GetDistinctGradeGroupCombinationsAsync`
- `GetAllSubjectAssignments`
- `GetByGroupAndGradeAsync`

Riesgo: `GetDistinctGradeGroupCombinationsAsync` no filtra por escuela. En producción solo hay una escuela, por lo que no explica el vacío actual.

### `GradeLevelService`

Método crítico:

- `GetAllAsync`

Riesgo: devuelve todos los grados sin filtro por escuela.

### `AcademicYearService`

Método crítico:

- `GetActiveAcademicYearAsync`

Filtro:

- `SchoolId == targetSchoolId`
- `IsActive == true`
- `StartDate <= now`
- `EndDate >= now`
- Ordena por `CreatedAt DESC`

Riesgo confirmado: hay 13 años activos 2026; el método elegirá el último creado, aunque las matrículas activas usan otros IDs.

## 6. Vistas involucradas

### `Views/Prematriculation/Create.cshtml`

Muestra:

- estudiante
- grado
- grupo

No muestra materias. El `select` de grupos depende del AJAX `GetAvailableGroups`.

### `Views/Prematriculation/MyPrematriculations.cshtml`

Muestra botón de acción para:

- detalles
- seleccionar materias (`ModularSubjects`)

Si no existen prematrículas, el estudiante solo verá el botón de nueva prematrícula.

### `Views/Prematriculation/ModularSubjects.cshtml`

Muestra:

- tabla de `Model.AvailableSubjects`
- tabla de `Model.SelectedSubjects`

Si `AvailableSubjects` viene vacío, la tabla queda sin filas. Esto ocurre cuando no existe malla curricular modular.

### `Views/StudentAssignment/Index.cshtml`

Vista administrativa para asignar grado/grupo/materias. Usa endpoints de `StudentAssignment`.

### `Views/SubjectAssignment/Index.cshtml`

Vista administrativa para crear/editar `SubjectAssignment`.

### `Views/AcademicCatalog/Index.cshtml` y `Upload`

Vista administrativa/carga de catálogo. Alimenta `SubjectAssignment`, no `curriculum_subjects`.

## 7. Endpoints AJAX involucrados

- `GET /Prematriculation/GetAvailableGrades`
- `GET /Prematriculation/GetAvailableGroups`
- `GET /StudentAssignment/GetAvailableGradeGroups`
- `GET /StudentAssignment/GetGradeGroupByStudent/{studentId}`
- `GET /StudentAssignment/GetSubjectEnrollmentsByStudent/{studentId}`
- `GET /StudentAssignment/GetAvailableSubjectCatalog`
- `POST /StudentAssignment/AddSubjectEnrollment`
- `POST /Prematriculation/SelectModularSubject`
- `POST /Prematriculation/FinalizeModular`
- `GET /SubjectAssignment/GetDropdownData`

## 8. Consultas SQL de diagnóstico usadas

Todas las consultas fueron `SELECT`.

### Escuela, años, períodos y trimestres

```sql
SELECT id, name, is_active, created_at FROM schools ORDER BY created_at NULLS LAST;

SELECT id, name, school_id, is_active, start_date, end_date
FROM academic_years
ORDER BY is_active DESC, start_date DESC;

SELECT id, name, school_id, academic_year_id, trimester_id, is_active,
       start_date, end_date, max_subjects_allowed, max_capacity_per_group,
       auto_assign_by_shift
FROM prematriculation_periods
ORDER BY created_at DESC;

SELECT id, name, school_id, is_active
FROM trimester
ORDER BY name;
```

### Catálogos base

```sql
SELECT id, name, school_id FROM grade_levels ORDER BY school_id NULLS FIRST, name;

SELECT id, name, school_id, display_order, is_active
FROM shifts
ORDER BY name;

SELECT g.id, g.name, g.grade, g.shift, s.name AS shift_name, g.shift_id, g.max_capacity
FROM groups g
LEFT JOIN shifts s ON s.id = g.shift_id
WHERE g.school_id = '6e42399f-6f17-4585-b92e-fa4fff02cb65'
ORDER BY COALESCE(g.shift, s.name), g.name;

SELECT COUNT(*) AS subjects_total,
       COUNT(*) FILTER (WHERE school_id='6e42399f-6f17-4585-b92e-fa4fff02cb65') AS subjects_school,
       COUNT(*) FILTER (WHERE school_id IS NULL) AS subjects_null_school
FROM subjects;
```

### SubjectAssignment

```sql
SELECT COALESCE(status,'<NULL>') AS status, COUNT(*)
FROM subject_assignments
WHERE "SchoolId" = '6e42399f-6f17-4585-b92e-fa4fff02cb65'
GROUP BY COALESCE(status,'<NULL>')
ORDER BY 1;

SELECT gl.name AS grade, g.name AS group_name, COALESCE(g.shift, sh.name, '') AS shift,
       COUNT(*) AS subject_assignment_count,
       COUNT(DISTINCT sa.subject_id) AS distinct_subjects
FROM subject_assignments sa
JOIN grade_levels gl ON gl.id = sa.grade_level_id
JOIN groups g ON g.id = sa.group_id
LEFT JOIN shifts sh ON sh.id = g.shift_id
WHERE sa."SchoolId" = '6e42399f-6f17-4585-b92e-fa4fff02cb65'
GROUP BY gl.name, g.name, COALESCE(g.shift, sh.name, '')
ORDER BY gl.name::int NULLS LAST, g.name;
```

### Currículo modular

```sql
SELECT id, name, school_id, academic_year_id, is_active, created_at
FROM curriculum_tracks
ORDER BY is_active DESC, school_id NULLS LAST, academic_year_id NULLS LAST, created_at DESC;

SELECT ct.name AS track_name, ct.school_id, ct.academic_year_id, ct.is_active,
       COUNT(cs.id) AS curriculum_subjects,
       COUNT(*) FILTER (WHERE cs.is_active) AS active_subjects
FROM curriculum_tracks ct
LEFT JOIN curriculum_subjects cs ON cs.curriculum_track_id = ct.id
GROUP BY ct.id, ct.name, ct.school_id, ct.academic_year_id, ct.is_active
ORDER BY ct.is_active DESC, active_subjects DESC;
```

### Estudiantes y disponibilidad real legacy

```sql
WITH target_students AS (
  SELECT id, email
  FROM users
  WHERE email IN ('8-1084-5433@celosam.com','5-720-332@celosam.com')
), active_assignments AS (
  SELECT ts.email, sa.student_id, sa.grade_id, sa.group_id, sa.enrollment_type
  FROM target_students ts
  JOIN student_assignments sa ON sa.student_id = ts.id AND sa.is_active
)
SELECT aa.email, gl.name AS student_grade, g.name AS student_group, aa.enrollment_type,
       COUNT(DISTINCT subj.id) AS matching_subject_assignments_same_grade_group,
       COUNT(DISTINCT subj.subject_id) AS matching_distinct_subjects
FROM active_assignments aa
JOIN grade_levels gl ON gl.id = aa.grade_id
JOIN groups g ON g.id = aa.group_id
LEFT JOIN subject_assignments subj
       ON subj.grade_level_id = aa.grade_id
      AND subj.group_id = aa.group_id
      AND subj."SchoolId" = '6e42399f-6f17-4585-b92e-fa4fff02cb65'
GROUP BY aa.email, gl.name, g.name, aa.enrollment_type
ORDER BY aa.email;
```

### Docentes y horarios

```sql
SELECT COUNT(*) AS teacher_assignments_total FROM teacher_assignments;

SELECT COUNT(DISTINCT ta.subject_assignment_id) AS subject_assignments_with_teacher,
       (SELECT COUNT(*) FROM subject_assignments
        WHERE "SchoolId"='6e42399f-6f17-4585-b92e-fa4fff02cb65') AS subject_assignments_school
FROM teacher_assignments ta
JOIN subject_assignments sa ON sa.id = ta.subject_assignment_id
WHERE sa."SchoolId"='6e42399f-6f17-4585-b92e-fa4fff02cb65';

SELECT COUNT(*) AS schedule_entries_total FROM schedule_entries;
```

## 9. Resultado de cada validación

### Escuela

Existe una escuela:

- `6e42399f-6f17-4585-b92e-fa4fff02cb65`
- `Centro de Educación Laboral Oficial San Miguelito`
- Activa.

### Años académicos

Resultado:

- 13 registros activos con nombre `2026` para la misma escuela.
- Todos tienen rango `2026-01-01` a `2026-12-31`.
- Las matrículas activas usan 3 `academic_year_id` distintos.
- El servicio actual escogería el año 2026 más reciente por `created_at`.
- 349 matrículas activas tienen un `academic_year_id` diferente al que el servicio escogería actualmente.

Impacto:

- Alto riesgo en cualquier filtro por `academic_year_id`.
- Si se crea un período o track con un año distinto al usado en matrículas, puede no cruzar datos como se espera.

### Períodos de prematrícula

Resultado:

- `prematriculation_periods`: 0 filas.

Impacto:

- `PrematriculationController.Create()` redirige con mensaje: "El período de prematrícula no está disponible".
- No hay prematrículas creadas (`prematriculations`: 0 filas).
- Sin prematrícula no hay acceso real al flujo `ModularSubjects` salvo por IDs inexistentes.

### Trimestres

Resultado:

- Tabla real: `trimester`.
- Existen 3 registros:
  - `1T`: inactivo.
  - `2T`: activo.
  - `3T`: activo.

Impacto:

- Existen trimestres, pero no hay período de prematrícula que use `trimester_id`.
- `FinalizeModular` falla si el período no tiene trimestre ni la prematrícula `TargetTrimesterId`.

### Grados / niveles

Resultado:

- Existen 6 `grade_levels` globales (`school_id` NULL): `7`, `8`, `9`, `10`, `11`, `12`.

Impacto:

- Los niveles existen.
- No están asociados directamente a la escuela, pero el código permite globales.
- No explica el vacío de materias.

### Jornadas / grupos nocturnos

Resultado:

- Existe jornada `Noche`.
- Hay grupos nocturnos reales:
  - `7-A`, `8-A`, `9-A`, `10-A`, `10-A1`... `12-A4`.
- Hay un grupo `12-A1` con `shift='Noche'` pero `shift_id` vacío.
- El grupo `12-A` no tiene `SubjectAssignment`.

Impacto:

- La mayoría de grupos nocturnos tienen estructura.
- Hay inconsistencias puntuales de `shift_id`/asignaciones que deben corregirse después, pero no explican que no aparezca nada globalmente.

### Materias

Resultado:

- `subjects`: 83 filas, todas con `school_id` de CELOSAM.

Impacto:

- Las materias base existen.

### SubjectAssignment

Resultado:

- `subject_assignments` para CELOSAM: 435.
- Estados:
  - `Active`: 30.
  - `NULL`: 405.
- La lógica modular considera válido todo lo que no sea `Closed`, por lo tanto `NULL` no bloquea.
- Cobertura por grado:
  - 7: 33 asignaciones.
  - 8: 32 asignaciones.
  - 9: 30 asignaciones.
  - 10: 121 asignaciones.
  - 11: 100 asignaciones.
  - 12: 119 asignaciones.

Impacto:

- El catálogo legacy está poblado.
- `SubjectAssignment` no es la causa raíz del vacío modular.

### CurriculumTrack / CurriculumSubject

Resultado:

- `curriculum_tracks`: 0 filas.
- `curriculum_subjects`: 0 filas.

Impacto:

- Causa raíz confirmada para `ModularSubjects`: `GetDashboardAsync` no encuentra malla curricular, retorna `AvailableSubjects` vacío.

### Prematriculations

Resultado:

- `prematriculations`: 0 filas.

Impacto:

- No hay registros desde los cuales el estudiante pueda entrar a seleccionar materias.
- La pantalla `MyPrematriculations` mostrará lista vacía, aunque tenga botón de nueva prematrícula.

### TeacherAssignment / ScheduleEntries

Resultado:

- `teacher_assignments`: 129.
- De 435 `SubjectAssignment`, 129 tienen docente.
- `schedule_entries`: 0.

Impacto:

- No es requerido para mostrar materias en `ModularSubjects`.
- Puede afectar comprobantes, horarios y resolución final con conflictos de horario.
- Si el requerimiento funcional exige que solo se muestren materias con docente/horario publicado, actualmente no hay horarios.

## 10. Causa raíz probable

Hay dos causas raíz principales:

1. **No existe período activo de prematrícula**: `prematriculation_periods` está vacía. El estudiante no puede iniciar formalmente una prematrícula desde `Create`.
2. **No existe malla curricular modular**: `curriculum_tracks` y `curriculum_subjects` están vacías. Aunque se cree una prematrícula, `ModularSubjects` no tendrá materias disponibles porque depende de esas tablas, no directamente de `subject_assignments`.

## 11. Causa raíz confirmada

Causa confirmada para materias vacías en `ModularSubjects`:

- Archivo: `Services/Implementations/CelosamPrematriculationModuleService.cs`
- Método: `GetDashboardAsync`
- Consulta lógica:
  - resolver `CurriculumTrack` activo.
  - cargar `CurriculumSubjects` activos del track.
- Tablas:
  - `curriculum_tracks`
  - `curriculum_subjects`
- Campo/filtro:
  - `ct.IsActive`
  - `ct.SchoolId == null || ct.SchoolId == schoolId`
  - `ct.AcademicYearId == null || ct.AcademicYearId == period.AcademicYearId`
  - `cs.CurriculumTrackId == track.Id`
  - `cs.IsActive`
- Evidencia:
  - Ambas tablas están vacías en producción.

Causa confirmada para no iniciar prematrícula:

- Archivo: `Services/Implementations/PrematriculationPeriodService.cs`
- Método: `GetActivePeriodAsync`
- Tabla: `prematriculation_periods`
- Filtros:
  - `SchoolId == schoolId`
  - `IsActive == true`
  - `StartDate <= now`
  - `EndDate >= now`
- Evidencia:
  - `prematriculation_periods` tiene 0 filas.

## 12. Riesgos

- Crear solo `prematriculation_periods` sin `curriculum_tracks/curriculum_subjects` permitirá crear prematrículas, pero la selección de materias seguirá vacía.
- Crear `curriculum_tracks` asociado al `academic_year_id` incorrecto puede hacer que el servicio no resuelva la malla si el período usa otro año.
- Hay 13 años académicos activos 2026. Esto puede provocar resultados no deterministas de negocio aunque el método ordene por `CreatedAt`.
- Algunos grupos tienen `shift='Noche'` pero `shift_id` nulo. Los flujos modernos usan `shift_id` en varias partes.
- `SubjectAssignmentService.GetDistinctGradeGroupCombinationsAsync` no filtra por escuela.
- `GradeLevelService.GetAllAsync` y `GroupService.GetAllAsync` no filtran por escuela.
- `AcademicCatalog.SaveCatalog` crea `SubjectAssignment`, pero no crea `CurriculumSubject`. La carga de catálogo nocturno no alimenta el modelo modular.
- `schedule_entries` está vacío. La selección puede verse, pero horarios/docente final pueden salir incompletos.

## 13. Solución recomendada

Separar corrección de datos semilla/controlada y corrección de código.

Primero, definir una estrategia única para educación nocturna:

- Si el flujo oficial de estudiante será **prematrícula modular**, entonces hay que alimentar:
  - `prematriculation_periods`
  - `curriculum_tracks`
  - `curriculum_subjects`
  - opcionalmente `curriculum_subject_prerequisites`
  - y asegurar mapeo con `subject_assignments`.

- Si el flujo oficial será **legacy/avanzado por SubjectAssignment**, entonces `ModularSubjects` no debe ser el camino principal para selección de materias, o debe tener fallback al catálogo legacy.

Recomendación técnica:

1. Normalizar/seleccionar un único `academic_years` activo para la escuela.
2. Crear un período de prematrícula activo para ese año y trimestre correspondiente.
3. Crear un `curriculum_track` activo CELOSAM para ese año.
4. Poblar `curriculum_subjects` desde `subject_assignments`, agrupando por `subject_id` + `grade_level_id` y usando `level_name` desde `grade_levels.name`.
5. Mantener `subject_assignments` como oferta por grupo/jornada.
6. En código, agregar validaciones administrativas que adviertan:
   - no hay período activo.
   - no hay malla curricular activa.
   - no hay materias curriculares.
   - no hay `SubjectAssignment` para las materias curriculares.

## 14. Cambios mínimos necesarios

Sin aplicar todavía, el set mínimo recomendado sería:

### Datos/configuración

- Crear exactamente un período activo en `prematriculation_periods`.
- Crear un `curriculum_track` activo para CELOSAM.
- Crear `curriculum_subjects` activos para grados 7-12 usando materias existentes.
- Revisar cuál de los 13 años académicos 2026 será el canónico.

### Código

- En `PrematriculationController.Create`, mostrar un mensaje más accionable si no hay período activo.
- En `CelosamPrematriculationModuleService.GetDashboardAsync`, diferenciar:
  - "no hay malla activa" vs.
  - "hay malla, pero no hay materias" vs.
  - "hay materias, pero sin grupos/cupos".
- En `AcademicCatalogController.SaveCatalog`, evaluar si debe alimentar también `curriculum_subjects` o si debe existir un importador separado para malla curricular nocturna.
- En `SubjectAssignmentService.GetDistinctGradeGroupCombinationsAsync`, filtrar por escuela actual.
- En `GradeLevelService.GetAllAsync` y `GroupService.GetAllAsync`, filtrar por tenant o exponer métodos específicos por escuela.

## 15. Cambios que NO deben hacerse

- No insertar materias duplicadas en `subjects` si ya existen.
- No crear más años académicos 2026 activos.
- No asociar `curriculum_tracks` a un `academic_year_id` arbitrario sin resolver primero el año canónico.
- No borrar `subject_assignments`: son el catálogo legacy que sí tiene oferta real.
- No cambiar todos los grupos a una jornada por texto solamente; debe respetarse `shift_id`.
- No hacer fallback silencioso en producción que mezcle malla modular y legacy sin advertencia, porque puede matricular materias fuera de plan.
- No depender de `teacher_assignments` para mostrar materias si el negocio permite selección antes de docente/horario.

## 16. Plan seguro de corrección

Fase 1: solo lectura / validación funcional

1. Confirmar con dirección académica cuál es el año 2026 canónico.
2. Confirmar si el estudiante debe elegir por:
   - materia modular individual, o
   - grado/grupo completo.
3. Confirmar trimestre activo real para prematrícula.
4. Confirmar si materias sin docente/horario pueden seleccionarse.

Fase 2: script de diagnóstico repetible

1. Crear SQL solo `SELECT` para:
   - años activos duplicados.
   - período activo.
   - track activo.
   - materias curriculares.
   - materias curriculares sin `SubjectAssignment`.
   - grupos nocturnos sin `shift_id`.
   - grupos nocturnos sin `SubjectAssignment`.

Fase 3: corrección de datos con script transaccional revisado

1. Preparar script `BEGIN/COMMIT` con rollback posible.
2. Crear período activo solo si no existe.
3. Crear track curricular solo si no existe.
4. Poblar `curriculum_subjects` derivado de `subject_assignments`.
5. No tocar matrículas existentes.

Fase 4: corrección de código

1. Agregar mensajes de diagnóstico visibles para admin/superadmin.
2. Agregar fallback controlado o pantalla de configuración pendiente.
3. Agregar filtros de tenant donde faltan.

Fase 5: pruebas

1. Probar estudiante sin prematrícula.
2. Crear prematrícula.
3. Entrar a seleccionar materias.
4. Seleccionar materia con cupo.
5. Finalizar prematrícula.
6. Validar `student_subject_assignments`, recibo y portal del estudiante.

## 17. Pruebas funcionales recomendadas

- Login como estudiante CELOSAM con matrícula activa 9 / 9-A / Noche.
- Ver menú `Prematrícula`.
- Entrar a `Nueva Prematrícula`.
- Confirmar que aparece período activo.
- Confirmar que aparecen niveles esperados: 9 y/o 10 según regla de promoción.
- Seleccionar grupo nocturno correcto.
- Crear prematrícula.
- Entrar a `Seleccionar materias`.
- Confirmar que `AvailableSubjects` no está vacío.
- Confirmar que cada materia muestra nivel, estado, cupos.
- Seleccionar una materia.
- Confirmar que pasa a selección actual.
- Finalizar prematrícula.
- Confirmar creación de `student_prematriculation_subject_selections`.
- Confirmar creación de `student_subject_assignments`.
- Confirmar que el recibo no muestra "Por asignar" donde no corresponde.
- Repetir con estudiante de 10 / 10-A / Noche.
- Repetir con estudiante sin matrícula activa para validar mensaje.
- Repetir con grupo sin `shift_id` para validar bloqueo o corrección.

## 18. Checklist final para validar escuela nocturna

- [ ] Existe exactamente un año académico activo canónico para CELOSAM.
- [ ] Existe un período de prematrícula activo con `academic_year_id` canónico.
- [ ] El período tiene `trimester_id` definido si se usará flujo modular.
- [ ] Existe `curriculum_track` activo para CELOSAM y año canónico.
- [ ] Existen `curriculum_subjects` activos para niveles 7-12.
- [ ] Cada `curriculum_subject` tiene `subject_id` válido.
- [ ] Cada `curriculum_subject` tiene `grade_level_id` o `level_name` correcto.
- [ ] Para cada materia curricular hay al menos un `SubjectAssignment` compatible.
- [ ] Los grupos nocturnos tienen `shift='Noche'` y `shift_id` correcto.
- [ ] No hay grupos nocturnos esperados sin `SubjectAssignment`.
- [ ] Los estudiantes tienen `student_assignments` activos con grado/grupo/jornada correctos.
- [ ] `ModularSubjects` muestra materias disponibles.
- [ ] Las materias bloqueadas muestran razón clara.
- [ ] Las materias sin cupo muestran razón clara.
- [ ] La selección crea `student_prematriculation_subject_selections`.
- [ ] La finalización crea `student_subject_assignments`.
- [ ] El flujo no depende de registros con `Guid.Empty`.
- [ ] Los endpoints AJAX no devuelven vacío sin mensaje explicativo.

## Conclusión

La base de producción sí tiene oferta académica nocturna en `subject_assignments`, pero el flujo de prematrícula modular no consume directamente esa tabla como catálogo principal. Consume primero `curriculum_tracks` y `curriculum_subjects`, que están vacías. Además, no hay período activo de prematrícula. Por eso los estudiantes no pueden ver correctamente materias/niveles en matrícula/prematrícula.

La corrección segura debe empezar por definir el año académico canónico, crear/configurar el período de prematrícula y poblar la malla curricular modular desde el catálogo académico existente, antes de tocar lógica de selección.
