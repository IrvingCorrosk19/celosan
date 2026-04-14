# Análisis Arquitectónico: Soporte de Estudiantes Nocturnos
**Sistema**: EduPlaner / SchoolManager — ASP.NET Core 8 + EF Core 9 + PostgreSQL  
**Fecha**: 2026-04-13  
**Nivel de análisis**: Arquitectura Enterprise / Líder Técnico  
**Restricción**: Solo análisis — sin cambios en código ni base de datos  

---

## 1. Resumen Ejecutivo

El sistema actual fue diseñado bajo el supuesto de que **un estudiante pertenece a exactamente un grupo, en un grado, en una jornada**, en un año académico dado. Esta suposición está codificada en al menos **9 puntos críticos** distribuidos entre el modelo de datos, la lógica de negocio y las vistas.

La buena noticia: la **base de datos ya es físicamente compatible** con múltiples asignaciones por estudiante (no existe UNIQUE constraint que lo impida). El problema es que **todo el código de aplicación que rodea esa tabla activamente destruye o ignora esa posibilidad**.

El cambio para soportar estudiantes nocturnos es **técnicamente viable**, pero requiere cirugía en capas múltiples. No es un cambio trivial. Estimación de impacto global: **ALTO**. Sin embargo, los cambios son quirúrgicos y no requieren reescribir el sistema; requieren extender el modelo y corregir los puntos de acoplamiento identificados.

---

## 2. Modelo de Datos Actual

### 2.1 Entidades principales y sus relaciones

```
School
 └── AcademicYear (1 activo por vez)
 └── GradeLevel (Grado 7, 8, 9, 10...)   [UNIQUE: Name]
 └── Subject (Materia: Matemáticas, Inglés...)
 └── Group (Grupo: "A", "B", "Noche-1")
      └── ShiftId → Shift (Mañana / Tarde / Noche)
 └── SubjectAssignment (qué materias se imparten en qué grupo+grado)
      └── TeacherAssignment (qué docente imparte esa materia en ese grupo)
           [UNIQUE: TeacherId + SubjectAssignmentId]

Student (User con rol Student)
 ├── Grade (string denormalizado ← PROBLEMA)
 ├── GroupName (string denormalizado ← PROBLEMA)
 └── StudentAssignment (asignación a grupo+grado+jornada)
      ├── GradeId → GradeLevel
      ├── GroupId → Group
      ├── ShiftId → Shift
      └── AcademicYearId → AcademicYear

StudentActivityScore (nota de una actividad)
 ├── StudentId
 ├── ActivityId
 [UNIQUE: StudentId + ActivityId]
```

### 2.2 Fuentes de verdad — cuál dato manda

| Dato | Tabla fuente de verdad | Duplicado denormalizado | Estado de sincronía |
|------|------------------------|------------------------|----------------------|
| Grado del estudiante | `student_assignments.grade_id` | `students.Grade` (string) | **Desincronizado** — no hay trigger ni sync automático |
| Grupo del estudiante | `student_assignments.group_id` | `students.GroupName` (string) | **Desincronizado** |
| Jornada del grupo | `groups.shift_id` | `groups.Shift` (string) y `student_assignments.shift_id` | **Triple redundancia** — potencial inconsistencia |
| Materias del estudiante | `subject_assignments` (via grupo) | `user_subjects` (tabla M:M) | `user_subjects` **no se usa activamente** |

### 2.3 Tabla student_assignments — núcleo del problema

```sql
CREATE TABLE student_assignments (
    id              UUID PRIMARY KEY,
    student_id      UUID NOT NULL REFERENCES users(id),
    grade_id        UUID NOT NULL REFERENCES grade_levels(id),
    group_id        UUID NOT NULL REFERENCES groups(id),
    shift_id        UUID REFERENCES shifts(id),
    academic_year_id UUID REFERENCES academic_years(id),
    is_active       BOOL NOT NULL DEFAULT true,
    end_date        TIMESTAMPTZ,
    created_at      TIMESTAMPTZ
);

-- Índices existentes (SIN UNIQUE):
-- IX_student_assignments_student_id
-- IX_student_assignments_student_active        (student_id, is_active)
-- IX_student_assignments_student_academic_year (student_id, academic_year_id)
```

**Conclusión clave**: La BD no prohíbe múltiples asignaciones simultáneas por estudiante. El código sí.

---

