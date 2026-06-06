# Auditoría arquitectónica y funcional – Compatibilidad con educación nocturna

**Proyecto:** `SchoolManager` (EduplanerNoche)  
**Stack:** ASP.NET Core 8, EF Core, PostgreSQL (Render – producción `schoolmanager_daqf`)  
**Fecha:** 25 de mayo de 2026  
**Alcance:** Solo análisis — sin cambios en código, modelos, migraciones ni datos.

---

## 1. Resumen ejecutivo

SchoolManager fue diseñado principalmente para **educación regular por grado y grupo**, con evolución reciente hacia **matrícula nocturna simple** (un grado + un grupo + jornada Noche + `EnrollmentType = Nocturno`).

La **base de datos y el modelo de matrícula** ya permiten escenarios más complejos (multi-matrícula, inscripción por materia vía `student_subject_assignments`), pero **múltiples módulos de negocio siguen asumiendo un contexto académico único** (un grado/grupo “principal”) o analizan resultados **por grupo**, no por trayectoria individual de arrastre.

| Dimensión | Conclusión |
|-----------|------------|
| **Compatibilidad global estimada con educación nocturna panameña (modelo completo)** | **~55% – Parcial** |
| **Operación actual en producción (1 matrícula nocturna por estudiante)** | **Viable con limitaciones** |
| **Riesgo de adaptación** | **Medio–Alto** (modelo completo) / **Medio** (uso actual) |
| **¿Operar hoy sin modificaciones?** | **NO** para el modelo nocturno completo; **SÍ con restricciones** para el patrón ya desplegado (297/298 matrículas `Nocturno`, una por estudiante) |

---

## 2. Arquitectura actual

### 2.1 Cadena académica principal

```
User (estudiante)
  └── StudentAssignment (matrícula: grado + grupo + jornada + año + tipo)
        └── StudentSubjectAssignment (inscripción por materia ofertada)
              └── SubjectAssignment (materia + grado + grupo + área + especialidad)
                    └── TeacherAssignment (docente → oferta)
                          └── Activity / ScheduleEntry
                                └── StudentActivityScore (nota)
                                └── Attendance (asistencia por grupo, no por materia)
```

**Evidencia:**  
- `Models/StudentAssignment.cs`, `Models/StudentSubjectAssignment.cs`, `Models/SubjectAssignment.cs`  
- `Services/Implementations/StudentAssignmentService.cs` → `SyncStudentSubjectAssignmentsAsync` (líneas 25–66)

### 2.2 Dimensiones temporales

| Concepto | Existe | Evidencia |
|----------|--------|-----------|
| Año lectivo | Sí | `Models/AcademicYear.cs`, tabla `academic_years` (prod: `2026` activo) |
| Trimestre | Sí | `Models/Trimester.cs`, tabla `trimester` (prod: **3** trimestres) |
| Semestre | **No** | Sin entidad `Semester` en modelos |
| Módulo académico | **No** | Sin entidad de módulo ligada a matrícula |
| Período prematrícula | Sí | `Models/PrematriculationPeriod.cs` (ventana de inscripción, no período lectivo modular) |

### 2.3 Jornadas y tipos de matrícula

| Concepto | Evidencia |
|----------|-----------|
| Jornadas | `Models/Shift.cs` — prod: `Mañana`, `Tarde`, `Noche` |
| Tipo matrícula | `StudentAssignment.EnrollmentType` — valores documentados: `"Regular"`, `"Nocturno"`, `"Refuerzo"`, `"Libre"` (`Models/StudentAssignment.cs`, `SchoolDbContext.cs` ~1016) |
| Detección nocturna | `AprobadosReprobadosService.GetNightShiftIdsAsync`, `ActiveStudentAssignmentHelper`, `StudentAssignmentController` (asigna `Nocturno` si jornada nocturna) |

### 2.4 Producción (solo lectura – Render)

