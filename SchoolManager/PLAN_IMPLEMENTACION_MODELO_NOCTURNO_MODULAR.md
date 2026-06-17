# Plan de Implementacion del Modelo Academico Nocturno Modular

Fecha: 2026-06-17

Proyecto: `C:\Proyectos\EduplanerNoche\SchoolManager`

Estado de este documento: propuesta tecnica para revision y aprobacion. No contiene cambios de codigo, migraciones ejecutadas ni modificaciones de datos.

## 1. Fase 0: Backup obligatorio completado

Antes de crear este plan se genero y valido un backup completo de PostgreSQL desde Render hacia almacenamiento local.

| Campo | Valor |
|---|---|
| Ruta del backup | `C:\Proyectos\EduplanerNoche\backups\modelo_nocturno_modular_20260617_183436\schoolmanager_render_full_20260617_183436.dump` |
| Directorio | `C:\Proyectos\EduplanerNoche\backups\modelo_nocturno_modular_20260617_183436` |
| Tamano | `402409` bytes |
| Fecha/hora local | `2026-06-17 18:34:37` |
| SHA256 | `A363472FC99FCAE46E519D2431C952F3A2FCBD4A55ED5465A622A1A2F9E9C246` |
| Validacion | `pg_restore --list OK`; `pg_restore --schema-only OK` |

La validacion confirma que el archivo es legible por `pg_restore` y que puede materializar el esquema en SQL local. No se ejecuto restauracion contra produccion ni se hicieron escrituras en Render.

## 2. Objetivo funcional

Transformar el modelo academico nocturno desde una matricula anual con trimestres evaluativos hacia un modelo modular donde cada trimestre sea una unidad academica operativa.

El modelo requerido debe permitir:

- Matricula por ano academico, trimestre, materia y nivel.
- Malla curricular secuencial.
- Prerrequisitos formales por materia.
- Promocion por materia, no por ano completo.
- Ingreso tardio en segundo o tercer trimestre.
- Convalidaciones/equivalencias externas.
- Arrastres y refuerzos sin inventar notas de trimestres no cursados.
- Compatibilidad con estudiantes y notas existentes.

## 3. Diagnostico de punto de partida

El analisis previo determino:

- `student_assignments` representa la matricula/asignacion base por estudiante, grado, grupo, jornada y ano academico.
- `student_subject_assignments` representa la inscripcion individual por materia, pero hoy no exige trimestre.
- `trimester` divide el ano para actividades/notas, pero no crea matriculas.
- `subject_promotion_records` registra aprobacion/reprobacion por materia y trimestre como texto, pero no habilita ni bloquea inscripciones futuras.
- No existen tablas de prerrequisitos, malla curricular, equivalencias o convalidaciones.
- Las validaciones actuales son indirectas: duplicados, cupos, grado numerico, materias reprobadas y nota minima `3.0`.

Conclusion tecnica: el cambio debe ser aditivo y por fases. No conviene reemplazar de golpe `student_assignments` ni `student_subject_assignments`, porque calificaciones, reportes, asistencia, carnets, pagos y portal docente ya dependen de esas tablas.

## 4. Principios de diseno

1. **No perdida de informacion.** Ninguna tabla actual debe eliminarse ni vaciarse.
2. **Compatibilidad gradual.** El modelo anual actual debe seguir funcionando mientras se activa el modelo modular.
3. **Nuevas reglas en servicios centrales.** Las validaciones deben concentrarse en servicios de dominio, no solo en controladores.
4. **Datos historicos intactos.** Registros actuales con `trimester_id` o `curriculum_subject_id` nulos deben seguir siendo legibles.
5. **Feature flag.** Activar el modelo modular por escuela para evitar romper instituciones o datos existentes.
6. **Migraciones aditivas primero.** Crear columnas nullable y tablas nuevas antes de cambiar flujos.
7. **No backfill masivo automatico.** Cualquier migracion de estudiantes actuales debe ejecutarse como reporte/preview y luego aplicarse con aprobacion.
8. **Prerrequisitos como fuente de verdad.** La inscripcion debe consultar creditos aprobados, equivalencias y convalidaciones antes de crear una materia.

