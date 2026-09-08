# 02 — Análisis de base de datos y modelos

Este documento diagnostica **solo** el modelo de datos y los datos actuales. No propone cómo crear lo que falta.

Fuente de esquema: `SchoolManager/Models/*.cs` y mapeo en `SchoolManager/Models/SchoolDbContext.cs`.  
Fuente de datos: consultas de solo lectura a PostgreSQL `schoolmanager_daqf`.

---

## Tablas relevantes

| Tabla | Entidad | PK | Para qué sirve en este reporte |
|---|---|---|---|
| `specialties` | `Specialty` | `id` (uuid) | Nombre del bachillerato/carrera |
| `area` | `Area` | `id` (uuid) | Humanística / Científica / Tecnológica |
| `subjects` | `Subject` | `id` (uuid) | Catálogo de asignaturas |
| `grade_levels` | `GradeLevel` | `id` (uuid) | Grados (7–12; el reporte usa 10–12) |
| `groups` | `Group` | `id` (uuid) | Grupos operativos (10-A3, A3, etc.), no B1/B2 |
| `subject_assignments` | `SubjectAssignment` | `id` (uuid) | Cruce especialidad + área + materia + grado + grupo |
| `curriculum_tracks` | `CurriculumTrack` | `id` (uuid) | Malla curricular (una por escuela/año, no por bachillerato) |
| `curriculum_subjects` | `CurriculumSubject` | `id` (uuid) | Materia dentro de la malla: grado, orden, créditos |
| `trimester` | `Trimester` | `id` (uuid) | Periodos `1T`, `2T`, `3T` |
| `shifts` | `Shift` | `id` (uuid) | Jornada (Mañana, Tarde, Noche) |
| `time_slots` | `TimeSlot` | `id` (uuid) | Franjas diarias “Bloque 1…n” |
| `schedule_entries` | `ScheduleEntry` | `id` (uuid) | Horario semanal docente (operativo) |
| `teacher_assignments` | `TeacherAssignment` | `id` (uuid) | Puente horario → materia asignada |
| `academic_years` | `AcademicYear` | `id` (uuid) | Año lectivo; no es carga horaria |
| `school_schedule_configurations` | `SchoolScheduleConfiguration` | `id` | Duración en minutos de bloques de **horario**, no de malla |

Tablas revisadas y **descartadas** como fuente del reporte pedido:

- `student_assignments`, `student_subject_assignments`, `student_academic_credits`: matrícula y créditos del estudiante, no plan curricular de horas.
- `teacher_work_plans` / `teacher_work_plan_details`: plan de contenidos por semanas, sin horas de malla.
- Reportes de disciplina/orientación: no son malla curricular.

---

## Relaciones (claves foráneas usadas por el reporte)

```
specialties 1 ──< subject_assignments >── 1 area
                                      >── 1 subjects
                                      >── 1 grade_levels
                                      >── 1 groups

curriculum_tracks 1 ──< curriculum_subjects >── 1 subjects
                                           >── 0..1 grade_levels

subjects 0..1 ── area     (campo subjects."AreaId"; en datos está NULL)

teacher_assignments >── subject_assignments
schedule_entries    >── teacher_assignments
schedule_entries    >── time_slots
```

Observación crítica: **la relación canónica carrera–área–materia–grado está en `subject_assignments`**, no en `curriculum_subjects`. La malla modular no conoce la especialidad.

---

## Campos por entidad (solo los pertinentes)

### `specialties` / `Specialty`

| Campo | Columna | Tipo | Observación |
|---|---|---|---|
| Id | `id` | uuid | PK |
| Name | `name` | varchar(100) | Nombre del bachillerato |
| Description | `description` | text | Vacío en datos actuales |
| SchoolId | `school_id` | uuid? | Tenant |

Datos actuales: 5 filas, entre ellas `BACHILLER EN ELECTRICIDAD`. No aparece el texto “INDUSTRIAL”, pero el equivalente de carrera sí existe.

### `area` / `Area`

| Campo | Columna | Tipo | Observación |
|---|---|---|---|
| Id | `id` | uuid | PK |
| Name | `name` | varchar(100), unique | HUMANISTICA, CIENTÍFICA, TECNOLÓGICA |
| Code | `code` | varchar(20) | Vacío |
| IsActive | `is_active` | bool | true |