| Métrica | Valor | Consulta / fuente |
|---------|-------|-------------------|
| Matrículas activas | 298 | `student_assignments WHERE is_active = true` |
| Estudiantes con matrícula activa | 298 | Mismo conteo → **1 matrícula activa por estudiante hoy** |
| Estudiantes con >1 matrícula activa | **0** | `GROUP BY student_id HAVING COUNT(*) > 1` |
| Tipo `Nocturno` | 297 | `enrollment_type` |
| Tipo `Regular` | 1 | `enrollment_type` |
| Inscripciones por materia activas | 13 (3 estudiantes) | `student_subject_assignments` |
| Estudiantes con materias en >1 grado (SSA) | **0** | JOIN `subject_assignments.grade_level_id` |
| Ofertas académicas (`subject_assignments`) | 407 | Catálogo grado+grupo+materia |
| Grupos | 23 | Mezcla de convenciones (`10-A`, `A1`, `7-A`, etc.) |

**Conclusión de prod:** el despliegue actual usa **matrícula nocturna homogénea (1:1 estudiante–matrícula)**; las capacidades multi-nivel/multi-grupo **existen en código pero no se usan operativamente**.

---

## 3. Restricciones únicas (PostgreSQL + EF)

### 3.1 Matrícula (`student_assignments`)

| Restricción | Definición | Impacto nocturno |
|-------------|------------|------------------|
| `uq_student_assignments_active_enrollment` | `UNIQUE (student_id, grade_id, group_id, shift_id, academic_year_id) WHERE is_active = true` NULLS NOT DISTINCT | Permite **varias matrículas activas** si difiere grado, grupo, jornada o año. **Impide** duplicar la misma combinación. |
| EF | `SchoolDbContext.cs` ~1029–1031 | |
| Migración | `Migrations/20260418204938_UqActiveStudentAssignmentEnrollment.cs` | |

### 3.2 Inscripción por materia (`student_subject_assignments`)

| Restricción | Definición | Impacto |
|-------------|------------|---------|
| `ix_student_subject_assignments_active_unique` | `UNIQUE (student_id, subject_assignment_id, academic_year_id) WHERE is_active = true` | Un estudiante no puede tener dos inscripciones activas a la **misma oferta** en el mismo año. **Sí puede** inscribirse en ofertas distintas (distinto grado/grupo/materia). |

### 3.3 Notas (`student_activity_scores`)

| Restricción | Definición |
|-------------|------------|
| `uq_scores_assignment_activity` | `UNIQUE (student_assignment_id, activity_id)` |
| `uq_scores_subject_enrollment_activity` | `UNIQUE (student_subject_assignment_id, activity_id)` |

**Evidencia:** `Models/StudentActivityScore.cs`, `SchoolDbContext.cs` ~901–903

### 3.4 Asistencia (`attendance`)

| Restricción | Definición |
|-------------|------------|
| `ix_attendance_student_date_group_grade_shift` | `UNIQUE (student_id, date, group_id, grade_id, shift_id)` |

**No incluye `subject_id`.** Asistencia = **estudiante + grupo + grado + fecha + jornada**.

**Evidencia:** `Models/Attendance.cs`, `AttendanceService.cs` ~188–193

### 3.5 Impartición docente (`teacher_assignments`)

| Restricción | Definición |
|-------------|------------|
| `teacher_assignments_teacher_id_subject_assignment_id_key` | `UNIQUE (teacher_id, subject_assignment_id)` |

Permite al docente tener **múltiples asignaciones** (una por cada `SubjectAssignment` / grado / grupo).

### 3.6 Oferta académica (`subject_assignments`)

**No hay UNIQUE** en `(subject_id, grade_level_id, group_id)` — solo índices no únicos (`SchoolDbContext.cs` ~1150–1158).

---

## 4. Validación de escenarios obligatorios

### ESCENARIO 1 – Materias de distintos niveles

**Caso:** Pedro – Matemática 11°, Español 11°, Historia 10° (pendiente), Inglés 9° (pendiente).