## 5. Arquitectura propuesta

### 5.1 Capas nuevas

| Capa | Responsabilidad |
|---|---|
| Catalogo curricular | Define malla, niveles, materias, orden, creditos y prerrequisitos. |
| Periodo academico modular | Agrupa las inscripciones del estudiante por ano y trimestre. |
| Validacion academica | Decide si una materia puede matricularse. |
| Historial/creditos aprobados | Guarda aprobaciones internas y convalidaciones externas. |
| Promocion modular | Cierra una materia y habilita las siguientes segun prerrequisitos. |
| Convalidacion | Registra evidencia externa y la convierte en credito academico validado. |

### 5.2 Integracion con tablas existentes

El sistema actual debe mantenerse:

- `student_assignments` seguira como base de estudiante en grado/grupo/jornada.
- `student_subject_assignments` seguira como inscripcion operativa que usa el libro de calificaciones.
- `student_activity_scores` seguira enlazando notas a actividades y a la inscripcion de materia.
- `subject_promotion_records` seguira registrando resultados, pero debe enriquecerse con FKs formales.

El modelo modular se integra agregando:

- Un encabezado de matricula por trimestre.
- Referencias desde `student_subject_assignments` al trimestre y a la materia curricular.
- Una tabla independiente de creditos aprobados que sirva para validar prerrequisitos.

## 6. Tablas nuevas propuestas

### 6.1 `curriculum_tracks`

Representa una malla curricular por escuela y vigencia.

Campos propuestos:

| Campo | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | Generado por BD. |
| `school_id` | uuid FK nullable | Permite mallas globales o por escuela. |
| `name` | varchar(150) | Ej: `Educacion Nocturna Modular 2026`. |
| `description` | text nullable | Descripcion funcional. |
| `academic_year_id` | uuid FK nullable | Vigencia opcional. |
| `is_active` | boolean | Solo una malla activa por escuela/ano debe usarse en validacion. |
| `created_at`, `updated_at` | timestamptz | Auditoria. |
| `created_by`, `updated_by` | uuid nullable | Auditoria. |

Indices:

- `ix_curriculum_tracks_school_id`
- `ix_curriculum_tracks_academic_year_id`
- Indice parcial recomendado: una malla activa por escuela/ano.

### 6.2 `curriculum_subjects`

Define cada materia dentro de una malla.

Campos propuestos:

| Campo | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | Materia curricular. |
| `curriculum_track_id` | uuid FK | Malla a la que pertenece. |
| `subject_id` | uuid FK | Materia existente. |
| `grade_level_id` | uuid FK nullable | Nivel/grado existente, si aplica. |
| `level_name` | varchar(80) | Ej: `Nivel 1`, `Modulo 2`, `Decimo`. |
| `module_order` | int | Orden secuencial. |
| `credits` | numeric(5,2) | Creditos academicos. |
| `minimum_passing_score` | numeric(5,2) | Default `3.0`. |
| `is_active` | boolean | Vigencia dentro de la malla. |
| `created_at`, `updated_at` | timestamptz | Auditoria. |

Indices:

- `ix_curriculum_subjects_track`
- `ix_curriculum_subjects_subject`
- `ix_curriculum_subjects_grade_level`
- Unico recomendado: `(curriculum_track_id, subject_id, grade_level_id, module_order)`.

### 6.3 `curriculum_subject_prerequisites`

Define prerrequisitos formales.

Campos propuestos:

| Campo | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | Identificador. |
| `curriculum_subject_id` | uuid FK | Materia que se desea cursar. |
| `prerequisite_curriculum_subject_id` | uuid FK | Materia requerida. |
| `requirement_type` | varchar(30) | `Required`, `Corequisite`, `Recommended`. |
| `minimum_score` | numeric(5,2) nullable | Si difiere de la malla. |
| `allow_equivalence` | boolean | Si acepta convalidacion/equivalencia. |
| `is_active` | boolean | Permite retirar reglas sin borrarlas. |
| `created_at`, `updated_at` | timestamptz | Auditoria. |

Reglas:

- No permitir que una materia sea prerrequisito de si misma.
- Evitar ciclos en servicio de validacion.
- Para bloqueo obligatorio, solo `Required` debe impedir matricula.

### 6.4 `student_academic_period_enrollments`

Encabezado de matricula modular por estudiante, ano y trimestre.

Campos propuestos:

| Campo | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | Matricula del periodo. |
| `school_id` | uuid FK | Escuela. |
| `student_id` | uuid FK | Estudiante. |
| `academic_year_id` | uuid FK | Ano academico. |
| `trimester_id` | uuid FK | Trimestre real. |
| `student_assignment_id` | uuid FK nullable | Matricula base existente. |
| `entry_type` | varchar(30) | `Regular`, `LateEntry`, `Transfer`, `Reentry`. |
| `status` | varchar(30) | `Draft`, `Active`, `Closed`, `Cancelled`. |
| `created_at`, `updated_at` | timestamptz | Auditoria. |
| `created_by`, `updated_by` | uuid nullable | Auditoria. |

Indices:

- Unico recomendado: `(student_id, academic_year_id, trimester_id)` para encabezado activo.
- `ix_student_period_enrollments_school`
- `ix_student_period_enrollments_trimester`

### 6.5 `student_academic_credits`

Fuente de verdad para materias aprobadas o convalidadas. Esta tabla alimenta la validacion de prerrequisitos.

Campos propuestos:

| Campo | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | Credito academico. |
| `school_id` | uuid FK nullable | Escuela que registra. |
| `student_id` | uuid FK | Estudiante. |
| `curriculum_subject_id` | uuid FK | Materia curricular aprobada/equivalente. |
| `subject_id` | uuid FK | Redundancia util para consultas. |
| `grade_level_id` | uuid FK nullable | Nivel asociado. |
| `academic_year_id` | uuid FK nullable | Ano de aprobacion interna. |
| `trimester_id` | uuid FK nullable | Trimestre de aprobacion interna. |
| `source_type` | varchar(30) | `Promotion`, `Equivalence`, `Convalidation`, `ExternalCertificate`, `ManualAdjustment`. |
| `source_id` | uuid nullable | ID de promocion o convalidacion. |
| `final_score` | numeric(5,2) nullable | Nota si existe. |
| `approved_at` | timestamptz | Fecha de reconocimiento. |
| `status` | varchar(30) | `Valid`, `Revoked`, `PendingReview`. |
| `notes` | text nullable | Observaciones. |
| `created_at`, `created_by` | auditoria | Auditoria. |

Indices:

- Unico recomendado para creditos validos: `(student_id, curriculum_subject_id)` con filtro `status = 'Valid'`.
- `ix_student_academic_credits_student`
- `ix_student_academic_credits_subject`

### 6.6 `student_subject_equivalencies`

Encabezado de convalidacion/equivalencia externa.

Campos propuestos:

| Campo | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | Solicitud/evidencia. |
| `school_id` | uuid FK | Escuela que recibe. |
| `student_id` | uuid FK | Estudiante. |
| `source_institution_name` | varchar(200) | Escuela/institucion externa. |
| `source_country` | varchar(100) nullable | Pais. |
| `certificate_number` | varchar(100) nullable | Numero de certificado. |
| `document_url` | text nullable | Evidencia digital. |
| `status` | varchar(30) | `Pending`, `Approved`, `Rejected`, `Revoked`. |
| `reviewed_by` | uuid nullable | Usuario revisor. |
| `reviewed_at` | timestamptz nullable | Fecha de revision. |
| `notes` | text nullable | Observaciones. |
| `created_at`, `created_by` | auditoria | Auditoria. |

