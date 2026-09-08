# 03 — Análisis de backend y reportes

Este documento revisa si el backend actual puede **recuperar y procesar** la información del reporte de carga horaria curricular. No inventa archivos ni endpoints.

---

## 1. ¿Existe una consulta que reúna los datos del reporte?

**No.**

No hay controlador, servicio, DTO ni vista cuyo propósito sea una malla de horas por área / asignatura / grado / B1 / B2.

Lo más cercano, y no equivale al reporte:

| Pieza | Qué hace | Por qué no alcanza |
|---|---|---|
| `CurriculumService.GetTracksAsync` | Lista mallas y sus `CurriculumSubjects` | No incluye área, especialidad, B1/B2 ni horas reales (`credits` = 1) |
| `SubjectAssignmentService.GetSubjectsBySpecialtyAndAreaAsync` | Materias de una especialidad y área | No devuelve grado ni horas |
| `CelosanReportService.BuildDashboardAsync` | Tablero CELOSAM (prematrícula, cupos, cédulas, retiros) | Otro dominio |
| `AprobadosReprobadosService` | Rendimiento académico y PDF | Notas, no malla de horas |

No se encontró ningún endpoint tipo “reporte carga horaria”, “plan de estudio PDF” o “malla B1/B2”.

---

## 2. ¿Existen los datos pero falta una consulta?

**A medias.**

### Datos que sí se podrían consultar hoy (no hay consulta lista, pero el origen existe)

Archivo: `SchoolManager/Services/Implementations/SubjectAssignmentService.cs`  
Función: `GetSubjectsBySpecialtyAndAreaAsync(Guid specialtyId, Guid areaId)`  
Utilidad: recupera asignaturas de una carrera y un área.  
Limitación: no incluye grado, B1/B2 ni horas.

Archivo: `SchoolManager/Services/Implementations/SubjectAssignmentService.cs`  
Función: `GetGradeLevelsBySubjectIdAsync(Guid subjectId, Guid specialtyId, Guid areaId)`  
Utilidad: indica en qué grados se dicta una materia de esa carrera/área.  
Limitación: el grado no está partido en B1/B2; no hay horas.

Archivo: `SchoolManager/Services/Implementations/SubjectAssignmentService.cs`  
Función: `GetBySpecialtyIdAsync(Guid specialtyId)`  
Utilidad: áreas usadas por una especialidad.  
Limitación: no arma la grilla ni totales de horas.

Archivo: `SchoolManager/Services/Implementations/SubjectService.cs`  
Función: `GetSubjectAssignmentsByGradeAndGroupAsync(Guid gradeId, Guid groupId)`  
Utilidad: asignaciones de un grado y un grupo, con `Subject`, `Area` y `Specialty` incluidos.  
Limitación: está pensada para un grupo operativo (`10-A3`), no para el plan de la carrera; no trae horas.

Archivo: `SchoolManager/Services/Implementations/ModularAcademicServices.cs` (`CurriculumService`)  
Función: `GetTracksAsync` / `AddSubjectAsync`  
Utilidad: CRUD de malla modular; persiste `Credits`, `ModuleOrder`, `GradeLevelId`.  
Limitación: `Credits` no es carga horaria en los datos (todas las filas = 1.00); no hay B1/B2; la malla no tiene especialidad.

Archivo: `SchoolManager/Controllers/CurriculumTracksController.cs`  
Ruta: `GET /SuperAdmin/CurriculumTracks`  
Utilidad: pantalla de mallas modulares.  
Limitación: UI de administración, no reporte de carga horaria. El formulario pide `credits` y `moduleOrder`, no horas ni B1/B2.

Archivo: `SchoolManager/Controllers/SpecialtyController.cs`  
Función: `Index` / `Create`  
Utilidad: catálogo de bachilleratos.  
Limitación: no genera documento.

Archivo: `SchoolManager/Controllers/AreaController.cs`  
Función: `Index` / `Create`  
Utilidad: catálogo de áreas.  
Limitación: no genera documento.

Archivo: `SchoolManager/Controllers/SubjectController.cs`  
Función: `Index`, `ListJson`  
Utilidad: catálogo de materias.  
Limitación: el JSON no incluye área (el `AreaId` del catálogo está vacío) ni horas.

**Conclusión de esta parte:** se podría armar, con nuevas consultas sobre `subject_assignments`, la lista de materias por especialidad/área/grado. Esa consulta **no existe hoy**. Aunque existiera, **seguiría faltando el dato de horas y B1/B2**.

### Datos que no están, aunque se escriba la consulta

Una consulta nueva no puede inventar:

- horas por asignatura/grado/bloque;
- columnas B1 y B2;
- subtotales de horas.

Eso no es un hueco de backend: es un hueco de datos (ver documento 02).

---

## 3. ¿Existen mecanismos para generar PDF o Excel?

### PDF: sí hay infraestructura, no hay este reporte

La solución usa **QuestPDF** (licencia Community) en varios servicios reales:

| Archivo | Uso actual |
|---|---|
| `SchoolManager/Services/Implementations/AprobadosReprobadosService.cs` — `ExportarAPdfAsync` | PDF de aprobados/reprobados |
| `SchoolManager/Services/Implementations/TeacherWorkPlanPdfService.cs` | PDF de plan de trabajo docente |
| `SchoolManager/Services/Implementations/DirectorWorkPlanPdfService.cs` | PDF de planes del director |
| `SchoolManager/Services/Implementations/StudentIdCardPdfService.cs` | Carnet estudiantil |
| `SchoolManager/Services/Implementations/InstitutionalCredentialPdfService.cs` | Credencial institucional |
| `SchoolManager/Services/Implementations/CelosamPrematriculationModuleService.cs` | Comprobante/horario de prematrícula modular |