| Capacidad | ¿Soportado? | Evidencia |
|-----------|-------------|-----------|
| Inscribir materias de distintos grados | **Parcial – Sí a nivel datos** | `StudentSubjectAssignment` → `SubjectAssignment.GradeLevelId` distinto por fila |
| UI/API de inscripción individual | **Parcial** | `StudentAssignmentController.AddSubjectEnrollment` (POST `/StudentAssignment/AddSubjectEnrollment`) |
| Requiere matrícula base | **Sí** | Si no hay `StudentAssignment` con mismo grado+grupo, usa la matrícula activa más reciente (líneas 313–326) — **riesgo de vincular arrastre al contexto equivocado** |
| Auto-inscripción al matricular | **Sobrecarga** | `SyncStudentSubjectAssignmentsAsync` inscribe **todas** las materias del grado+grupo (`StudentAssignmentService.cs` 27–35) |
| Notas en gradebook | **Parcial** | Con `subjectId`: `StudentService.GetBySubjectGroupAndGradeAsync` filtra por `StudentSubjectAssignments` (`StudentService.cs` 84–99) |
| Prod multi-grado | **No usado** | 0 estudiantes con SSA en >1 grado |

**Veredicto escenario 1:** **Parcialmente compatible** — posible con inscripciones manuales por materia y catálogo `subject_assignments` por cada grado/grupo, pero **sin flujo nativo de “materia pendiente / arrastre”** y con riesgo en el fallback de matrícula base.

---

### ESCENARIO 2 – Múltiples grupos

**Caso:** Pedro – Grupo A (Matemática), Grupo B (Inglés).

| Capacidad | ¿Soportado? | Evidencia |
|-----------|-------------|-----------|
| Dos matrículas activas (grupos distintos) | **Sí (BD)** | `uq_student_assignments_active_enrollment` permite distinto `group_id` |
| Flujo UI additive | **Sí** | `StudentAssignmentController.AddEnrollment`, flag `additive` en `UpdateGroupAndGrade` |
| Flujo replace (default histórico) | **No** | `UpdateGroupAndGrade` sin `additive` inactiva todas las matrículas previas |
| Horario estudiante | **Sí multi-grupo** | `ScheduleService.GetByStudentUserAsync` — merge de todas las matrículas activas |
| Boletín estudiante | **Sí multi-grupo** | `StudentReportService.GetActiveEnrollmentGroupsAsync` — une grupos activos |
| Asistencia | **Por grupo** | Una fila por grupo/fecha — compatible si Pedro asiste en A y B por separado |
| Prod | **No usado** | 0 estudiantes con >1 matrícula activa |

**Veredicto escenario 2:** **Parcialmente compatible** — arquitectura lo permite; operación y pantallas administrativas aún favorecen **una matrícula principal** (`ActiveStudentAssignmentHelper.PickForDisplay`).

---

### ESCENARIO 3 – Materias pendientes (arrastre)

**Caso:** Nivel actual 11°; pendientes Historia 10°, Inglés 9°.

| Área | ¿Soporta arrastre? | Evidencia |
|------|-------------------|-----------|
| Inscripción | **Parcial** | `EnrollmentType` admite `Refuerzo`/`Libre` en modelo; no hay wizard “materia pendiente” |
| Asistencia | **Por grupo, no por materia pendiente** | `Attendance` sin `SubjectId` |
| Calificaciones | **Por actividad en contexto grado+grupo+materia** | `Activity` requiere `GroupId`, `GradeLevelId`, `SubjectId` |
| Actividades | **Por clase** | `ActivityService` filtra teacher+group+grade+subject+trimester |
| Reportes / boletín | **Parcial multi-grupo** | `StudentReportService` — sí; sin etiqueta “pendiente/arrastre” |
| AprobadosReprobados | **Por grupo + trimestre** | `AprobadosReprobadosService.CalcularEstadisticasGrupoAsync` — promedio por materia dentro del grupo analizado |
| Portal estudiante | **Parcial** | Mis calificaciones: multi-grupo; perfil: `FirstOrDefault` en matrícula |
| Portal padres | **No académico** | `ClubParents` = pagos/carnet; sin calificaciones para `parent`/`acudiente` |
| Promoción parcial | **No implementada** | `Docs/prematriculation_apply_academic_year_changes_analysis.md`: “No hay lógica de promoción”; `RECOMENDACIONES_GESTION_GRADOS.md` solo propuesta |