## 3. Limitaciones Actuales

### 3.1 Campos denormalizados en Student

**Archivo**: `Models/Student.cs`  
```csharp
public string? Grade { get; set; }       // "10"
public string? GroupName { get; set; }   // "A"
```

- Solo pueden contener UN valor.
- No se actualizan automáticamente cuando cambia `StudentAssignment`.
- Si el estudiante está en dos grupos, estos campos quedan con el valor del último actualizado.
- **Todo código que los lea en lugar de ir a `StudentAssignment` obtendrá información incompleta o falsa.**

### 3.2 AssignAsync() destruye asignaciones previas

**Archivo**: `Services/Implementations/StudentAssignmentService.cs`  
```csharp
// Inactiva TODAS las asignaciones activas antes de crear la nueva
var existing = await _context.StudentAssignments
    .Where(a => a.StudentId == studentId && a.IsActive)
    .ToListAsync();

foreach (var assignment in existing)
{
    assignment.IsActive = false;      // ← Borra historial activo
    assignment.EndDate = DateTime.UtcNow;
}
// ... luego agrega las nuevas
```

**Efecto**: Al asignar al estudiante a un nuevo grupo, se inactivan todos los grupos anteriores. Es imposible agregar una asignación incremental con este método.

### 3.3 UpdateGroupAndGrade() — patrón "borrar y reemplazar"

**Archivo**: `Controllers/StudentAssignmentController.cs`  
```csharp
await _studentAssignmentService.RemoveAssignmentsAsync(studentId);  // Inactiva todo
var newAssignment = new StudentAssignment { ... };                   // Crea uno nuevo
await _studentAssignmentService.InsertAsync(newAssignment);
```

Mismo patrón destructivo: asume que el estudiante solo puede estar en un grupo a la vez.

### 3.4 StudentAssignmentRequest no vincula materia ↔ grupo

**Archivo**: `ViewModels/StudentAssignmentRequest.cs`  
```csharp
public Guid UserId { get; set; }
public Guid SubjectId { get; set; }    // Una materia
public Guid GradeId { get; set; }      // Un grado
public List<Guid> GroupIds { get; set; } // Varios grupos del MISMO grado
```

El modelo no permite expresar: "este estudiante cursa Matemáticas en Grupo A y Biología en Grupo B".

### 3.5 ViewModel de listado pierde la estructura

**Archivo**: `ViewModels/StudentAssignmentOverviewViewModel.cs`  
```csharp
public List<string> GradeGroupPairs { get; set; } = new();
// Ejemplo: ["Grado 10 - Grupo A", "Grado 10 - Grupo B"]
```

Son strings planos. No se puede derivar de ellos: qué materias tiene el estudiante en cada grupo, ni qué jornada, ni el estado de cada asignación.

### 3.6 Calificaciones con UNIQUE por actividad

**Tabla**: `student_activity_scores`  
```sql
UNIQUE (student_id, activity_id)   -- uq_scores
```

Un estudiante solo puede tener UNA nota por actividad. Si el mismo estudiante está en dos grupos que usan la misma actividad (mismo `activity_id`), hay conflicto.

### 3.7 Vista de carga masiva (Excel) sin columna de materia

**Archivo**: `Views/StudentAssignment/Upload.cshtml`  

Columnas actuales del Excel:
```
Email | Nombre | Apellido | Doc | F.Nac | Grado | Grupo | Jornada | Inclusión
```

No hay columna de materia específica. Las materias se heredan del grupo vía `SubjectAssignment`. Para un nocturno con materias selectivas por grupo, esto es insuficiente.

### 3.8 TeacherGradebook filtra por un único grupo

**Archivo**: `Controllers/TeacherGradebookController.cs`  
```csharp
var students = await _studentService.GetByGroupAndGradeAsync(
    notes.GroupId,      // UN grupo
    notes.GradeLevelId  // UN grado
);
```

Si un estudiante está en Grupo A y Grupo B, aparecerá en las consultas del docente del Grupo A, pero no en las del Grupo B (a menos que también esté asignado allí). El sistema no maneja el caso donde un mismo docente tenga al mismo estudiante en dos grupos paralelos.

---

## 4. Riesgos del Cambio

### Riesgo 1 — CRÍTICO: Pérdida de datos históricos
**AssignAsync()** inactiva asignaciones previas. Si se modificara esta función sin cuidado, se podría perder el historial de qué grupos cursó el estudiante.