Controlador de exportación PDF existente:

- `SchoolManager/Controllers/AprobadosReprobadosController.cs` — `ExportarPdf`
- `SchoolManager/Controllers/DirectorWorkPlansController.cs` — `ExportPdf`

Ninguno genera la malla de carga horaria.

### Excel: no hay un motor de Excel académico listo

Archivo: `SchoolManager/Services/Implementations/AprobadosReprobadosService.cs`  
Función: `ExportarAExcelAsync`  
Utilidad declarada: exportar el reporte a Excel.  
Limitación real: lanza `NotImplementedException` con el comentario “TODO: Implementar exportación a Excel usando ClosedXML o EPPlus”.

Archivo: `SchoolManager/Controllers/DisciplineReportController.cs`  
Función: `ExportToExcel`  
Utilidad: exporta disciplina.  
Limitación: genera **CSV** (`text/csv`), no un .xlsx.

Archivo: `SchoolManager/Views/Director/Director.cshtml`  
Función JS: `exportToExcel`  
Utilidad: descarga del dashboard del director.  
Limitación: exportación en cliente, de otros indicadores (desempeño, profesores), no de malla curricular.

**No se confirmó** en el código actual el uso de ClosedXML ni EPPlus para este reporte (el propio servicio de aprobados/reprobados lo deja pendiente).

---

## 4. ¿Hay un reporte parecido que demuestre infraestructura?

Sí hay **infraestructura de reportes**, pero de otro tipo:

1. **Reportes CELOSAM**  
   Archivo: `SchoolManager/Services/Implementations/CelosanCompletionServices.cs` (`CelosanReportService.BuildDashboardAsync`)  
   Vista: `SchoolManager/Views/CelosanAdmin/Reports.cshtml`  
   Controlador: `SchoolManager/Controllers/CelosanAdminController.cs`  
   Contenido: prematriculados, demanda de materias, cupos, grupos llenos, cédulas vencidas, retiros, avance académico.  
   Limitación: no incluye carga horaria curricular ni B1/B2.

2. **Aprobados / reprobados**  
   Es el reporte tabular PDF más cercano en madurez (filtros, PDF, intento de Excel).  
   Dominio: calificaciones, no plan de horas.

3. **Planes de trabajo docente**  
   `TeacherWorkPlanPdfService` / `DirectorWorkPlanPdfService`.  
   Dominio: contenidos por semanas. El PDF habla de “Contenidos por bloques”, que son bloques pedagógicos del plan docente, **no** B1/B2 de la malla MEDUCA.

4. **Horarios**  
   `TimeSlotController`, `ScheduleConfigurationController`, `ScheduleEntry`.  
   Dominio: franjas diarias de clase. “Bloque 1/2” aquí es un slot de 45–50 minutos.

Eso demuestra que Celosan **sí sabe generar documentos**, no que **ya tenga** este documento.

---

## 5. ¿Dónde está el impedimento principal?

Está en **varios** de los factores, en este orden:

### 1) Datos (impedimento principal)

No hay horas curriculares ni B1/B2 persistidos.  
`credits = 1.00` no sustituye horas.  
El horario operativo no cubre el plan.

Sin ese hecho, ningún backend puede rellenar las celdas del documento de referencia.

### 2) Relaciones (impedimento secundario)

- El área de la materia no está en `subjects` (catalogo vacío).
- La malla `curriculum_subjects` no tiene especialidad ni área.
- La relación usable (especialidad + área + materia + grado) está en `subject_assignments`, pensada para grupos, no para un plan oficial por bloque.

Se puede agrupar y distinct-ar esa tabla para una matriz materia×grado×área. No se puede partir cada grado en B1 y B2.

### 3) Backend (falta de consulta y de reporte)

No existe servicio ni endpoint que arme esta grilla.  
Los servicios citados recuperan piezas (materias, áreas, grados, malla), no el documento.

Esto **solo** bloquearía la entrega automática del inventario de materias. No es el bloqueo de las horas: esas no existen aunque se escriba el servicio.

### 4) Generación del documento (no es el cuello de botella)

QuestPDF ya está en el proyecto. La ausencia de este PDF no es por falta de librería.  
Excel académico no está implementado de forma general, pero eso es secundario: aunque hubiera Excel, no habría horas que exportar.

---

## Resumen del backend

| Pregunta | Respuesta |
|---|---|
| ¿Hay una consulta que reúna el reporte completo? | No |
| ¿Hay datos de materias/áreas/grados/carrera recuperables? | Sí, principalmente vía `SubjectAssignment` |
| ¿Hay datos de B1/B2 y horas recuperables? | No |
| ¿Hay PDF en el sistema? | Sí, para otros reportes (QuestPDF) |
| ¿Hay Excel académico genérico? | No (pendiente / CSV) |
| ¿Existe un reporte parecido de malla de horas? | No |
| Impedimento principal | Datos de carga horaria y ausencia de B1/B2 |
| Impedimento secundario | No hay consulta ni reporte; relaciones de malla incompletas respecto al documento |

Si se pidiera únicamente “listado de asignaturas de Electricidad por área y grado”, el backend **aún no lo entrega**, pero los datos **sí están**.  
Si se pide el documento de referencia con horas y B1/B2, el backend **no puede** completarlo con el modelo actual.
