# Analisis del Modelo Academico para Educacion Nocturna

Fecha de auditoria: 2026-06-17

Alcance: revision de codigo del proyecto `C:\Proyectos\EduplanerNoche\SchoolManager` y consultas `SELECT` contra la base PostgreSQL de produccion en Render. No se modifico codigo, no se modificaron datos y no se crearon migraciones.

## 1. Respuesta ejecutiva

### Pregunta principal

**Cada trimestre no funciona como una matricula independiente.**

La evidencia tecnica muestra que la matricula real vive en `student_assignments` y, para educacion nocturna avanzada, en `student_subject_assignments`. Ambas se asocian a `academic_year_id`, `grade_id`, `group_id`, `shift_id`, `enrollment_type`, `is_active`, `start_date` y `end_date`, pero no tienen `trimester_id`.

**Los trimestres funcionan como periodos academicos para actividades, notas, asistencia/reportes y cierre/promocion por materia.** La tabla fisica `trimester` tiene fechas, orden, escuela y opcionalmente `academic_year_id`; `activities` referencia trimestre por `TrimesterId` y tambien mantiene el codigo legado `trimester`.

### Conclusion principal

El sistema hoy opera con una **matricula base anual o por ano academico**, no con matriculas separadas por trimestre. En Eduplaner Noche existe soporte adicional para multiples matriculas activas y materias individuales (`Nocturno`, `Refuerzo`, `Libre`), pero ese soporte tampoco convierte el trimestre en matricula. El trimestre solo delimita el periodo de evaluacion.

## 2. Arquitectura actual

### Tablas principales

| Area | Tabla | Rol real |
|---|---|---|
| Ano academico | `academic_years` | Periodo anual por escuela. Es la referencia anual de matriculas, notas y promocion. |
| Trimestres | `trimester` | Periodos fechados dentro del ano. No crea matriculas. |
| Prematricula | `prematriculation_periods` | Ventana administrativa de prematricula. |
| Prematricula | `prematriculations` | Solicitud del estudiante para grado/grupo; termina en estado `Matriculado`. |
| Historial prematricula | `prematriculation_histories` | Historial de cambios de estado de prematricula. |
| Matricula base | `student_assignments` | Matricula/asignacion activa del estudiante a grado, grupo, jornada y ano academico. |
| Materias del estudiante | `student_subject_assignments` | Inscripcion individual a una materia ofertada; clave en educacion nocturna avanzada. |
| Oferta academica | `subject_assignments` | Materias ofertadas por grado, grupo, area y especialidad. |
| Actividades | `activities` | Actividades por docente, materia, grupo, grado y trimestre. |
| Notas | `student_activity_scores` | Nota del estudiante asociada a actividad, matricula base y opcionalmente materia inscrita. |
| Promocion por materia | `subject_promotion_records` | Registro manual/automatico de resultado de materia por trimestre. |
| Asistencia | `attendance` | Asistencia por estudiante, grupo, grado, fecha, jornada y matricula base. |

### Servicios y controladores clave

| Flujo | Controlador | Servicio |
|---|---|---|
| Prematricula | `Controllers/PrematriculationController.cs` | `Services/Implementations/PrematriculationService.cs` |
| Periodos de prematricula | `Controllers/PrematriculationPeriodController.cs` | `Services/Implementations/PrematriculationPeriodService.cs` |
| Matricula/asignacion | `Controllers/StudentAssignmentController.cs` | `Services/Implementations/StudentAssignmentService.cs` |
| Trimestres | no se encontro controlador dedicado en la busqueda de archivos; el servicio existe | `Services/Implementations/TrimesterService.cs` |
| Actividades/notas | `Controllers/ActivityController.cs`, `Controllers/TeacherGradebookController.cs` | `Services/Implementations/ActivityService.cs`, `Services/Implementations/StudentActivityScoreService.cs` |
| Promocion | `Controllers/SubjectPromotionController.cs` | `Services/Implementations/SubjectPromotionService.cs` |
| Asistencia | `Controllers/AttendanceController.cs` | `Services/Implementations/AttendanceService.cs` |

## 3. Flujo real de prematricula y matricula

### 3.1 Nacimiento de una prematricula

El flujo inicia en `PrematriculationController.Create`.

Evidencia de codigo:

- `PrematriculationController.Create()` exige un periodo activo de prematricula mediante `_periodService.GetActivePeriodAsync(currentUser.SchoolId.Value)`.
- En `Create(PrematriculationCreateDto dto)`, el controlador asigna `PrematriculationPeriodId`, valida condicion academica, valida avance de grado y llama a `_prematriculationService.CreatePrematriculationAsync(dto, parentId)`.
- `PrematriculationService.CreatePrematriculationAsync` valida periodo activo, escuela del estudiante, duplicado por estudiante/periodo, documentos para nuevo ingreso, condicion academica y cupo de grupo.

Estados creados:

- La entidad se crea inicialmente con `Status = "Pendiente"`.
- Luego se cambia a `Status = "Prematriculado"` si la creacion termina correctamente.

Campos principales de `prematriculations` segun BD:

- `school_id`
- `student_id`
- `parent_id`
- `grade_id`
- `group_id`
- `prematriculation_period_id`
- `status`
- `failed_subjects_count`
- `academic_condition_valid`
- `payment_date`
- `matriculation_date`

### 3.2 Validaciones de prematricula

Validaciones encontradas:

- Periodo activo: `PrematriculationPeriodService.GetActivePeriodAsync`.
- Duplicado por periodo: `PrematriculationService.CreatePrematriculationAsync` busca prematricula activa para el mismo `student_id` y `prematriculation_period_id`.
- Documentos de nuevo ingreso: `ValidateRequiredDocumentsAsync`.
- Acudiente para menores: `ValidateParentRequiredAsync`.
- Condicion academica: `GetFailedSubjectsCountAsync` calcula promedios por materia desde `StudentActivityScores`; se permite si `failedSubjects <= 3`.
- Grado siguiente o repeticion: el controlador extrae numero del nombre del grado y bloquea retroceso o salto mayor a +1.
- Cupo de grupo: `CheckGroupCapacityAsync`.

Riesgo: estas validaciones son por grado numerico y conteo de materias reprobadas, no por prerrequisito materia-a-materia.

### 3.3 Conversion a matricula

La conversion se hace en `PrematriculationService.ConfirmMatriculationAsync`.

Evidencia de codigo:

- Rechaza prematriculas `Matriculado`, `Rechazado` o `Cancelado`.
- Exige grado y grupo; si faltan intenta asignarlos automaticamente.
- Revalida condicion academica.
- Exige pago confirmado y monto requerido si aplica.
- Inactiva matriculas activas previas del estudiante.
- Crea una nueva fila en `student_assignments` con:
  - `StudentId`
  - `GradeId`
  - `GroupId`
  - `ShiftId`
  - `IsActive = true`
  - `AcademicYearId = activeAcademicYear?.Id`
  - `CreatedAt`
- Cambia la prematricula a `Status = "Matriculado"` y registra historial en `prematriculation_histories`.

Conclusion: la prematricula no se convierte en un trimestre; se convierte en una matricula base en `student_assignments`.

## 4. Flujo de matricula/asignacion academica

### 4.1 Matricula base

`StudentAssignment` representa la matricula/asignacion base:

- `StudentId`
- `GradeId`
- `GroupId`
- `ShiftId`
- `AcademicYearId`
- `EnrollmentType`
- `IsActive`
- `StartDate`
- `EndDate`

La tabla `student_assignments` no tiene campo de trimestre.

### 4.2 Materias por nivel

La oferta de materias esta en `subject_assignments`, vinculada a:

- `subject_id`
- `grade_level_id`
- `group_id`
- `area_id`
- `specialty_id`

Cuando se crea una matricula base, `StudentAssignmentService.SyncStudentSubjectAssignmentsAsync` inscribe al estudiante en materias de ese `grade_id` y `group_id`, creando `student_subject_assignments`.

En modo nocturno avanzado, si no se especifica una materia explicita, el servicio puede no sincronizar automaticamente todas las materias. Esto permite inscripcion selectiva por materia.

### 4.3 Educacion nocturna avanzada

Evidencia:

- `Options/NocturnalAdvancedEnrollmentOptions.cs` define `EnableForAllSchools = true`.
- `appsettings.json` mantiene `"NocturnalAdvancedEnrollment": { "EnableForAllSchools": true }`.
- `EnrollmentTypeConstants` define `Regular`, `Nocturno`, `Refuerzo`, `Libre`.
- `DefaultPrimary = Nocturno`.
- `StudentAssignmentService.AddSubjectEnrollmentAsync` permite inscribir materias individuales y crear la matricula base si no existe.
- `StudentAssignmentController` expone `AddEnrollment`, `AddSubjectEnrollment` y carga masiva nocturna con tipos `Nocturno / Refuerzo`.