**Mitigación**: Diseñar la nueva lógica como "agregar asignación" (nunca borrar), usando `IsActive + EndDate` correctamente como patrón de historial.

### Riesgo 2 — ALTO: Duplicación de notas
Si un estudiante nocturno está en dos grupos que imparten la misma materia, y ambos docentes cargan notas, el constraint `UNIQUE(student_id, activity_id)` provocará error en la segunda inserción.

**Mitigación**: Las actividades deben ser por grupo (no globales). El `activity_id` debe ser único por (materia, grupo, trimestre). Verificar el modelo `Activity`.

### Riesgo 3 — ALTO: Informes y boletines inconsistentes
Los reportes de notas, boletines y promedios actuales probablemente agregan todas las notas del estudiante sin distinguir por grupo. Si el estudiante tiene Matemáticas en dos grupos con dos docentes, el reporte podría promediar notas de ambos o duplicarlas.

**Mitigación**: Los reportes deben filtrar por asignación específica (grupo + materia), no solo por estudiante + materia.

### Riesgo 4 — MEDIO: Carnet y datos del estudiante
Los campos `Student.Grade` y `Student.GroupName` se usan en el módulo de carnet digital (visto en análisis previo del sistema). Si el estudiante tiene múltiples grupos, el carnet mostraría solo uno.

**Mitigación**: El carnet debería listar todos los grupos o mostrar solo el grupo "principal" (definible por el administrador).

### Riesgo 5 — MEDIO: Conflictos de horario
Un estudiante nocturno en dos grupos podría tener actividades superpuestas en el horario. El sistema actual no tiene validación de conflictos de horario para estudiantes.

**Mitigación**: Agregar validación en la asignación que verifique solapamiento de horarios si el módulo de Schedule existe.

### Riesgo 6 — BAJO: Tablas N:M no usadas (user_groups, user_grades, user_subjects)
Estas tablas existen pero no son la fuente de verdad. Si el cambio las activa, podrían desincronizarse con `StudentAssignment`.

**Mitigación**: Decidir si eliminarlas o hacerlas derivadas de `StudentAssignment` via trigger/servicio.

---

## 5. Recomendación de Arquitectura para Nocturno

### Principio guía
> "Un estudiante nocturno es un estudiante con **múltiples matrículas activas simultáneas**, cada una con su propio contexto de grupo, materias y calificaciones."

El error conceptual del sistema actual es tratar la asignación como "estado del estudiante" (uno solo). Debe tratarse como **matrícula** (puede haber varias activas).

### Patrón recomendado: Matrícula como entidad de primera clase

```
Enrollment (nueva entidad conceptual, o renombrar StudentAssignment)
 ├── StudentId         (estudiante)
 ├── GroupId           (grupo específico)
 ├── GradeLevelId      (grado del grupo)
 ├── ShiftId           (jornada del grupo)
 ├── AcademicYearId    (año académico)
 ├── EnrollmentType    ("Regular" | "Nocturno" | "Refuerzo" | "Libre")
 ├── IsActive          (para historial)
 ├── StartDate         (cuándo inició esta matrícula)
 └── EndDate           (cuándo terminó, null si activa)
```

Cada matrícula tiene su propio ciclo de vida. Un estudiante nocturno tiene 2-3 matrículas activas; un estudiante regular tiene 1.

### Separación de notas por matrícula

```
StudentActivityScore
 ├── StudentId
 ├── ActivityId
 ├── EnrollmentId   ← NUEVO: la nota pertenece a UNA matrícula específica
 └── Score
```

Esto elimina la ambigüedad y el conflicto de `UNIQUE(student_id, activity_id)`. El nuevo unique sería `UNIQUE(enrollment_id, activity_id)`.

---

## 6. Propuesta de Nuevo Modelo (sin implementar)

### 6.1 Cambios en modelos existentes

#### Student — eliminar campos denormalizados
```
ELIMINAR: Grade (string)
ELIMINAR: GroupName (string)
AGREGAR:  PrimaryEnrollmentId (Guid?) → FK a StudentAssignment
          (permite designar cuál es el grupo "principal" para el carnet y reportes)
```

#### StudentAssignment — agregar EnrollmentType
```
AGREGAR: EnrollmentType (string) → "Regular" | "Nocturno" | "Refuerzo" | "Libre"
AGREGAR: StartDate (DateTime) → cuándo inició (actualmente solo CreatedAt)
MANTENER: IsActive, EndDate para historial
```