### 6.7 `student_subject_equivalency_items`

Detalle por materia convalidada.

Campos propuestos:

| Campo | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | Detalle. |
| `equivalency_id` | uuid FK | Encabezado. |
| `curriculum_subject_id` | uuid FK | Materia reconocida en la malla local. |
| `external_subject_name` | varchar(200) | Nombre como aparece en certificado. |
| `external_score` | varchar(50) nullable | Puede venir en escala diferente. |
| `normalized_score` | numeric(5,2) nullable | Nota homologada si aplica. |
| `status` | varchar(30) | `Pending`, `Approved`, `Rejected`. |
| `created_at`, `updated_at` | auditoria | Auditoria. |

Cuando un item queda `Approved`, el sistema debe crear o habilitar un `student_academic_credits` con `source_type = 'Equivalence'` o `Convalidation`.

## 7. Tablas existentes a modificar

### 7.1 `student_subject_assignments`

Agregar columnas nullable:

| Columna | Motivo |
|---|---|
| `period_enrollment_id` uuid nullable | Enlace al encabezado de matricula trimestral. |
| `trimester_id` uuid nullable | Trimestre real de la inscripcion. |
| `curriculum_subject_id` uuid nullable | Materia dentro de la malla. |
| `validation_status` varchar(30) nullable | `Validated`, `Blocked`, `PendingEquivalence`, `Legacy`. |
| `validation_message` text nullable | Explicacion del bloqueo o decision. |

Compatibilidad:

- Registros actuales permanecen con valores nulos.
- Los flujos existentes siguen leyendo por `student_id`, `subject_assignment_id`, `academic_year_id`.
- Los nuevos flujos modulares deben exigir `trimester_id` y `curriculum_subject_id` solo cuando el feature flag este activo.

Indice nuevo recomendado:

- `ix_student_subject_assignments_period_enrollment_id`
- `ix_student_subject_assignments_trimester_id`
- `ix_student_subject_assignments_curriculum_subject_id`

Nota sobre indice unico existente:

- Hoy existe unico activo por `(student_id, subject_assignment_id, academic_year_id)`.
- No debe eliminarse en la primera migracion.
- En una fase posterior, si se requiere recursar la misma materia en otro trimestre manteniendo otra activa, se debe reemplazar por un indice unico parcial que incluya `trimester_id`, previa auditoria de duplicados.

### 7.2 `subject_promotion_records`

Agregar columnas nullable:

| Columna | Motivo |
|---|---|
| `trimester_id` uuid nullable | Reemplazar progresivamente el texto `trimester`. |
| `curriculum_subject_id` uuid nullable | Saber que materia curricular fue aprobada. |
| `academic_credit_id` uuid nullable | Enlace al credito creado al aprobar. |

Compatibilidad:

- Mantener `trimester` string mientras existan reportes que lo usan.
- Nuevas promociones deben escribir ambos: `trimester` legado y `trimester_id`.

### 7.3 `prematriculations`

Agregar columnas nullable opcionales:

| Columna | Motivo |
|---|---|
| `target_trimester_id` uuid nullable | Prematricula para ingreso tardio o periodo especifico. |
| `entry_type` varchar(30) nullable | `Regular`, `LateEntry`, `Transfer`, `Reentry`. |
| `requires_equivalence_review` boolean default false | Nuevo ingreso con historial externo. |

Compatibilidad:

- Prematriculas existentes no se alteran.
- Si `target_trimester_id` es null, el flujo actual se comporta como anual/legacy.

### 7.4 `activities` y `student_activity_scores`

No requieren cambio obligatorio inicial porque ya existe `activities.TrimesterId` y `student_activity_scores.StudentSubjectAssignmentId`.