Las tres áreas del documento de referencia **existen y están pobladas**.

### `subjects` / `Subject`

| Campo | Columna | Tipo | Observación |
|---|---|---|---|
| Id | `id` | uuid | PK |
| Name | `name` | varchar(100) | Nombre de la asignatura |
| Code | `code` | varchar(10) | Mayormente vacío |
| Area | `"AreaId"` | uuid? | **NULL en 83/83 filas** |
| SchoolId | `school_id` | uuid? | |

**NO EXISTE ACTUALMENTE EN EL MODELO DE DATOS** un campo de horas, horas semanales, horas anuales o carga.

### `grade_levels` / `GradeLevel`

| Campo | Columna | Tipo | Observación |
|---|---|---|---|
| Id | `id` | uuid | PK |
| Name | `name` | text | `7`,`8`,`9`,`10`,`11`,`12` |

No hay B1/B2 dentro del grado. El nombre es solo el número de grado.

### `subject_assignments` / `SubjectAssignment`

| Campo | Columna | Tipo | Observación |
|---|---|---|---|
| SpecialtyId | `specialty_id` | uuid | FK carrera |
| AreaId | `area_id` | uuid | FK área |
| SubjectId | `subject_id` | uuid | FK materia |
| GradeLevelId | `grade_level_id` | uuid | FK grado |
| GroupId | `group_id` | uuid | FK grupo (sección operativa) |
| Status | `status` | varchar(10) | Estado de la asignación |

**NO EXISTE ACTUALMENTE EN EL MODELO DE DATOS** un campo de horas en esta tabla.

Hay 435 asignaciones. Para Electricidad el cruce único materia×grado×área está completo (39 combinaciones en 10/11/12). Cada combinación aparece en 2 grupos (`10-A3` y `A3`, etc.), que son secciones, no B1/B2.

### `curriculum_tracks` / `CurriculumTrack`

| Campo | Columna | Tipo | Observación |
|---|---|---|---|
| Name | `name` | varchar(150) | “Malla Modular Nocturna CELOSAM 2026” |
| AcademicYearId | `academic_year_id` | uuid? | |
| IsActive | `is_active` | bool | true |
| SchoolId | `school_id` | uuid? | |

Hay **1** malla. No hay una malla por bachillerato. **NO EXISTE** `specialty_id`.

### `curriculum_subjects` / `CurriculumSubject`

| Campo | Columna | Tipo | Observación |
|---|---|---|---|
| CurriculumTrackId | `curriculum_track_id` | uuid | FK malla |
| SubjectId | `subject_id` | uuid | FK materia |
| GradeLevelId | `grade_level_id` | uuid? | FK grado |
| LevelName | `level_name` | varchar(80) | Copia del grado: `10`,`11`,`12`,`7`,`8`,`9` |
| ModuleOrder | `module_order` | int | Entero 1–71; orden de listado, no B1/B2 |
| Credits | `credits` | numeric(5,2) | **1.00 en las 285 filas** |
| MinimumPassingScore | `minimum_passing_score` | numeric(5,2) | Nota mínima, no horas |

**NO EXISTE ACTUALMENTE EN EL MODELO DE DATOS**: horas, B1/B2, `area_id`, `specialty_id`.

`level_name` no contiene B1 ni B2 (0 filas).

`module_order` no agrupa dos bloques por grado: es un contador correlativo de la generación masiva de la malla.

### `trimester` / `Trimester`

| Campo | Columna | Tipo | Observación |
|---|---|---|---|
| Name | `name` | text | `1T`, `2T`, `3T` |
| Order | `order` | int | 1, 2, 3 |
| IsActive | `is_active` | bool | 1T inactivo; 2T y 3T activos |

Tres trimestres ≠ dos bloques B1/B2 por grado.

### `time_slots` / `TimeSlot` y `schedule_entries` / `ScheduleEntry`

| Campo | Significado real |
|---|---|
| `time_slots.name` “Bloque 1”, “Bloque 2”… | Franja horaria del día (p. ej. 18:10–19:00) |
| `schedule_entries.day_of_week` | Día de la semana del horario docente |
| `school_schedule_configurations.*_duration_minutes` | Duración del bloque de **clase**, no horas de malla |