#### StudentActivityScore — vincular a matrícula
```
AGREGAR: StudentAssignmentId (Guid) → FK a student_assignments
CAMBIAR: UNIQUE de (student_id, activity_id) a (student_assignment_id, activity_id)
```

### 6.2 Nuevo flujo de asignación de estudiante nocturno

```
Administrador crea Matrícula 1:
  Estudiante X → Grado 10 → Grupo Noche-A → Jornada Noche → Tipo: Nocturno

Administrador crea Matrícula 2 (sin eliminar la 1):
  Estudiante X → Grado 10 → Grupo Noche-B → Jornada Noche → Tipo: Nocturno

Ambas matrículas coexisten activas.
Las notas del docente de Grupo Noche-A van a Matrícula 1.
Las notas del docente de Grupo Noche-B van a Matrícula 2.
El boletín consolida ambas matrículas.
```

### 6.3 Cambio en carga masiva (Excel)

Agregar columna `TipoMatricula` al formato de importación:
```
Email | Nombre | Apellido | Doc | F.Nac | Grado | Grupo | Jornada | Inclusión | TipoMatricula
x@x.com | Juan | P | 123 | 01/01 | 10 | Noche-A | Noche | No | Nocturno
x@x.com | Juan | P | 123 | 01/01 | 10 | Noche-B | Noche | No | Nocturno
```

El importador ya maneja múltiples filas por estudiante; solo faltaría el tipo.

### 6.4 Nuevo ViewModel para asignaciones

```csharp
// Reemplaza GradeGroupPairs (List<string>) por lista estructurada:
public List<EnrollmentSummaryDto> Enrollments { get; set; }

public class EnrollmentSummaryDto
{
    public Guid AssignmentId { get; set; }
    public string GradeName { get; set; }
    public string GroupName { get; set; }
    public string ShiftName { get; set; }
    public string EnrollmentType { get; set; }
    public List<string> Subjects { get; set; }
    public bool IsActive { get; set; }
    public DateTime StartDate { get; set; }
}
```

---

## 7. Lista de Cambios Necesarios por Módulo

### BASE DE DATOS

| # | Cambio | Tabla afectada | Tipo | Descripción |
|---|--------|---------------|------|-------------|
| BD-1 | Agregar columna `enrollment_type` | `student_assignments` | ALTER TABLE | Valores: Regular, Nocturno, Refuerzo, Libre |
| BD-2 | Agregar columna `start_date` | `student_assignments` | ALTER TABLE | Fecha de inicio efectiva de la matrícula |
| BD-3 | Agregar columna `primary_enrollment_id` | `students` | ALTER TABLE | FK a student_assignments, grupo principal |
| BD-4 | Eliminar columnas `Grade`, `GroupName` | `students` | ALTER TABLE | Eliminar datos denormalizados obsoletos |
| BD-5 | Agregar columna `student_assignment_id` | `student_activity_scores` | ALTER TABLE | FK a la matrícula específica de la nota |
| BD-6 | Reemplazar UNIQUE constraint | `student_activity_scores` | DROP + CREATE | De (student_id, activity_id) a (student_assignment_id, activity_id) |
| BD-7 | Migración de datos | Todas | DATA MIGRATION | Poblar `enrollment_type = 'Regular'`, `start_date = created_at`, sincronizar `primary_enrollment_id` |

### BACKEND — Modelos

| # | Archivo | Cambio | Descripción |
|---|---------|--------|-------------|
| M-1 | `Models/Student.cs` | Eliminar `Grade`, `GroupName`; agregar `PrimaryEnrollmentId` | Quitar denormalización |
| M-2 | `Models/StudentAssignment.cs` | Agregar `EnrollmentType`, `StartDate` | Tipificar matrícula |
| M-3 | `Models/StudentActivityScore.cs` | Agregar `StudentAssignmentId` | Vincular nota a matrícula |

### BACKEND — Servicios