Recomendacion posterior:

- Fortalecer validacion para que la actividad del docente solo acepte estudiantes cuya `student_subject_assignment.trimester_id` coincida con el trimestre de la actividad cuando el modo modular este activo.

## 8. Servicios nuevos propuestos

### 8.1 `ICurriculumService`

Responsabilidades:

- Crear/editar mallas.
- Administrar materias curriculares.
- Administrar prerrequisitos.
- Resolver `curriculum_subject_id` desde `subject_assignment_id`, nivel y malla activa.
- Detectar ciclos en prerrequisitos.

### 8.2 `IAcademicPrerequisiteService`

Responsabilidades:

- Evaluar si un estudiante puede cursar una materia curricular.
- Consultar `student_academic_credits`.
- Aceptar creditos por promocion, equivalencia o convalidacion aprobada.
- Devolver resultado estructurado:
  - `CanEnroll`
  - `MissingPrerequisites`
  - `SatisfiedByEquivalences`
  - `RequiresReview`
  - `Message`

### 8.3 `IModularEnrollmentService`

Responsabilidades:

- Crear encabezado `student_academic_period_enrollments`.
- Crear inscripciones en `student_subject_assignments` con `trimester_id`.
- Bloquear inscripciones si faltan prerrequisitos.
- Permitir matricula parcial.
- Manejar ingreso tardio sin generar notas artificiales.
- Mantener enlace con `student_assignments`.

### 8.4 `IAcademicCreditService`

Responsabilidades:

- Crear credito al aprobar materia.
- Crear credito por convalidacion.
- Revocar credito con auditoria si se aprueba una anulacion.
- Consultar historial academico del estudiante.

### 8.5 `IEquivalencyService`

Responsabilidades:

- Registrar certificados externos.
- Aprobar/rechazar items de equivalencia.
- Homologar materia externa contra materia curricular local.
- Crear creditos academicos cuando la convalidacion sea aprobada.

### 8.6 `IModularPromotionService`

Puede extender o reemplazar progresivamente `SubjectPromotionService`.

Responsabilidades:

- Cerrar una inscripcion de materia.
- Registrar `SubjectPromotionRecord`.
- Crear `student_academic_credits` si aprueba.
- Marcar como `Refuerzo` o mantener pendiente si reprueba.
- Exponer materias siguientes habilitadas.

## 9. Flujos propuestos

### 9.1 Matricula regular en primer trimestre

1. Admin/secretaria selecciona estudiante.
2. Sistema crea o reutiliza `student_assignment`.
3. Sistema crea `student_academic_period_enrollments` para `academic_year + 1T`.
4. Admin selecciona materias del trimestre.
5. `IAcademicPrerequisiteService` valida cada materia.
6. Si cumple, se crea `student_subject_assignments` con:
   - `academic_year_id`
   - `trimester_id`
   - `period_enrollment_id`
   - `curriculum_subject_id`
   - `status = Active`
7. Materias bloqueadas se muestran con razon.

### 9.2 Ingreso en segundo trimestre

1. Admin crea prematricula o asignacion modular con `target_trimester_id = 2T`.
2. Sistema no crea notas de `1T`.
3. Sistema permite seleccionar materias cuyo prerrequisito este satisfecho por:
   - credito interno aprobado,
   - convalidacion aprobada,
   - equivalencia externa aprobada.
4. Si faltan prerrequisitos, la materia queda bloqueada.
5. Se permite matricula parcial en materias compatibles.

### 9.3 Ingreso en tercer trimestre

Mismo criterio que segundo trimestre:

- No inventar notas previas.
- No aprobar automaticamente materias anteriores.
- Permitir solo materias habilitadas por historial o convalidacion.

### 9.4 Promocion por materia