**Veredicto escenario 3:** **Parcial / No nativo** — se puede simular con SSA + catálogo, pero **no hay semántica de arrastre, promoción parcial ni reportes de pendientes**.

---

### ESCENARIO 4 – Profesor multinivel

**Caso:** Matemática 9°, 10°, 11° y nocturna.

| Capacidad | ¿Soportado? | Evidencia |
|-----------|-------------|-----------|
| Múltiples asignaciones docente | **Sí** | `TeacherAssignment` — una fila por `SubjectAssignment` |
| UNIQUE evita duplicar misma oferta | **Sí** | `(teacher_id, subject_assignment_id)` |
| Agrupación en servicio | **Sí** | `AcademicAssignmentService.GetAssignmentsByTeacherAsync` agrupa por materia+grado con lista de grupos |
| Gradebook | **Un contexto a la vez** | `TeacherGradebookController.Index` — docente elige materia+grupo+grado+trimestre |

**Veredicto escenario 4:** **Compatible** para impartición; la limitación es de **UX por sesión**, no de modelo.

---

### ESCENARIO 5 – Materia independiente del grado

**Pregunta:** ¿Obliga Estudiante → Grado → Grupo → Materia o permite Estudiante → Materia?

| Respuesta | Evidencia |
|-----------|-----------|
| **Obliga cadena completa en la práctica** | `SubjectAssignment` siempre incluye `GradeLevelId` + `GroupId` + `SubjectId` (`Models/SubjectAssignment.cs`) |
| Inscripción estudiante | `StudentSubjectAssignment.SubjectAssignmentId` → oferta académica acoplada a grado y grupo |
| Atajo matrícula | `SyncStudentSubjectAssignmentsAsync` deriva materias del par grado+grupo de `StudentAssignment` |
| **No existe** matrícula directa Estudiante → Materia sin grado/grupo | Sin tabla ni servicio equivalente |

**Veredicto escenario 5:** **No compatible** con modelo Estudiante → Materia puro; siempre mediado por **oferta académica contextualizada**.

---

## 5. Análisis por módulo

### 5.1 StudentAssignment

| Aspecto | Detalle |
|---------|---------|
| Controlador | `Controllers/StudentAssignmentController.cs` |
| Servicio | `Services/Implementations/StudentAssignmentService.cs` |
| Vista | `Views/StudentAssignment/Index.cshtml` |
| Compatibilidad | **Parcial** |
| Evidencia positiva | `AddEnrollment`, `AddSubjectEnrollment`, `EnrollmentType=Nocturno`, filtros grado/grupo/jornada |
| Evidencia negativa | `UpdateGroupAndGrade` replace por defecto; sync masivo de materias al matricular; prod 1:1 matrícula |

### 5.2 SubjectAssignment / AcademicCatalog / AcademicAssignment

| Módulo | Archivos | Compatibilidad |
|--------|----------|----------------|
| Catálogo | `AcademicCatalogController.cs`, `ViewModels/AcademicCatalogViewModel.cs` | **Parcial** — carga masiva grado+grupo+materia |
| Asignación docente | `AcademicAssignmentController.cs`, `AcademicAssignmentService.cs` | **Compatible** multinivel |
| Oferta | `SubjectAssignment` | **Parcial** — requiere grado+grupo por materia |

### 5.3 TeacherGradebook