| # | Archivo | Método | Cambio | Descripción |
|---|---------|--------|--------|-------------|
| S-1 | `StudentAssignmentService.cs` | `AssignAsync()` | Eliminar bloque "inactivar todo" | Cambiar de "reemplazar" a "agregar" |
| S-2 | `StudentAssignmentService.cs` | `RemoveAssignmentsAsync()` | Agregar parámetro `assignmentId` | Permitir inactivar una sola matrícula, no todas |
| S-3 | `StudentAssignmentService.cs` | `AssignStudentAsync()` | Eliminar param `subjectId` no usado; mantener lógica | Ya funciona correctamente |
| S-4 | `StudentAssignmentService.cs` | **NUEVO** `AddEnrollmentAsync()` | Agregar matrícula sin afectar las existentes | Método incremental |
| S-5 | `StudentActivityScoreService.cs` | `GetNotasPorFiltroAsync()` | Agregar filtro por `AssignmentId` | Notas por matrícula específica |

### BACKEND — Controladores

| # | Archivo | Método | Cambio | Descripción |
|---|---------|--------|--------|-------------|
| C-1 | `StudentAssignmentController.cs` | `UpdateGroupAndGrade()` | Reescribir para no llamar `RemoveAssignmentsAsync` globalmente | Cambiar a "agregar matrícula" |
| C-2 | `StudentAssignmentController.cs` | `GuardarAsignacion()` | Cambiar `StudentAssignmentRequest` para incluir `EnrollmentType` | Tipificar matrícula en creación |
| C-3 | `StudentAssignmentController.cs` | `Index()` | Cambiar `GradeGroupPairs (List<string>)` a `List<EnrollmentSummaryDto>` | ViewModel estructurado |
| C-4 | `StudentAssignmentController.cs` | `SaveAssignments()` | Agregar campo `EnrollmentType` en `StudentAssignmentInputModel` | Soporte en carga masiva |
| C-5 | `TeacherGradebookController.cs` | `GuardarNotasTemp()` | Incluir `AssignmentId` en el registro de nota | Vincular nota a matrícula |
| C-6 | `TeacherGradebookController.cs` | `GetNotasCargadas()` | Mantener filtro por grupo (correcto), agregar `AssignmentId` en respuesta | Sin cambio mayor |

### FRONTEND — ViewModels

| # | Archivo | Cambio |
|---|---------|--------|
| VM-1 | `StudentAssignmentOverviewViewModel.cs` | Cambiar `List<string> GradeGroupPairs` por `List<EnrollmentSummaryDto> Enrollments` |
| VM-2 | `StudentAssignmentViewModel.cs` | Cambiar `SelectedGrades`/`SelectedGroups` (listas sin relación) por `List<(GradeId, GroupId, ShiftId, Type)>` |
| VM-3 | `StudentAssignmentRequest.cs` | Agregar `EnrollmentType` |
| VM-4 | `StudentAssignmentInputModel.cs` | Agregar `EnrollmentType` (para carga Excel) |

### FRONTEND — Vistas

| # | Archivo | Cambio |
|---|---------|--------|
| V-1 | `Views/StudentAssignment/Index.cshtml` | Mostrar lista de matrículas estructurada por estudiante (tipo, grupo, jornada) |
| V-2 | `Views/StudentAssignment/Upload.cshtml` | Agregar columna `TipoMatricula` en plantilla Excel y documentación |
| V-3 | Vista de asignación manual (Assign) | Reemplazar selects separados (Grado / Grupo) por UI de "agregar matrícula" con tipo |
| V-4 | `Views/TeacherGradebook/` | Mostrar indicador si el estudiante tiene matrículas en múltiples grupos |

### MÓDULO CARNET

| # | Archivo | Cambio |
|---|---------|--------|
| K-1 | `StudentIdCardService.cs` | Usar `PrimaryEnrollmentId` para obtener el grado/grupo del carnet |
| K-2 | `Generate.cshtml` | Si el estudiante no tiene `PrimaryEnrollmentId`, mostrar todos los grupos |

---

## 8. Impacto Técnico por Módulo