Conclusion: el sistema tiene soporte para multi-nivel/multi-grupo y arrastre por materia, pero ese soporte esta modelado por `EnrollmentType` y por `student_subject_assignments`, no por trimestres.

## 5. Trimestres

### 5.1 Modelo

`Trimester` contiene:

- `Id`
- `SchoolId`
- `Name`
- `Description`
- `Order`
- `StartDate`
- `EndDate`
- `IsActive`
- `AcademicYearId`

La tabla fisica se llama `trimester`.

### 5.2 Servicio

`TrimesterService` permite:

- Listar trimestres por escuela.
- Validar fechas, orden, solapamiento y consecutividad.
- Crear trimestres para la escuela actual.
- Editar fechas.
- Activar/desactivar.
- Validar si un trimestre esta activo.

Evidencia clave:

- `ValidateTrimesterActiveAsync(string trimesterName)` solo valida existencia y `IsActive`.
- `GuardarTrimestresAsync` crea registros de `Trimester`, no matriculas.
- `EliminarTodosLosTrimestresAsync` desvincula `activities.TrimesterId` antes de borrar trimestres. Esto confirma que la relacion fuerte del trimestre es con actividades.

### 5.3 Relacion con actividades y notas

`ActivityService.CreateAsync`:

- Valida trimestre activo.
- Busca trimestre por `Name` y `SchoolId`.
- Crea `Activity` con `Trimester = dto.TrimesterCode` y `TrimesterId = trimestre.Id`.

`StudentActivityScoreService.SaveBulkFromNotasAsync`:

- Valida que cada trimestre este activo.
- Busca el `Trimester` por codigo.
- Crea o reutiliza `Activity`.
- Guarda notas en `student_activity_scores` asociadas a `StudentAssignmentId` y opcionalmente `StudentSubjectAssignmentId`.

Conclusion: el trimestre delimita actividades y notas. No crea `student_assignments` ni `student_subject_assignments`.

### 5.4 Evidencia de BD

Consultas `SELECT` contra produccion:

- `trimester` tiene 3 registros:
  - `1T`, inactivo, `2026-03-09` a `2026-06-12`
  - `2T`, activo, `2026-06-23` a `2026-09-12`
  - `3T`, activo, `2026-09-22` a `2026-12-19`
- `activities` no tiene registros por `trimester` legado en produccion: 0 filas agrupadas.
- `subject_promotion_records` no tiene registros en produccion: 0 filas.
- `student_assignments` tiene registros activos e inactivos asociados a `academic_years`, no a trimestres.

## 6. Promocion

### 6.1 Modelo actual

La promocion implementada es por materia, no por grado anual completo:

`subject_promotion_records` contiene:

- `student_id`
- `subject_id`
- `grade_level_id`
- `academic_year_id`
- `trimester`
- `outcome`
- `final_score`
- `student_subject_assignment_id`
- `promoted_at`
- `school_id`

### 6.2 Servicio

`SubjectPromotionService.PromoteSubjectAsync`:

- Recibe estudiante, `studentSubjectAssignmentId`, trimestre, nota final y resultado.
- Si no llega resultado, calcula:
  - `Approved` si `finalScore >= 3.0`
  - `Failed` si `finalScore < 3.0`
- Crea `SubjectPromotionRecord`.
- Si aprueba:
  - cambia `StudentSubjectAssignment.Status = "Approved"`
  - `IsActive = false`
  - `EndDate = DateTime.UtcNow`
- Si reprueba:
  - cambia `Status = "Failed"`
  - si no es arrastre, cambia `EnrollmentType = Refuerzo`

`CloseYearForStudentAsync`:

- Toma todas las materias activas del estudiante.
- Calcula promedio de notas filtrando por `Activity.Trimester == trimester`.
- Aplica umbral `passingScore = 3.0`.
- Llama a `PromoteSubjectAsync`.

### 6.3 Que no existe

No se encontro un servicio de promocion por grado completo que:

- Cree automaticamente la matricula del siguiente nivel.
- Verifique prerrequisitos materia-a-materia.
- Bloquee inscripcion a una materia superior por no tener aprobada la anterior.
- Use asistencia minima para promocionar.

La asistencia se calcula/reporta en `AttendanceService`, pero no se encontro integracion con `SubjectPromotionService` para bloquear o aprobar promocion.

## 7. Prerrequisitos, secuencia de materias y requisitos academicos

### 7.1 Busqueda en codigo

Se buscaron terminos:

- `Prerequisite`
- `Requirement`
- `SubjectRequirement`
- `AcademicRequirement`
- `PromotionRule`
- `ValidationRule`
- `Curriculum`
- `Pensum`
- `Correlatividad`
- `Equivalencia`
- variantes en espanol

Resultado: no se encontro un modelo/servicio/controlador de prerrequisitos academicos. Las coincidencias relevantes se limitan a textos/documentacion o validaciones generales.

### 7.2 Busqueda en BD

Consulta a `information_schema`:

- Tablas coincidentes: solo `subject_promotion_records`.
- No existen tablas con nombres tipo `prerequisite`, `requirement`, `curriculum`, `pensum`, `correlatividad`, `equivalencia`.
- Columnas academicas cercanas:
  - `prematriculations.failed_subjects_count`
  - `prematriculations.academic_condition_valid`
  - `prematriculation_periods.required_amount`

Conclusion: no hay estructura persistente para prerrequisitos/correlatividades.

### 7.3 Validaciones reales que existen

Existen validaciones parciales:

- En prematricula: no saltar mas de un grado numerico.
- En prematricula: maximo 3 materias reprobadas.
- En matricula: evitar duplicados activos por grado/grupo/jornada/ano.
- En materias: evitar duplicar `student_subject_assignments` activos para la misma materia ofertada y ano.
- En promocion: aprobar/reprobar una materia segun nota >= 3.0.

Pero no existe una validacion del tipo:

> Para matricular Matematica 2 debe existir `SubjectPromotionRecord` Approved de Matematica 1.

## 8. Caso real: aprueba Matematica 1, abandona y vuelve por Matematica 2

### Pregunta

Alumno aprueba Matematica 1, abandona y meses despues quiere entrar directamente a Matematica 2.

### Que permite el sistema hoy

Depende del camino operativo:

1. Si entra por prematricula normal, el sistema valida grado numerico actual/siguiente y cantidad de materias reprobadas, pero no valida correlatividad de Matematica 1 -> Matematica 2.
2. Si un admin usa `StudentAssignment/AddEnrollment`, puede agregar una matricula activa a un grado/grupo, siempre que no exista duplicado activo en ese mismo grado/grupo/jornada.
3. Si se usa `AddSubjectEnrollment`, puede inscribir una materia ofertada si no esta ya activa para ese estudiante/ano; no consulta `subject_promotion_records` para validar materia previa.
4. Si se usa carga masiva nocturna, el sistema puede marcar materias de niveles inferiores como `Refuerzo` y el nivel principal como `Nocturno`, pero tampoco valida que la materia anterior haya sido aprobada.

### Donde validaria si existiera

La validacion deberia estar como minimo en:

- `StudentAssignmentService.AddSubjectEnrollmentAsync`
- carga masiva de `StudentAssignmentController`
- `PrematriculationService.CreatePrematriculationAsync`
- `PrematriculationService.ConfirmMatriculationAsync`

Hoy esas rutas no consultan una tabla de prerrequisitos ni exigen `SubjectPromotionRecord` anterior.

### Conclusion del caso

El sistema **probablemente permite** matricular/inscribir Matematica 2 sin una validacion formal de Matematica 1, salvo controles indirectos de grado numerico, cupo, duplicado y estado academico general. No hay bloqueo tecnico especifico por secuencia de materia.

## 9. Ingreso en segundo o tercer trimestre

### Caso A: estudiante nunca estuvo matriculado e ingresa en Segundo Trimestre

Si el estudiante no tiene `StudentAssignments` ni `StudentActivityScores`, `PrematriculationService.IsNewStudentAsync` lo considera nuevo. Para nuevo ingreso:

- No requiere validacion de condicion academica previa.
- Debe tener documentos completos.
- Si es menor, debe tener acudiente.
- Puede crear prematricula si hay periodo activo.
- Puede confirmarse matricula si tiene grado, grupo y pago confirmado.
- La confirmacion crea `student_assignments` con el ano academico activo, no con el trimestre activo.

Materias recibidas:

- En modo no avanzado, se sincronizan materias del grado/grupo.
- En modo nocturno avanzado, la asignacion puede ser selectiva por materia.

Notas faltantes:

- No se encontro codigo que calcule, complete o impute notas faltantes de trimestres anteriores.
- La promocion/cierre calcula promedios con actividades del trimestre indicado. Si no hay actividades/notas, `DefaultIfEmpty().AverageAsync()` puede producir promedio 0 para ese conjunto, y el resultado seria `Failed` si se cierra ese trimestre para la materia.