| Aspecto | Detalle |
|---------|---------|
| Controlador | `Controllers/TeacherGradebookController.cs` (no existe `TeacherGradebookService`) |
| Servicios | `StudentActivityScoreService`, `ActivityService`, `AttendanceService` |
| Clave de notas | `StudentAssignmentId` + `ActivityId`; opcional `StudentSubjectAssignmentId` |
| Roster con materia | `GetBySubjectGroupAndGradeAsync` — usa SSA |
| Compatibilidad | **Parcial** — soporta clase con inscripción por materia; sesión única; fallback sin materia usa solo grupo+grado |

### 5.4 Attendance

| Aspecto | Detalle |
|---------|---------|
| Servicio | `Services/Implementations/AttendanceService.cs` |
| Clave | Estudiante + Grupo + Grado + Fecha + Jornada |
| Compatibilidad | **Parcial** — multi-grupo posible; **no por materia**; impacto nocturno: arrastre en otro grupo requiere registrar asistencia en ese grupo |

### 5.5 Activities

| Aspecto | Detalle |
|---------|---------|
| Modelo | `Models/Activity.cs` |
| Servicio | `ActivityService.cs` — `TeacherId + SubjectId + GroupId + GradeLevelId + Trimester` |
| Compatibilidad | **Parcial** — alineado a clase regular; no a módulos flexibles |

### 5.6 Student Portal

| Función | Archivo | Multi-grupo / multi-nivel |
|---------|---------|---------------------------|
| Mis calificaciones | `StudentReportController` / `StudentReportService` | **Sí** — merge matrículas activas |
| Mi horario | `StudentScheduleController` / `ScheduleService` | **Sí** |
| Mi perfil | `StudentProfileService` | **No** — `FirstOrDefaultAsync` en matrícula |
| Menú | `_AdminLayout.cshtml` | Rol `student`/`estudiante` |

### 5.7 Parent Portal

| Hallazgo | Evidencia |
|----------|-----------|
| Sin portal de calificaciones padres | No hay controlador de notas para `parent`/`acudiente` |
| Club de Padres | `ClubParentsController`, `ClubParentsPaymentService` — pagos/carnet |
| Una sola matrícula mostrada | `ClubParentsPaymentService` — `FirstOrDefault` ordenado preferiendo Nocturno |
| Compatibilidad | **No compatible** para seguimiento académico multi-nivel |

### 5.8 Reportes

| Reporte | Servicio / controlador | Agrupación | Multi-nivel |
|---------|------------------------|------------|-------------|
| AprobadosReprobados | `AprobadosReprobadosService` | Por **grupo** + trimestre | Parcial — estudiante una vez por informe de grupo |
| Boletín estudiante | `StudentReportService` | Por estudiante, actividades en grupos activos | **Parcial – mejor soporte** |
| Report Cards | Equivalente a boletín anterior | — | Parcial |
| Transcripts | No identificado módulo dedicado | — | **No verificado / probablemente no** |

### 5.9 Dashboard

| Hallazgo | Evidencia |
|----------|-----------|
| Dashboard director | `DirectorController`, `DirectorService.GetDashboardViewModelAsync` | Métricas por trimestre escolar — **no verificado multi-matrícula** |
| Dashboard planes de trabajo | `DirectorWorkPlansController` | Ámbito planes docentes |

**Compatibilidad Dashboard:** **Parcial** (sin evidencia de soporte arrastre).

---

## 6. Matriz de compatibilidad por módulo