1. Docente/admin cierra materia.
2. Se calcula promedio segun reglas vigentes.
3. Si nota >= minimo:
   - `student_subject_assignments.Status = Approved`
   - `IsActive = false`
   - `EndDate = now`
   - se crea `subject_promotion_records`
   - se crea `student_academic_credits`
4. Si reprueba:
   - `Status = Failed`
   - `EnrollmentType = Refuerzo` si aplica
   - no se crea credito valido.
5. El sistema calcula materias siguientes habilitadas.

### 9.5 Convalidacion

1. Secretaria registra certificado externo.
2. Adjunta documento o referencia.
3. Selecciona materias locales equivalentes.
4. Director/admin revisa.
5. Al aprobar:
   - `student_subject_equivalency_items.Status = Approved`
   - se crea `student_academic_credits`
6. Prerrequisitos quedan satisfechos por ese credito.

## 10. Validaciones obligatorias

### 10.1 Validacion al matricular materia

Debe bloquear si:

- No existe trimestre activo o permitido.
- No existe malla activa.
- La materia no pertenece a la malla.
- Faltan prerrequisitos obligatorios.
- El estudiante ya tiene la misma materia activa en el mismo periodo.
- La materia fue aprobada previamente y no esta marcada como recursable.
- La convalidacion requerida esta pendiente o rechazada.

Debe permitir si:

- No tiene prerrequisitos.
- Todos los prerrequisitos tienen credito valido.
- El prerrequisito fue satisfecho por equivalencia aprobada.
- La regla permite correquisito.

### 10.2 Validacion de arrastres

Un arrastre debe:

- Mantener `EnrollmentType = Refuerzo` o `Libre`.
- No contar como aprobacion hasta que exista credito valido.
- No habilitar materia siguiente si sigue `Failed` o `Active`.

### 10.3 Validacion de estudiantes actuales

Para estudiantes con datos legacy:

- Si no tienen `curriculum_subject_id`, se consideran `Legacy`.
- El sistema puede inferir posibles equivalencias, pero no debe escribirlas masivamente sin aprobacion.
- Debe existir reporte de brechas: materias cursadas sin mapeo curricular, notas sin inscripcion, inscripciones sin trimestre, etc.

## 11. Estrategia de migracion

### Fase 1: Migracion aditiva de esquema

Crear:

- `curriculum_tracks`
- `curriculum_subjects`
- `curriculum_subject_prerequisites`
- `student_academic_period_enrollments`
- `student_academic_credits`
- `student_subject_equivalencies`
- `student_subject_equivalency_items`

Modificar con columnas nullable:

- `student_subject_assignments`
- `subject_promotion_records`
- `prematriculations`

No hacer:

- Deletes.
- Updates masivos.
- Reemplazo de indices unicos existentes.
- Migracion automatica de datos academicos.

### Fase 2: Modelos EF y servicios base

Agregar entidades y configuracion EF.

Servicios:

- `CurriculumService`
- `AcademicPrerequisiteService`
- `ModularEnrollmentService`
- `AcademicCreditService`
- `EquivalencyService`

Activar detras de feature flag:

- `NocturnalModularEnrollment.Enabled`
- Opcionalmente `EnabledSchoolIds`.

### Fase 3: Administracion de malla curricular

Crear pantallas/API para:

- Mallas.
- Materias por malla.
- Orden/creditos.
- Prerrequisitos.

Antes de permitir matricula modular, la escuela debe tener malla activa valida.

### Fase 4: Matricula modular

Modificar flujos:

- `StudentAssignmentController`
- carga masiva
- `PrematriculationService.ConfirmMatriculationAsync`
- `StudentAssignmentService.AddSubjectEnrollmentAsync`

Regla:

- Si flag modular esta apagado, flujo actual.
- Si flag modular esta encendido, validar malla/trimestre/prerrequisitos antes de crear `student_subject_assignments`.

### Fase 5: Promocion modular

Extender `SubjectPromotionService` o introducir `ModularPromotionService`.

Al aprobar:

- crear credito academico.
- cerrar inscripcion.
- habilitar materias siguientes.

Al reprobar:

- marcar refuerzo/pendiente.
- no crear credito.

### Fase 6: Convalidaciones

Agregar flujo administrativo:

- Registro de certificado.
- Detalle por materia.
- Aprobacion/rechazo.
- Creacion de creditos por equivalencia.

### Fase 7: Compatibilidad y migracion asistida de datos actuales

Crear herramientas de solo reporte primero:

- Estudiantes con materias activas sin trimestre.
- Materias aprobadas inferibles desde notas.
- Materias sin mapeo a malla.
- Promociones existentes sin `curriculum_subject_id`.
- Posibles creditos candidatos.

Despues de revision humana, ejecutar migracion controlada por lotes pequenos y con backup nuevo.

### Fase 8: Endurecimiento

Cuando el modelo modular este estable:

- Revaluar indice unico de `student_subject_assignments`.
- Hacer obligatorios `trimester_id` y `curriculum_subject_id` solo para escuelas modulares.
- Deprecar rutas legacy para escuelas nocturnas.

## 12. Compatibilidad con datos existentes

### 12.1 Estudiantes actuales

No se deben modificar automaticamente.

Estrategia:

- Mantener sus matriculas actuales.
- Permitir que nuevas inscripciones sean modulares.
- Generar reporte para convertir historial a creditos.
- Revisar manualmente casos ambiguos.

### 12.2 Notas actuales

No se deben recalcular ni mover.

Estrategia:

- Mantener `student_activity_scores`.
- Usar `Activity.TrimesterId` cuando exista.
- Si falta `StudentSubjectAssignmentId`, conservar relacion por `StudentAssignmentId`.
- Crear creditos historicos solo con aprobacion administrativa.

### 12.3 Promociones actuales

`subject_promotion_records` esta vacia en la consulta de produccion reciente, pero debe tratarse como tabla historica.

Estrategia:

- Nuevos campos nullable.
- Nuevas promociones escriben FKs.
- Registros antiguos, si aparecen, siguen leyendo `trimester` string.

### 12.4 Prematriculas actuales

La consulta reciente mostro 0 filas en `prematriculations`, pero el diseno no depende de eso.

Estrategia:

- Agregar columnas nullable.
- No cambiar estados existentes.
- Nuevo flujo modular usa `target_trimester_id` si esta presente.

## 13. Riesgos principales

| Riesgo | Impacto | Mitigacion |
|---|---|---|
| Romper libro de calificaciones | Alto | Mantener `student_subject_assignments` como tabla operativa y agregar columnas nullable. |
| Duplicar inscripciones | Alto | Validacion central en `ModularEnrollmentService` e indices parciales controlados. |
| Bloquear estudiantes legacy | Alto | Feature flag y modo `Legacy` para registros sin malla. |
| Mapear mal materias existentes | Alto | Reportes de preview y aprobacion manual antes de crear creditos historicos. |
| Prerrequisitos ciclicos | Medio | Validacion en `CurriculumService` al guardar reglas. |
| Convalidaciones fraudulentas o incompletas | Alto | Estado `Pending/Approved/Rejected`, evidencia documental y auditoria. |
| Ingreso tardio con notas faltantes | Medio | No inventar notas; usar convalidaciones o matricula parcial. |
| Cambiar indices unicos demasiado pronto | Alto | No modificar indices existentes hasta fase de estabilizacion. |
| Confundir trimestre evaluativo con periodo de matricula | Medio | Crear encabezado `student_academic_period_enrollments` y usar `trimester_id` formal. |

## 14. Plan de pruebas

### 14.1 Pruebas unitarias

- Materia sin prerrequisitos permite inscripcion.
- Materia con prerrequisito aprobado permite inscripcion.
- Materia con prerrequisito faltante bloquea.
- Equivalencia aprobada satisface prerrequisito.
- Equivalencia pendiente no satisface.
- Reprobacion no crea credito.
- Aprobacion crea credito unico.
- Ciclo de prerrequisitos se rechaza.