### Ingreso en tercer trimestre

La conclusion es la misma: la matricula se crea contra `academic_year_id` y fecha de inicio, no contra `trimester_id`. El sistema puede registrar al estudiante mientras haya flujo operativo de prematricula/matricula o asignacion administrativa, pero no genera automaticamente equivalencias/notas faltantes de `1T` o `2T`.

## 10. Mapa relacional de BD

Relaciones principales confirmadas por `information_schema`:

- `prematriculations.school_id` -> `schools.id`
- `prematriculations.student_id` -> `users.id`
- `prematriculations.parent_id` -> `users.id`
- `prematriculations.grade_id` -> `grade_levels.id`
- `prematriculations.group_id` -> `groups.id`
- `prematriculations.prematriculation_period_id` -> `prematriculation_periods.id`
- `prematriculation_histories.prematriculation_id` -> `prematriculations.id`
- `student_assignments.student_id` -> `users.id`
- `student_assignments.grade_id` -> `grade_levels.id`
- `student_assignments.group_id` -> `groups.id`
- `student_assignments.academic_year_id` -> `academic_years.id`
- `student_subject_assignments.student_id` -> `users.id`
- `student_subject_assignments.subject_assignment_id` -> `subject_assignments.id`
- `student_subject_assignments.student_assignment_id` -> `student_assignments.id`
- `student_subject_assignments.academic_year_id` -> `academic_years.id`
- `subject_assignments.subject_id` -> `subjects.id`
- `subject_assignments.grade_level_id` -> `grade_levels.id`
- `subject_assignments.group_id` -> `groups.id`
- `activities.TrimesterId` -> `trimester.id`
- `activities.subject_id` -> `subjects.id`
- `activities.group_id` -> `groups.id`
- `student_activity_scores.activity_id` -> `activities.id`
- `student_activity_scores.student_assignment_id` -> `student_assignments.id`
- `student_activity_scores.student_subject_assignment_id` -> `student_subject_assignments.id`
- `student_activity_scores.academic_year_id` -> `academic_years.id`
- `subject_promotion_records.student_subject_assignment_id` -> `student_subject_assignments.id`
- `subject_promotion_records.academic_year_id` -> `academic_years.id`
- `subject_promotion_records.subject_id` -> `subjects.id`
- `subject_promotion_records.grade_level_id` -> `grade_levels.id`

Indices/restricciones relevantes:

- `uq_student_assignments_active_enrollment`: unico activo por `student_id`, `grade_id`, `group_id`, `shift_id`, `academic_year_id`.
- `ix_student_subject_assignments_active_unique`: unico activo por `student_id`, `subject_assignment_id`, `academic_year_id`.
- `trimester_name_school_key`: unico por `name`, `school_id`.
- `IX_subject_promotion_records_student_subject_year_trimester`: indice por estudiante, materia, ano y trimestre; no es unico.

## 11. Evidencia de produccion

Consultas ejecutadas solo con `SELECT` y transaccion `READ ONLY`.

Resultados relevantes:

- `prematriculations`: 0 filas por estado en produccion consultada.
- `prematriculation_periods`: 0 filas en produccion consultada.
- `student_assignments`: 349 activas y 280 inactivas para `academic_year` 2026.
- `student_subject_assignments`:
  - `Active / Nocturno`: 159
  - `Active / Refuerzo`: 121
  - `Active / Regular`: 11
  - `Inactive / Nocturno`: 27
  - `Inactive / Refuerzo`: 1
- `trimester`: 3 periodos (`1T`, `2T`, `3T`).
- `subject_promotion_records`: 0 filas.
- `academic_years`: registros activos 2026 por escuela.
- Tablas de prerrequisitos/curriculo/correlatividad/equivalencia: no encontradas.

Interpretacion: la produccion actual ya usa matriculas e inscripciones por materia para 2026, pero no tiene datos de prematricula ni promocion por materia registrados en las tablas revisadas.

## 12. Riesgos identificados

### 12.1 Que impide avance incorrecto hoy

- Validacion de grado numerico en prematricula: mismo grado o siguiente grado.
- Maximo 3 materias reprobadas para prematricula/confirmacion.
- Duplicados activos bloqueados por indice unico de matricula.
- Duplicados activos de materias bloqueados por indice unico de `student_subject_assignments`.
- Promocion por materia puede marcar `Approved` o `Failed`.

### 12.2 Que permitiria saltarse niveles