| Módulo | Compatible | Parcial | No Compatible | Evidencia clave | Observaciones |
|--------|:----------:|:-------:|:-------------:|-----------------|---------------|
| StudentAssignment | | ✓ | | `StudentAssignmentController`, `uq_student_assignments_active_enrollment` | Multi-matrícula en BD; UI mixta replace/additive |
| TeacherAssignment | ✓ | | | `TeacherAssignment`, `AcademicAssignmentService` | Docente multinivel OK |
| TeacherGradebook | | ✓ | | `TeacherGradebookController`, `StudentActivityScore` | Una clase por sesión; SSA cuando hay materia |
| Attendance | | ✓ | | `AttendanceService`, UNIQUE por grupo | No por materia |
| Activities | | ✓ | | `ActivityService` | Clase fija grado+grupo+materia |
| Student Portal | | ✓ | | `StudentReportService` vs `StudentProfileService` | Calificaciones sí; perfil no |
| Parent Portal | | | ✓ | `ClubParentsController` | Sin notas; una matrícula en listados |
| Reportes / Boletín | | ✓ | | `StudentReportService`, `AprobadosReprobadosService` | Boletín mejor que reportes institucionales |
| AprobadosReprobados | | ✓ | | Por grupo, trimestre, promedio ≥3.0 | No promoción parcial |
| Dashboard | | ✓ | | `DirectorController` | Sin evidencia arrastre |

---

## 7. Compatibilidad por modelo académico futuro

| Modelo académico | Compatible | Observaciones |
|------------------|:----------:|---------------|
| Educación Regular | **Sí (~90%)** | Diseño original; trimestres; grado+grupo+materia |
| Educación Nocturna | **Parcial (~55%)** | Jornada Noche + `Nocturno` implementados; arrastre/multi-grupo no operativos en prod |
| Educación Modular | **No (~15%)** | Sin entidad módulo ni períodos modulares |
| Educación Semestral | **Parcial (~35%)** | Solo `AcademicYear` + `Trimester` (3T); no semestres |
| Educación por Competencias | **No (~10%)** | Sin competencias, evidencias ni rutas de aprendizaje |

---

## 8. Promoción académica

| Pregunta | Respuesta | Evidencia |
|----------|-----------|-----------|
| ¿Promoción por grado completo? | **Manual / no automatizada** | `RECOMENDACIONES_GESTION_GRADOS.md` propone servicio; no implementado |
| ¿Promoción por materia? | **No** | Sin servicio ni tablas de historial de promoción |
| ¿Aprobación parcial (9 de 11)? | **No** | AprobadosReprobados evalúa por grupo/trimestre, nota mínima 3.0 (`NOTA_MINIMA_APROBACION`) |
| ¿Avanzar de nivel con pendientes? | **No soportado explícitamente** | Requeriría multi-matrícula + arrastre + promoción — no integrado |

---

## 9. Consultas que asumen un solo grado/grupo

| Ubicación | Patrón | Impacto |
|-----------|--------|---------|
| `StudentProfileService.GetStudentProfileAsync` | `StudentAssignments...FirstOrDefaultAsync()` | Perfil muestra un grado/grupo arbitrario |
| `ClubParentsPaymentService.GetStudentsAsync` | `FirstOrDefault` matrícula activa | Un contexto en listado padres |
| `ActiveStudentAssignmentHelper.PickForDisplay` | Elige matrícula “primaria” | Carnet/listados ignoran secundarias |
| `StudentAssignmentController.UpdateGroupAndGrade` (default) | Inactiva todas + una nueva | Destruye multi-matrícula |
| `AprobadosReprobadosService` | Estudiantes por `GroupId` | No consolida trayectoria multi-grupo |
| `SyncStudentSubjectAssignmentsAsync` | Todas las materias del grado+grupo | No selectivo para arrastre |

**Multi-matrícula consciente (evidencia positiva):**  
`StudentReportService.GetActiveEnrollmentGroupsAsync`, `ScheduleService.GetByStudentUserAsync`, `StudentAssignmentController.GetGradeGroupByStudent` (retorna array).

---

## 10. Tablas involucradas