### 14.2 Pruebas de integracion

- Crear malla completa.
- Matricular estudiante nuevo en `1T`.
- Matricular estudiante nuevo en `2T` con convalidacion.
- Intentar matricular `Matematica II` sin `Matematica I`.
- Aprobar `Matematica I` y luego matricular `Matematica II`.
- Reprobar materia y verificar `Refuerzo`.
- Validar que notas existentes sigan consultandose.

### 14.3 Pruebas de regresion

- Carnet estudiantil.
- Portal docente.
- Libro de calificaciones.
- Asistencia.
- Reporte academico del estudiante.
- Carga masiva actual.
- Prematricula y pago.
- Club de padres/acceso.

## 15. Cambios que NO deben hacerse en la primera implementacion

- No borrar `student_assignments`.
- No borrar `student_subject_assignments`.
- No convertir masivamente datos actuales.
- No eliminar columnas legacy como `Activity.Trimester` o `SubjectPromotionRecord.Trimester`.
- No hacer obligatorio `trimester_id` globalmente.
- No cambiar indices unicos existentes sin auditoria de datos.
- No generar notas para trimestres no cursados.

## 16. Orden recomendado de ejecucion

1. Aprobar este plan.
2. Crear backup nuevo antes de implementar.
3. Crear migracion aditiva de tablas/columnas nullable.
4. Compilar y aplicar migracion en entorno de prueba.
5. Crear entidades EF y servicios base.
6. Crear administracion de malla.
7. Crear validacion de prerrequisitos.
8. Integrar matricula modular detras de feature flag.
9. Integrar promocion modular y creditos.
10. Integrar convalidaciones.
11. Ejecutar pruebas con estudiante nuevo en `1T`, `2T` y `3T`.
12. Crear reportes de compatibilidad para estudiantes actuales.
13. Solo con aprobacion posterior, ejecutar migracion asistida de historial.

## 17. Decision tecnica recomendada

La recomendacion es **no reemplazar el modelo actual**, sino extenderlo:

- `student_assignments` sigue como base administrativa.
- `student_subject_assignments` se convierte gradualmente en inscripcion por materia y trimestre.
- `student_academic_period_enrollments` agrupa la matricula del trimestre.
- `curriculum_subjects` y `curriculum_subject_prerequisites` definen la malla.
- `student_academic_credits` se vuelve la fuente de verdad para saber que puede cursar un estudiante.

Esta ruta reduce riesgo, conserva datos existentes y permite activar el modelo modular por escuela o por fase.

## 18. Pendientes de definicion funcional

Antes de implementar se deben confirmar estas reglas:

1. Escala de notas definitiva y nota minima por materia: usar `3.0` global o configurable por malla.
2. Si una materia reprobada queda activa como `Refuerzo` o se cierra y se crea nueva inscripcion.
3. Si se permiten correquisitos.
4. Si una convalidacion puede aprobarse por secretaria o solo director/admin.
5. Si creditos externos requieren documento obligatorio.
6. Si un estudiante puede cursar materias de diferentes niveles en el mismo trimestre.
7. Si la asistencia debe bloquear promocion en algun programa.
8. Si la malla sera unica para todas las escuelas nocturnas o por escuela.

## 19. Veredicto

El cambio es viable, pero debe implementarse en fases y con migraciones aditivas. La parte critica no es crear tablas, sino mover la regla academica hacia una fuente formal de verdad:

- malla curricular,
- prerrequisitos,
- creditos aprobados,
- equivalencias,
- matricula trimestral.

Hasta que esas piezas existan y se integren en `StudentAssignmentService`, carga masiva, prematricula y promocion, el sistema seguira permitiendo saltos academicos por rutas administrativas.