Esto **no** es el B1/B2 del documento curricular. Además, el horario de Electricidad 10° solo cubre 3 de 14 materias.

---

## De dónde saldría cada dato del reporte

| Dato del reporte | Tabla/Entidad | Campo | Existe | Observación |
|---|---|---|---|---|
| Bachillerato | `specialties` / `Specialty` | `name` | Sí | Dato real: `BACHILLER EN ELECTRICIDAD`. No dice “INDUSTRIAL” |
| Área | `area` / `Area` | `name` | Sí | HUMANISTICA, CIENTÍFICA, TECNOLÓGICA |
| Asignatura | `subjects` / `Subject` | `name` | Sí | 83 materias; nombres alineados al ejemplo |
| Grado | `grade_levels` / `GradeLevel` | `name` | Sí | `10`, `11`, `12` |
| B1/B2 | — | — | No | NO EXISTE ACTUALMENTE EN EL MODELO DE DATOS |
| Relación materia–grado | `subject_assignments` | `subject_id` + `grade_level_id` | Sí | También en `curriculum_subjects`, pero sin especialidad |
| Relación materia–área | `subject_assignments` | `area_id` | Sí | No en `subjects` ni en `curriculum_subjects` |
| Relación materia–plan | `curriculum_subjects` | `curriculum_track_id` + `subject_id` | Parcial | Plan único escolar, no por carrera |
| Horas | — | — | No | NO EXISTE ACTUALMENTE EN EL MODELO DE DATOS |
| Créditos (no son horas) | `curriculum_subjects` | `credits` | Sí, pero inútil para este reporte | Todas las filas = 1.00 |
| Horas derivadas de horario | `schedule_entries` + `time_slots` | duración start/end | Parcial y no equivalente | Incompleto; es horario operativo |
| Total horas | calculado | — | No | No hay sumando |
| Subtotal horas por área | calculado | — | No | No hay sumando |
| Total de asignaturas | calculado | `COUNT(DISTINCT subject_id)` | Sí | Sobre `subject_assignments` filtrado por `specialty_id` |
| Asignaturas por nivel/grado | calculado | `COUNT(DISTINCT subject_id)` agrupado por `grade_level_id` | Sí | Ejemplo Electricidad: 14 / 12 / 13 en 10 / 11 / 12 |
| Periodo académico | `trimester` | `name` | Sí, pero otro concepto | `1T/2T/3T`, no B1/B2 |

---

## Información académica disponible (conteos reales)

Consultas de solo lectura sobre `schoolmanager_daqf`:

| Recurso | Cantidad |
|---|---|
| Áreas | 3 |
| Especialidades | 5 |
| Grados | 6 (7–12) |
| Materias | 83, ninguna con `AreaId` |
| Asignaciones materia-grupo | 435 |
| Materias curriculares | 285, todas `credits = 1.00` |
| Mallas | 1 |
| Trimestres | 3 (`1T`,`2T`,`3T`) |
| Entradas de horario | 302 |
| Coincidencias de texto B1/B2 en nombres clave | 0 |

Electricidad, materias distintas por área y grado:

| Grado | Humanística | Científica | Tecnológica | Total |
|---|---|---|---|---|
| 10 | 5 | 3 | 6 | 14 |
| 11 | 4 | 3 | 5 | 12 |
| 12 | 5 | 3 | 5 | 13 |

Eso demuestra que **la grilla de materias sí se puede reconstruir**.  
La grilla de **horas B1/B2 no**.

---

## Diagnóstico cerrado

El modelo actual responde a la operación escolar (quién dicta qué materia, en qué grupo, grado, área y especialidad).

El modelo actual **no** responde a un plan oficial de carga horaria:

- no hay horas;
- no hay B1/B2 curricular;
- los créditos no están usados como horas;
- la malla modular no está partida por bachillerato;
- el área no está en el catálogo de materias.

NO EXISTE ACTUALMENTE EN EL MODELO DE DATOS el hecho mínimo que el reporte necesita en cada celda: **horas de la asignatura en el bloque B1 o B2 de un grado**.