| Tabla | Rol en nocturna |
|-------|-----------------|
| `users` | Estudiantes, docentes |
| `student_assignments` | Matrícula grado+grupo+jornada+año+tipo |
| `student_subject_assignments` | Inscripción por materia ofertada |
| `subject_assignments` | Oferta materia+grado+grupo |
| `teacher_assignments` | Docente → oferta |
| `activities` | Evaluaciones por clase |
| `student_activity_scores` | Notas |
| `attendance` | Asistencia por grupo |
| `grade_levels` | Grados |
| `groups` | Grupos (campo `Grade` string + `ShiftId`) |
| `shifts` | Jornadas |
| `academic_years` | Años lectivos |
| `trimester` | Períodos trimestrales |
| `subjects` | Catálogo de materias |

**No verificado en esta auditoría:** stored procedures dedicados; vistas SQL materializadas académicas (no encontradas en exploración de código).

---

## 11. Controladores y servicios involucrados

| Área | Controladores | Servicios |
|------|---------------|-----------|
| Matrícula | `StudentAssignmentController` | `StudentAssignmentService` |
| Catálogo | `AcademicCatalogController` | (inline / import) |
| Asignación docente | `AcademicAssignmentController` | `AcademicAssignmentService` |
| Notas | `TeacherGradebookController` | `StudentActivityScoreService`, `ActivityService` |
| Asistencia | `AttendanceController` | `AttendanceService` |
| Boletín | `StudentReportController` | `StudentReportService` |
| Aprobados/Reprobados | (controller dedicado) | `AprobadosReprobadosService` |
| Estudiante perfil | `StudentProfileController` | `StudentProfileService` |
| Padres | `ClubParentsController` | `ClubParentsPaymentService` |
| Horario | `StudentScheduleController` | `ScheduleService` |

---

## 12. Riesgos

| Riesgo | Nivel | Descripción |
|--------|-------|-------------|
| Operar arrastre sin SSA correcta | **Alto** | Fallback de matrícula en `AddSubjectEnrollment` |
| Replace accidental de matrícula | **Medio** | Flujos default que inactivan todas las matrículas |
| Asistencia sin materia | **Medio** | No distingue arrastre vs regular en misma jornada |
| Datos duplicados grupo/grado | **Medio** | `Group.Grade` string vs `GradeLevel`; prod con grupos `A1` sin jornada |
| Promoción manual | **Alto** | Sin automatización fin de año |
| Portal padres sin calificaciones | **Medio** | Expectativa de acudiente no cubierta |
| Sobrescritura sync materias | **Medio** | Matricular grado+grupo inscribe todas las materias |

---

## 13. Limitaciones actuales

1. **No hay semestres ni módulos** académicos estructurados.  
2. **No hay promoción por materia** ni historial de arrastre.  
3. **SubjectAssignment** siempre acopla materia a grado y grupo.  
4. **Asistencia** no es por materia.  
5. **Varios módulos** muestran un solo grado/grupo (`FirstOrDefault`).  
6. **Producción actual** no ejerce multi-matrícula ni multi-grado en SSA.  
7. **Portal padres** no es portal académico.  
8. **Competencias** no modeladas.

---

## 14. Recomendaciones futuras (solo orientación — sin implementar)

1. Formalizar **`EnrollmentType = Refuerzo/Libre`** en UI y reportes como “materia pendiente”.  
2. Eliminar fallback ambiguo en `AddSubjectEnrollment`; exigir matrícula coherente grado+grupo por arrastre.  
3. Extender asistencia opcional por `SubjectAssignmentId` o SSA.  
4. Unificar **`Group.Grade`** con `GradeLevelId` FK.  
5. Implementar **promoción por materia** y cierre de año.  
6. Portal padres académico reutilizando `StudentReportService`.  
7. Perfil estudiante y carnet: mostrar **todas** las matrículas activas, no solo `PickForDisplay`.  
8. Reportes institucionales consolidados por estudiante multi-grupo.

---

## 15. Análisis de impacto