- `AddEnrollment` y `AddSubjectEnrollment` no validan prerrequisitos materia-a-materia.
- La carga masiva puede crear matriculas/materias segun Excel sin consultar una malla de correlatividades.
- `subject_promotion_records` existe, pero no es consultada como requisito de inscripcion.
- No hay tabla de malla curricular secuencial.

### 12.3 Entrada en segundo trimestre

- Puede operar como matricula nueva anual durante el ano academico.
- No se crean automaticamente notas previas.
- No hay equivalencias automaticas.
- El cierre de trimestre podria reprobar materias sin notas si se procesa sin datos suficientes.

### 12.4 Entrada en tercer trimestre

- Mismo riesgo que segundo trimestre, agravado por mas periodos sin evidencia de notas.
- No hay mecanismo formal para reconocer o completar `1T` y `2T`.

### 12.5 Estudiante sin materias previas

- Si es nuevo, no se exige historial academico.
- Puede recibir materias del grado/grupo o materias selectivas.
- No se bloquea por no haber aprobado materias anteriores.

### 12.6 Cambio de escuela u otra institucion

- Multi-tenant filtra por `SchoolId`.
- No se encontro modulo de equivalencias externas.
- No hay tabla para convalidar materias aprobadas en otra institucion.
- El sistema podria registrar al estudiante y asignarle materias, pero sin trazabilidad academica formal de equivalencias.

## 13. Conclusiones directas

1. **Cada trimestre es una matricula?**  
   No. El trimestre es un periodo academico/evaluativo. La matricula vive en `student_assignments` y `student_subject_assignments`, asociadas a `academic_year_id`, no a `trimester_id`.

2. **Existe una sola matricula anual?**  
   Existe una matricula base por ano academico, grado, grupo y jornada. En educacion nocturna avanzada puede haber multiples matriculas activas o materias individuales, pero siguen estando dentro del ano academico, no del trimestre.

3. **Puede entrar un estudiante nuevo en Segundo Trimestre?**  
   Si operativamente se le crea prematricula/matricula o asignacion administrativa, si. El sistema no lo impide por trimestre. Pero no genera automaticamente notas/equivalencias de `1T`.

4. **Puede entrar un estudiante nuevo en Tercer Trimestre?**  
   Si, por la misma razon. No hay bloqueo tecnico por trimestre ni calculo automatico de notas faltantes.

5. **Existen prerrequisitos academicos?**  
   No se encontro soporte tecnico formal para prerrequisitos/correlatividades/malla secuencial.

6. **Como se controlan?**  
   No se controlan materia-a-materia. Solo hay controles indirectos: grado numerico, maximo de materias reprobadas, duplicados, cupos y aprobacion/reprobacion de materias inscritas.

7. **Existe promocion automatica?**  
   Existe cierre/promocion por materia en `SubjectPromotionService.CloseYearForStudentAsync`, basado en promedio por trimestre y nota minima 3.0. No se encontro promocion automatica integral de grado ni creacion automatica del siguiente nivel.

8. **Que cambios serian necesarios para soportar educacion nocturna por modulos?**  
   Serian necesarios, al menos:
   - Definir una malla curricular por modulo/nivel/materia.
   - Crear tabla de prerrequisitos/correlatividades.
   - Asociar inscripcion por materia a modulo/periodo de ingreso, no solo ano academico.
   - Registrar equivalencias externas/convalidaciones.
   - Validar `AddSubjectEnrollment`, carga masiva y prematricula contra prerrequisitos.
   - Definir politica para ingreso tardio: notas faltantes, equivalencias, exoneraciones o modulo inicial.
   - Integrar asistencia minima si es regla academica real.
   - Definir promocion de modulo/nivel basada en todas las materias requeridas aprobadas.

## 14. Veredicto final

El modelo actual de Eduplaner Noche **no modela el trimestre como matricula independiente**. Modela:

1. `academic_years` como contenedor anual.
2. `student_assignments` como matricula/asignacion base al grado/grupo/jornada.
3. `student_subject_assignments` como inscripcion individual a materias, util para nocturna y arrastres.
4. `trimester` como periodo evaluativo para actividades/notas/promocion.

Por tanto, si la institucion nocturna necesita operar por modulos donde un estudiante puede entrar a `2T` o `3T` y cursar solo una secuencia validada de materias, el sistema requiere una capa formal de malla modular, prerrequisitos, equivalencias y reglas de avance. Hoy el sistema permite la operacion administrativa, pero no garantiza academicamente que el estudiante no salte niveles o materias previas.