| Módulo | Impacto | Justificación |
|--------|---------|---------------|
| **Base de datos** | 🔴 ALTO | 3 ALTER TABLE + DROP/CREATE UNIQUE + migración de datos existentes |
| **StudentAssignmentService** | 🔴 ALTO | El método `AssignAsync()` requiere cambio de paradigma (de "reemplazar" a "agregar") |
| **StudentAssignmentController** | 🔴 ALTO | 4 métodos afectados, ViewModel completo a reescribir |
| **TeacherGradebookController** | 🟡 MEDIO | Ajuste en cómo se guardan notas (nuevo campo `AssignmentId`) |
| **StudentActivityScoreService** | 🟡 MEDIO | Cambio en constraint UNIQUE afecta queries existentes |
| **Student Model** | 🟡 MEDIO | Eliminar campos requiere auditar todos los que los usan |
| **Vistas de asignación** | 🟡 MEDIO | Rediseño de formularios de asignación y listado |
| **Carga masiva (Excel)** | 🟢 BAJO | Solo agregar una columna; la lógica base ya soporta múltiples filas por estudiante |
| **Módulo Carnet** | 🟢 BAJO | Usar `PrimaryEnrollmentId`; cambio mínimo en la vista |
| **Reportes y boletines** | 🔴 ALTO | Requieren filtrar por matrícula, no por estudiante global |
| **Módulo de horarios** | 🟡 MEDIO | Validación de solapamiento de horarios para el mismo estudiante |

---

## 9. Dependencias Críticas

### Dependencia 1 — Migración de datos antes que código
El campo `enrollment_type` en `student_assignments` y el `primary_enrollment_id` en `students` deben existir y estar poblados **antes** de desplegar el código que los usa. Secuencia obligatoria:

```
1. Migración BD (ADD COLUMN)
2. Script de backfill (enrollment_type = 'Regular', primary_enrollment_id = primera asignación activa)
3. Deploy de código nuevo
```

### Dependencia 2 — UNIQUE constraint en StudentActivityScore
No se puede cambiar el UNIQUE de `(student_id, activity_id)` a `(student_assignment_id, activity_id)` si ya existen notas sin `student_assignment_id` poblado. Secuencia:

```
1. ADD COLUMN student_assignment_id (nullable)
2. Script de backfill: inferir la matrícula correcta para cada nota existente
3. Verificar que no quedan NULLs
4. DROP UNIQUE (student_id, activity_id)
5. ADD UNIQUE (student_assignment_id, activity_id)
6. ALTER COLUMN student_assignment_id SET NOT NULL
```

### Dependencia 3 — Student.Grade y Student.GroupName
Antes de eliminar estos campos, auditar **todo el código** que los usa (búsqueda por `student.Grade` y `student.GroupName`, incluyendo el módulo de carnet digital). Cada uso debe migrarse a leer desde `StudentAssignment` o `PrimaryEnrollmentId`.

### Dependencia 4 — AssignAsync() es el punto más peligroso
Este método es llamado desde múltiples controladores. Cualquier cambio en él afecta todos los flujos de asignación. Debe:
- Mantenerse la firma existente para compatibilidad (o versionarse)
- Crear un método nuevo `AddEnrollmentAsync()` para el flujo nocturno
- Migrar gradualmente los llamadores

### Dependencia 5 — Actividades globales vs. por grupo
Si las `Activity` son globales a la escuela (no por grupo), el constraint `UNIQUE(assignment_id, activity_id)` solo funciona si las actividades son únicas por grupo. Si dos grupos comparten la misma actividad (mismo `activity_id`), el nuevo constraint sería incompleto. **Verificar el modelo de `Activity` antes de implementar BD-6**.

---

## 10. Conclusión Final

| Pregunta | Respuesta |
|----------|-----------|
| ¿Qué tan preparado está el sistema? | **Parcialmente**. La BD lo permite; el código lo impide. |
| ¿Qué tan grande es el cambio? | **Grande pero acotado**. No requiere reescritura; requiere extensión quirúrgica en ~15 puntos identificados. |
| ¿Hay riesgo de pérdida de datos? | **Sí, si no se sigue la secuencia de migración correcta**. El mayor riesgo es el campo `student_assignment_id` en scores. |
| ¿Afecta a estudiantes regulares? | **Mínimamente**. El tipo `Regular` es el default; los flujos actuales continúan funcionando. |
| ¿Cuánto del sistema hay que tocar? | 3 modelos, 2 servicios, 2 controladores, 4 ViewModels, 4 vistas, 7 cambios de BD. |
| ¿Es recomendable hacerlo? | **Sí**, si hay demanda real de estudiantes nocturnos. El diseño de `StudentAssignment` ya fue pensado para historial; solo falta terminar la idea. |

---

*Documento generado para uso interno del equipo técnico de EduPlaner / SchoolManager.*  
*Análisis basado en lectura directa del código fuente. Sin modificaciones realizadas.*