| Dimensión | Nivel compatibilidad | Riesgo | Complejidad adaptación | Impacto |
|-----------|---------------------|--------|------------------------|---------|
| **Global nocturna completa** | Parcial (~55%) | Medio–Alto | Alta | BD media; Backend alta; Frontend media; Reportes alta |
| **Uso actual prod (1 matrícula nocturna)** | ~75% operativo | Medio | Media | Principalmente procesos y catálogo |
| Base de datos | Parcial | Medio | Media | Índices ya orientados a multi-matrícula |
| Backend | Parcial | Alto | Alta | Muchos servicios con supuesto único |
| Frontend | Parcial | Medio | Media | StudentAssignment mejorado; otros pendientes |
| Reportes | Parcial | Alto | Alta | AprobadosReprobados por grupo |
| Seguridad | Compatible | Bajo | Baja | Sin hallazgo bloqueante |
| Integridad datos | Parcial | Medio | Media | Sync materias + replace matrícula |

---

## 16. Conclusión técnica

SchoolManager **evolucionó** desde un modelo estrictamente regular hacia soporte de **jornada nocturna y matrícula tipada `Nocturno`**, con infraestructura de **multi-matrícula e inscripción por materia** en base de datos y en `StudentAssignment`. Sin embargo, **la operación en producción sigue el patrón 1 estudiante = 1 matrícula nocturna**, y módulos críticos (asistencia, promoción, padres, aprobados/reprobados, perfil) **no implementan el modelo nocturno panameño completo** (arrastre, multi-grupo, multi-nivel simultáneo, promoción parcial).

El sistema **puede funcionar hoy** para una escuela nocturna que opere **como escuela regular en horario noche** (mismo grado, mismo grupo, mismas materias del grado). **No puede** operar sin modificaciones el modelo de **educación nocturna con materias pendientes de niveles anteriores y múltiples grupos por materia** de forma nativa y consistente en todos los módulos.

---

## 17. Respuestas obligatorias finales

| # | Pregunta | Respuesta |
|---|----------|-----------|
| 1 | ¿Puede SchoolManager operar hoy en una escuela nocturna de Panamá **sin modificaciones**? | **NO** (modelo completo). **SÍ con restricciones** para el patrón ya usado en producción (1 matrícula nocturna por estudiante, materias del grado). |
| 2 | Porcentaje estimado de compatibilidad | **~55%** (modelo nocturno completo) / **~75%** (uso operativo actual en Render) |
| 3 | Módulos que funcionarían sin cambios | `TeacherAssignment`, catálogo académico básico, gradebook por clase, carnet/ credenciales, prematrícula/pagos (si aplica) |
| 4 | Módulos que requerirían ajustes | `StudentAssignment` (flujos replace/sync), `Attendance`, `AprobadosReprobados`, `StudentProfile`, `ClubParents`, promoción anual, reportes consolidados |
| 5 | ¿La arquitectura es escalable para educación nocturna? | **SÍ** a nivel de datos (multi-matrícula, SSA); **NO** completa sin evolución de reglas de negocio y UI |
| 6 | ¿Soporta materias pendientes (arrastre)? | **NO** (nativo) — **Parcial** vía SSA manual sin semántica de arrastre |
| 7 | ¿Soporta estudiantes con materias de múltiples niveles? | **Parcial** — posible en BD; **0 casos en prod**; flujos con riesgos |
| 8 | ¿Soporta educación modular? | **NO** |
| 9 | ¿Soporta educación semestral? | **NO** (solo trimestres) |
| 10 | Riesgo de adaptación | **Medio–Alto** (modelo completo) / **Medio** (extensión del uso actual) |

---

## 18. Referencias internas consultadas

- `Docs/ANALISIS_ESTUDIANTES_NOCTURNOS.md` (2026-04-13) — análisis previo; parcialmente superado por migración `20260418204938` y SSA  
- `Docs/prematriculation_apply_academic_year_changes_analysis.md` — confirma ausencia de promoción  
- `RECOMENDACIONES_GESTION_GRADOS.md` — propuesta no implementada  

---

*Documento generado en modo auditoría. No se modificó código, modelos, migraciones ni datos en base de datos.*
