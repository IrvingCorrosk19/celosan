# ANEXO TÉCNICO — SOPORTE JORNADA NOCTURNA: INVENTARIO DETALLADO

---

| Campo        | Valor                                      |
|--------------|--------------------------------------------|
| **Título**   | Anexo Técnico: Inventario de Tablas, Constraints, Métodos Críticos, Queries Peligrosas y Vistas Problemáticas |
| **Fecha**    | 2026-04-18                                 |
| **Versión**  | 1.0 — Inicial                              |
| **Referencia** | Complementa `ANALISIS_SOPORTE_NOCTURNA_EDUPLANER.md` |
| **Base de datos** | `schoolmanager_daqf` @ Render PostgreSQL 18 |
| **Rama auditada** | `main` (commit `2520c69`)          |

---

## SECCIÓN A — INVENTARIO DE TABLAS (estructura nocturna-relevante)

### A.1 `student_assignments`

**Tabla física**: `student_assignments`  
**Modelo C#**: `Models/StudentAssignment.cs`  
**Configuración ORM**: `Models/SchoolDbContext.cs` línea ~956

| Columna | Tipo SQL | Nullable | Valor defecto | Relevancia nocturna |
|---------|----------|----------|---------------|---------------------|
| `id` | uuid | NO | gen_random_uuid() | PK |
| `student_id` | uuid | NO | — | FK a users |
| `grade_id` | uuid | NO | — | FK a grade_levels |
| `group_id` | uuid | NO | — | FK a groups |
| `shift_id` | uuid | SI | NULL | FK a shifts — CRITICO para nocturna |
| `is_active` | boolean | NO | true | Soft-delete |
| `end_date` | timestamptz | SI | NULL | Fin matrícula |
| `academic_year_id` | uuid | SI | NULL | Año académico |
| `enrollment_type` | varchar(20) | NO | 'Regular' | 'Nocturno', 'Regular', 'Refuerzo', 'Libre' |
| `start_date` | timestamptz | SI | NULL | Inicio efectivo |
| `created_at` | timestamptz | SI | NOW() | Creación |

**Constraints e índices** (SchoolDbContext.cs ~960–1025):

```
PK:   student_assignments_pkey (id)
IDX:  IX_student_assignments_student_id (student_id)
IDX:  IX_student_assignments_grade_id (grade_id)
IDX:  IX_student_assignments_group_id (group_id)
IDX:  IX_student_assignments_shift_id (shift_id)
IDX:  IX_student_assignments_student_active (student_id, is_active)
IDX:  IX_student_assignments_student_academic_year (student_id, academic_year_id)
```

**AUSENCIA CRITICA**: NO existe ningún UNIQUE index sobre `(student_id, grade_id, group_id)`. Esto es INTENCIONAL — permite múltiples matrículas activas por estudiante, que es el diseño requerido para nocturna. Sin embargo, la ausencia de constraint significa que la protección contra duplicados depende 100% de la lógica de negocio en `StudentAssignmentService`.

---

### A.2 `student_subject_assignments`

**Tabla física**: `student_subject_assignments`  
**Modelo C#**: `Models/StudentSubjectAssignment.cs`  
**Configuración ORM**: `Models/SchoolDbContext.cs` línea ~1030

| Columna | Tipo SQL | Nullable | Valor defecto | Relevancia nocturna |
|---------|----------|----------|---------------|---------------------|
| `id` | uuid | NO | gen_random_uuid() | PK |
| `student_id` | uuid | NO | — | FK a users |
| `subject_assignment_id` | uuid | NO | — | FK a subject_assignments |
| `student_assignment_id` | uuid | SI | NULL | FK a student_assignments — NULLABLE (riesgo histórico) |
| `academic_year_id` | uuid | SI | NULL | Año académico |
| `shift_id` | uuid | SI | NULL | FK a shifts |
| `enrollment_type` | varchar(20) | NO | 'Regular' | Tipo matrícula |
| `is_active` | boolean | NO | true | Soft-delete |
| `start_date` | timestamptz | SI | NULL | Inicio |
| `end_date` | timestamptz | SI | NULL | Fin |
| `school_id` | uuid | SI | NULL | Multi-tenant |

**Constraints e índices** (SchoolDbContext.cs ~1034–1079):

```
PK:   student_subject_assignments_pkey (id)
IDX:  IX_student_subject_assignments_student_id (student_id)
IDX:  IX_student_subject_assignments_subject_assignment_id (subject_assignment_id)
IDX:  IX_student_subject_assignments_student_assignment_id (student_assignment_id)
IDX:  IX_student_subject_assignments_academic_year_id (academic_year_id)
IDX:  IX_student_subject_assignments_shift_id (shift_id)

UNIQUE PARCIAL:
  ix_student_subject_assignments_active_unique
  ON (student_id, subject_assignment_id, academic_year_id)
  WHERE (is_active = true)
```

**Análisis del UNIQUE parcial**: Este índice evita que un estudiante se inscriba dos veces en la misma combinación `(subject_assignment_id, academic_year_id)` mientras esté activa. Para nocturna, esto funciona correctamente siempre que dos grupos nocturnos de la misma materia tengan `subject_assignment_id` diferentes (lo cual ocurre porque `SubjectAssignment` incluye `GroupId` como parte de la entidad). El riesgo existe si por algún error se intenta reutilizar el mismo `subject_assignment_id` para dos grupos nocturnos distintos del mismo estudiante.

**RIESGO**: `student_assignment_id` es nullable. Registros históricos sin este vínculo no pueden ser rastreados a su contexto de matrícula. `StudentSubjectAssignment` sin `StudentAssignmentId` no puede distinguir a qué de las múltiples matrículas activas pertenece.

---

### A.3 `student_activity_scores`

**Tabla física**: `student_activity_scores`  
**Modelo C#**: `Models/StudentActivityScore.cs`  
**Configuración ORM**: `Models/SchoolDbContext.cs` línea ~887

| Columna | Tipo SQL | Nullable | Valor defecto |
|---------|----------|----------|---------------|
| `id` | uuid | NO | gen_random_uuid() |
| `student_id` | uuid | NO | — |
| `activity_id` | uuid | NO | — |
| `student_assignment_id` | uuid | NO | — |
| `student_subject_assignment_id` | uuid | SI | NULL |
| `score` | numeric(2,1) | SI | NULL |
| `created_at` | timestamptz | SI | NOW() |

**Constraints e índices** (SchoolDbContext.cs ~891–897):

```
PK:   student_activity_scores_pkey (id)
IDX:  idx_scores_activity (activity_id)
IDX:  idx_scores_student (student_id)

UNIQUE:
  uq_scores_assignment_activity (student_assignment_id, activity_id)
  uq_scores_subject_enrollment_activity (student_subject_assignment_id, activity_id)
```

**Análisis**: `student_assignment_id` es NOT NULL — cada nota está siempre vinculada a una matrícula específica. Esto es correcto para nocturna: dos matrículas activas del mismo estudiante tienen IDs diferentes, entonces los dos UNIQUE índices garantizan que no habrá colisiones de notas entre contextos. El constraint hace lo correcto, pero depende de que `ResolveStudentAssignmentIdAsync` resuelva al `StudentAssignmentId` correcto (ver Sección C.3).

---

### A.4 `attendance`

**Tabla física**: `attendance`  
**Modelo C#**: `Models/Attendance.cs`  
**Configuración ORM**: `Models/SchoolDbContext.cs` línea ~323

**Constraints e índices** relevantes (SchoolDbContext.cs ~327–380):

```
IDX:  IX_attendance_student_id (student_id)
IDX:  IX_attendance_grade_id (grade_id)
IDX:  IX_attendance_group_id (group_id)
IDX:  IX_attendance_teacher_id (teacher_id)
IDX:  IX_attendance_shift_id (shift_id)
IDX:  IX_attendance_student_assignment_id (student_assignment_id)

UNIQUE:
  ix_attendance_student_date_group_grade_shift
  ON (student_id, date, group_id, grade_id, shift_id)
```

**Análisis**: El UNIQUE index incluye `shift_id`. Esto significa que un estudiante nocturno puede tener registro de asistencia para el grupo A (shift_id=NOCHE) y para el grupo B (mismo shift_id=NOCHE) en la misma fecha SIN violar el constraint, porque el `group_id` difiere. Esto es correcto para el modelo nocturno.

---

### A.5 `groups`

**Tabla física**: `groups`  
**Modelo C#**: `Models/Group.cs`

**Columnas de riesgo**:

| Columna | Tipo | Problema |
|---------|------|----------|
| `shift` | varchar | Campo legacy de texto. Comentado "mantener por compatibilidad" en el modelo. |
| `shift_id` | uuid | FK a shifts — campo moderno |
| `grade` | varchar | Campo texto legacy (e.g., "7°"). Usado en lógica de AprobadosReprobados. |

**Riesgo de consistencia**: Existen dos campos de jornada en `groups`: `shift` (texto) y `shift_id` (FK). Si no se sincronizan, la búsqueda por texto puede diferir de la búsqueda por ID.

---

### A.6 `shifts`

**Tabla física**: `shifts`  
**Datos en BD** (resultado de query confirmada):

| id (uuid) | name |
|-----------|------|
| (uuid-1) | Mañana |
| (uuid-2) | Tarde |
| (uuid-3) | Noche |

Tres jornadas configuradas. La jornada Noche está registrada y tiene FK válida desde múltiples tablas.

---

## SECCIÓN B — CONSTRAINTS COMPLETOS NOCTURNA-RELEVANTES

### B.1 Tabla de constraints críticos

| Tabla | Constraint / Índice | Tipo | Columnas | Impacto nocturna |
|-------|---------------------|------|----------|------------------|
| `student_assignments` | (ninguno) | — | — | CORRECTO: permite múltiples matrículas por estudiante |
| `student_assignments` | `IX_student_assignments_student_active` | Índice | (student_id, is_active) | Eficiencia de queries por estudiante activo |
| `student_subject_assignments` | `ix_student_subject_assignments_active_unique` | UNIQUE PARCIAL WHERE is_active=true | (student_id, subject_assignment_id, academic_year_id) | Previene doble-inscripción en misma materia/año. Correcto si subject_assignments son distintos por grupo |
| `student_activity_scores` | `uq_scores_assignment_activity` | UNIQUE | (student_assignment_id, activity_id) | Previene doble nota por actividad. Correcto — cada matrícula tiene su propio ID |
| `student_activity_scores` | `uq_scores_subject_enrollment_activity` | UNIQUE | (student_subject_assignment_id, activity_id) | Redundante con anterior. Correcto |
| `attendance` | `ix_attendance_student_date_group_grade_shift` | UNIQUE | (student_id, date, group_id, grade_id, shift_id) | CORRECTO para nocturna — shift_id incluido |

---

## SECCIÓN C — MÉTODOS CRÍTICOS (con referencias exactas archivo:línea)

### C.1 `StudentAssignmentService.AssignAsync` — RIESGO ALTO

**Archivo**: `Services/Implementations/StudentAssignmentService.cs`  
**Línea**: ~227  
**Firma**:
```csharp
public async Task AssignAsync(
    Guid studentId,
    List<(Guid SubjectId, Guid GradeId, Guid GroupId)> assignments,
    bool replaceExistingActive = true)
```

**Código peligroso** (líneas ~233–248):
```csharp
if (replaceExistingActive)
{
    var existing = await _context.StudentAssignments
        .Where(a => a.StudentId == studentId && a.IsActive)
        .ToListAsync();

    foreach (var assignment in existing)
    {
        assignment.IsActive = false;
        assignment.EndDate = DateTime.UtcNow;
    }
    _context.StudentAssignments.UpdateRange(existing);
}
```

**Problema**: Cuando `replaceExistingActive = true` (valor por defecto), se inactivan TODAS las matrículas activas del estudiante sin filtrar por jornada o grupo. Un estudiante nocturno con 2 matrículas activas (ESP7/Noche y ESP8/Noche) perdería AMBAS si se llama este método con el valor por defecto.

**Callers conocidos**: `UpdateGroupAndGrade` en `Controllers/StudentAssignmentController.cs` línea ~78 cuando `additive = false` (que es el valor por defecto de ese parámetro también).

---

### C.2 `StudentAssignmentService.AddEnrollmentAsync` — CORRECTO

**Archivo**: `Services/Implementations/StudentAssignmentService.cs`  
**Línea**: ~310 (aprox.)

Este método fue diseñado específicamente para nocturna. Llama `ExistsWithShiftAsync` antes de agregar, evitando duplicados por jornada. No inactiva matrículas existentes.

**Riesgo residual**: Si `group.ShiftId` es null, `ExistsWithShiftAsync` puede comportarse incorrectamente al comparar con null. Revisar comportamiento cuando `ShiftId` no está poblado en el grupo.

---

### C.3 `StudentActivityScoreService.ResolveStudentAssignmentIdAsync` — RIESGO MEDIO

**Archivo**: `Services/Implementations/StudentActivityScoreService.cs`  
**Línea**: ~36

```csharp
private async Task<Guid?> ResolveStudentAssignmentIdAsync(
    Guid studentId, Guid? activityGroupId, Guid? activityGradeLevelId)
{
    var q = _context.StudentAssignments
        .Where(sa => sa.StudentId == studentId && sa.IsActive);
    if (activityGroupId.HasValue && activityGroupId.Value != Guid.Empty)
        q = q.Where(sa => sa.GroupId == activityGroupId.Value);
    if (activityGradeLevelId.HasValue && activityGradeLevelId.Value != Guid.Empty)
        q = q.Where(sa => sa.GradeId == activityGradeLevelId.Value);
    return await q
        .OrderByDescending(sa => sa.CreatedAt ?? DateTime.MinValue)
        .Select(sa => (Guid?)sa.Id)
        .FirstOrDefaultAsync();
}
```

**Análisis**: Filtra por `GroupId` Y `GradeId` si están disponibles, lo cual es correcto para distinguir entre dos matrículas nocturnas en grupos distintos. El `FirstOrDefaultAsync` al final solo sería problemático si un estudiante tuviera DOS matrículas activas en el mismo `GroupId + GradeId`, escenario que no debería ocurrir normalmente. El riesgo es bajo para el caso de uso típico nocturno.

---

### C.4 `StudentActivityScoreService.ResolveStudentSubjectAssignmentIdAsync` — RIESGO MEDIO

**Archivo**: `Services/Implementations/StudentActivityScoreService.cs`  
**Línea**: ~49

```csharp
var subjectAssignmentId = await _context.SubjectAssignments
    .Where(sa => sa.SubjectId == subjectId
        && (!activityGroupId.HasValue || sa.GroupId == activityGroupId.Value)
        && (!activityGradeLevelId.HasValue || sa.GradeLevelId == activityGradeLevelId.Value))
    .Select(sa => (Guid?)sa.Id)
    .FirstOrDefaultAsync();
```

**Riesgo**: Si `activityGroupId` y `activityGradeLevelId` son ambos null, la query retorna el primer `SubjectAssignment` encontrado para esa materia, sin filtrar por grupo/grado. En ese caso, un estudiante nocturno matriculado en Matemáticas del ESP7 y del ESP8 podría tener sus notas resueltas al contexto incorrecto.

---

### C.5 `ScheduleService.GetByStudentUserAsync` — RIESGO ALTO

**Archivo**: `Services/Implementations/ScheduleService.cs`  
**Línea**: ~203

```csharp
public async Task<List<ScheduleEntry>> GetByStudentUserAsync(
    Guid studentUserId, Guid academicYearId)
{
    // ...

    var assignment = await _context.StudentAssignments
        .AsNoTracking()
        .Include(sa => sa.Group)
        .Where(sa =>
            sa.StudentId == studentUserId &&
            sa.IsActive &&
            (sa.AcademicYearId == academicYearId || sa.AcademicYearId == null))
        .OrderByDescending(sa => sa.AcademicYearId != null)
        .ThenByDescending(sa => sa.CreatedAt)
        .FirstOrDefaultAsync(CancellationToken.None)   // <-- SOLO TOMA UNA
        .ConfigureAwait(false);

    if (assignment == null || assignment.Group == null)
        return new List<ScheduleEntry>();

    return await GetByGroupAsync(assignment.GroupId, academicYearId)
        .ConfigureAwait(false);
}
```

**Problema concreto**: Un estudiante nocturno con matrículas activas en ESP7 (lunes/miércoles) y S1 (martes/jueves) solo verá el horario de uno de ellos. El criterio de desempate es `CreatedAt DESC` — verá el último creado. El horario del otro grupo es invisible para el estudiante desde su vista personal.

**Impacto directo**: El módulo de horarios para el rol "estudiante" es disfuncional para multi-matrícula nocturna. Para el rol "docente" o "admin", la vista funciona correctamente porque usa `GetByGroupAsync` directamente con el grupo seleccionado.

---

### C.6 `AprobadosReprobadosService.ObtenerGradosPorNivel` — RIESGO ALTO

**Archivo**: `Services/Implementations/AprobadosReprobadosService.cs`  
**Línea**: ~293

```csharp
private List<string> ObtenerGradosPorNivel(string nivelEducativo)
{
    return nivelEducativo.ToLower() switch
    {
        "premedia" => new List<string> { "7°", "8°", "9°" },
        "media"    => new List<string> { "10°", "11°", "12°" },
        _          => new List<string>()   // <-- cualquier otro nivel = lista vacía
    };
}
```

**Problema**: Dos issues combinados:
1. Solo reconoce "premedia" y "media". Cualquier nivel nocturno diferente (e.g., "nocturna", "esp", etc.) retorna lista vacía.
2. Los grados "ESP7", "ESP8", "ESP9" no están contemplados.

**Cascada**: Esta lista vacía provoca que `GenerarReporte` produzca un reporte vacío o excluya completamente a los grupos nocturnos especiales.

---

### C.7 `AprobadosReprobadosService.ObtenerNivelesEducativosAsync` — RIESGO ALTO

**Archivo**: `Services/Implementations/AprobadosReprobadosService.cs`  
**Línea**: ~327

```csharp
public async Task<List<string>> ObtenerNivelesEducativosAsync()
{
    return await Task.FromResult(new List<string> { "Premedia", "Media" });
}
```

**Problema**: Hardcodea exactamente dos niveles educativos. No consulta la base de datos. El usuario del módulo jamás verá "Nocturna" como opción de filtro, haciendo imposible generar el reporte para alumnos nocturnos desde la UI.

---

### C.8 `AprobadosReprobadosService.PrepararDatosParaReporteAsync` — RIESGO ALTO

**Archivo**: `Services/Implementations/AprobadosReprobadosService.cs`  
**Línea**: ~630

```csharp
var groupNamesByGrade = new Dictionary<string, string[]>
{
    ["7°"]  = new[] { "A", "A1", "A2" },
    ["8°"]  = new[] { "B", "C", "C1", "C2" },
    ["9°"]  = new[] { "D", "E", "E1", "E2" },
    ["10°"] = new[] { "F", "G", "H" },
    ["11°"] = new[] { "I", "J", "K" },
    ["12°"] = new[] { "L", "M", "N" }
};
```

**Problema**: Lista completamente hardcodeada de grupos por grado. Grupos nocturnos como ESP7, ESP8, ESP9, S, S1, S2, TM1 no aparecen. La función `PrepararDatosParaReporte` actualiza la columna `Grade` de la tabla `groups` basándose en esta lista estática, lo que significa que grupos nocturnos existentes no reciben su `Grade` asignado por esta ruta de código.

**Efecto secundario grave**: Esta función también modifica activities (`a.Trimester = "3T"`) para TODA la escuela, lo cual puede afectar datos de jornada diurna incorrectamente.

---

### C.9 `StudentReportService` — múltiples FirstOrDefault nocturna-inseguros

**Archivo**: `Services/Implementations/StudentReportService.cs`  
**Líneas**: ~145–156, ~429–440

**Patrón repetido** (línea ~145):
```csharp
var studentAssignment = await _context.StudentAssignments
    .Where(sa => sa.StudentId == studentId && sa.IsActive)
    .OrderByDescending(sa => sa.StartDate ?? sa.CreatedAt)
    // Sin filtro por grupo ni jornada
    .Join(_context.GradeLevels, ...)
    .Join(_context.Groups, ...)
    .FirstOrDefaultAsync();
```

**Problema**: Para un estudiante nocturno con 2 matrículas activas, el reporte académico mostrará solo el grado/grupo de la matrícula más reciente (por `StartDate DESC`). Las calificaciones de todas las matrículas aún se obtienen correctamente (línea ~158 usa `StudentSubjectAssignments` de forma amplia), pero el encabezado del reporte —nombre del grado y grupo del estudiante— será arbitrariamente uno de los dos grupos activos.

**Callers**: Al menos dos métodos del mismo servicio repiten este patrón: `GetReportAsync` (~línea 145) y el método de PDF (~línea 429).

---

### C.10 `StudentService.GetByGroupAndGradeAsync` — observación

**Archivo**: `Services/Implementations/StudentService.cs`  
**Línea**: ~57

```csharp
public async Task<IEnumerable<StudentBasicDto>> GetByGroupAndGradeAsync(
    Guid groupId, Guid gradeId)
{
    var result = await (from sa in _context.StudentAssignments
                        join student in _context.Users on sa.StudentId equals student.Id
                        // ...
                        where sa.GroupId == groupId && sa.GradeId == gradeId && sa.IsActive
                        // ...
                        select new StudentBasicDto { ... })
                       .ToListAsync();
    return result;
}
```

**Análisis**: Este método filtra por `GroupId + GradeId` específico, lo cual es correcto. Si un estudiante tiene 2 matrículas activas en grupos diferentes, aparecerá exactamente una vez en la lista de cada grupo. No hay riesgo de duplicación en este método.

**Contraste**: `GetBySubjectGroupAndGradeAsync` (línea ~81) usa `StudentSubjectAssignments` con `.Distinct()` al final. Esto sugiere que el equipo sospechó posibles duplicados en esa ruta.

---

### C.11 `StudentAssignmentController.UpdateGroupAndGrade` — RIESGO ALTO

**Archivo**: `Controllers/StudentAssignmentController.cs`  
**Línea**: ~55

```csharp
[HttpPost("/StudentAssignment/UpdateGroupAndGrade")]
public async Task<IActionResult> UpdateGroupAndGrade(
    Guid studentId, Guid gradeId, Guid groupId, bool additive = false)
```

**Problema**: El parámetro `additive` tiene valor por defecto `false`. Cualquier llamada a este endpoint que no pase explícitamente `additive=true` ejecutará `RemoveAssignmentsAsync` —que inactiva TODAS las matrículas activas del estudiante— antes de crear la nueva. Si la UI de administración llama a este endpoint sin `additive=true`, un estudiante nocturno perderá todas sus matrículas previas.

**Flujo cuando `additive = false`** (línea ~78–80):
```csharp
if (!additive)
{
    await _studentAssignmentService.RemoveAssignmentsAsync(studentId);
}
```

**Dónde se llama**: Buscar en Views y JS todos los fetch/POST a `/StudentAssignment/UpdateGroupAndGrade`. Si alguna vista de administración de estudiantes usa este endpoint sin `additive=true`, es una trampa para datos nocturnos.

---

### C.12 `StudentAssignmentService.BulkAssignFromFileAsync` — RIESGO MEDIO

**Archivo**: `Services/Implementations/StudentAssignmentService.cs`  
**Línea**: ~416 (aprox.)

```csharp
var student = await _context.Users.FirstOrDefaultAsync(u => u.Email == row.StudentEmail);
var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Code == row.SubjectCode);
var grade   = await _context.GradeLevels.FirstOrDefaultAsync(g => g.Name == row.GradeName);
var group   = await _context.Groups.FirstOrDefaultAsync(
                  g => g.Name == row.GroupName && g.Grade == row.GradeName);
```

**Problema**: La búsqueda de `group` usa `g.Grade == row.GradeName`, que compara el campo texto legacy `Grade` del grupo. Si `groups.grade` no está consistentemente poblado para grupos nocturnos (y la evidencia de `PrepararDatosParaReporte` muestra que no lo está), la búsqueda del grupo fallará silenciosamente.

**Riesgo adicional**: El método usa `ExistsAsync` (sin `ShiftId`) para verificar duplicados, lo cual puede no discriminar correctamente entre jornadas.

---

## SECCIÓN D — QUERIES PELIGROSAS (patrones SQL via LINQ)

### D.1 Query que toma solo una matrícula activa (ScheduleService)

**Archivo**: `Services/Implementations/ScheduleService.cs` línea ~215

```sql
-- Equivalente SQL de la query LINQ
SELECT sa.group_id
FROM student_assignments sa
WHERE sa.student_id = @studentUserId
  AND sa.is_active = true
  AND (sa.academic_year_id = @academicYearId OR sa.academic_year_id IS NULL)
ORDER BY
  (sa.academic_year_id IS NOT NULL) DESC,
  sa.created_at DESC
LIMIT 1;  -- FirstOrDefaultAsync
```

**Riesgo**: Para un estudiante nocturno con 2 filas activas, retorna arbitrariamente 1 grupo.

---

### D.2 Query de resolución de matrícula sin filtro completo (StudentReportService)

**Archivo**: `Services/Implementations/StudentReportService.cs` líneas ~145–156

```sql
-- Equivalente SQL de la query LINQ
SELECT sa.group_id, sa.grade_id, gl.name as grade_name, g.name as group_name
FROM student_assignments sa
JOIN grade_levels gl ON gl.id = sa.grade_id
JOIN groups g ON g.id = sa.group_id
WHERE sa.student_id = @studentId
  AND sa.is_active = true
ORDER BY COALESCE(sa.start_date, sa.created_at) DESC
LIMIT 1;  -- FirstOrDefaultAsync
```

**Riesgo**: El encabezado del reporte mostrará el grado/grupo de la matrícula más reciente. No representa necesariamente la matrícula "principal" del estudiante nocturno.

---

### D.3 Query de grupos por nivel hardcodeada (AprobadosReprobadosService)

**Archivo**: `Services/Implementations/AprobadosReprobadosService.cs` línea ~293

No hay query SQL aquí — el problema es que los valores que deberían venir de la base de datos están escritos directamente en el código fuente como constantes de cadena.

**Qué debería ser**:
```sql
-- Query correcta que NO existe en el código
SELECT DISTINCT gl.name as grade_name
FROM grade_levels gl
JOIN groups g ON g.grade_level_id = gl.id
WHERE gl.school_id = @schoolId
ORDER BY gl.name;
```

---

### D.4 `ResolveStudentSubjectAssignmentIdAsync` sin contexto de grupo/grado

**Archivo**: `Services/Implementations/StudentActivityScoreService.cs` línea ~49

```sql
-- Cuando activityGroupId y activityGradeLevelId son NULL
SELECT sa.id
FROM subject_assignments sa
WHERE sa.subject_id = @subjectId
LIMIT 1;  -- FirstOrDefaultAsync sin filtros
```

**Riesgo**: Retorna cualquier `SubjectAssignment` de esa materia en la escuela, sin importar el grupo o grado. Las notas podrían vincularse al contexto equivocado.

---

### D.5 Query masiva de grados/grupos hardcoded en PrepararDatos

**Archivo**: `Services/Implementations/AprobadosReprobadosService.cs` línea ~649–666

```sql
-- Equivalente de una iteración del foreach
UPDATE groups
SET grade = '7°'
WHERE school_id = @schoolId
  AND name IN ('A', 'A1', 'A2')
  AND (grade IS NULL OR grade = '');
```

**Riesgo**: Esta operación modifica datos en producción basándose en nombres fijos. Grupos nocturnos no son incluidos. La operación que actualiza `activities.trimester = '3T'` para toda la escuela también puede afectar trimestres de jornada diurna.

---

## SECCIÓN E — VISTAS PROBLEMÁTICAS (archivo:línea)

### E.1 `Views/StudentIdCard/Generate.cshtml` — Texto hardcodeado

**Archivo**: `Views/StudentIdCard/Generate.cshtml`  
**Línea**: ~401  
**Código**:
```html
<span>Nocturna</span>
```

**Problema**: El texto "Nocturna" está hardcodeado en la vista del carnet. Aunque en este contexto es intencional (la instrucción era mostrar "Nocturna" en lugar del grado para nocturna), el enfoque es frágil. Si la lógica de selección de carnet cambia, este texto no se actualizará automáticamente.

**Severidad actual**: BAJA (funcional, pero inflexible).

---

### E.2 `Views/ScheduleConfiguration/Index.cshtml` — Correcta

**Archivo**: `Views/ScheduleConfiguration/Index.cshtml`

Esta vista expone correctamente los campos nocturnos: `NightStartTime`, `NightBlockDurationMinutes`, `NightBlockCount`. No hay hardcoding problemático. Se documenta como referencia positiva.

---

### E.3 Vistas de Aprobados/Reprobados (selector de nivel educativo)

**Archivo**: Buscar en `Views/AprobadosReprobados/` o equivalente.

El selector de "Nivel Educativo" en la UI de generación de reportes estará limitado a "Premedia" y "Media" porque `ObtenerNivelesEducativosAsync` (línea ~327 en `AprobadosReprobadosService.cs`) retorna esa lista hardcodeada. No es un problema de la vista en sí, sino del servicio que la alimenta. La vista, sin embargo, nunca mostrará una opción "Nocturna" al usuario.

---

## SECCIÓN F — MAPA DE RIESGOS POR ARCHIVO

| Archivo | Línea(s) | Tipo de Riesgo | Nivel |
|---------|----------|----------------|-------|
| `Services/Implementations/ScheduleService.cs` | ~215–224 | `FirstOrDefaultAsync` sin multi-grupo | ALTO |
| `Services/Implementations/StudentAssignmentService.cs` | ~233–248 | Inactiva TODAS las matrículas (`replaceExistingActive=true`) | ALTO |
| `Services/Implementations/AprobadosReprobadosService.cs` | ~293–300 | Grados hardcodeados — nocturna excluida | ALTO |
| `Services/Implementations/AprobadosReprobadosService.cs` | ~327–329 | Niveles educativos hardcodeados | ALTO |
| `Services/Implementations/AprobadosReprobadosService.cs` | ~649–656 | Grupos por grado hardcodeados | ALTO |
| `Controllers/StudentAssignmentController.cs` | ~56–80 | `additive=false` por defecto destruye multi-matrícula | ALTO |
| `Services/Implementations/StudentReportService.cs` | ~145–156, ~429–440 | `FirstOrDefaultAsync` — encabezado de reporte incompleto | MEDIO |
| `Services/Implementations/StudentActivityScoreService.cs` | ~49–67 | Resolución de SSA sin filtros de grupo/grado | MEDIO |
| `Services/Implementations/StudentAssignmentService.cs` | ~416–419 | BulkAssign usa campo `Grade` legacy para buscar grupos | MEDIO |
| `Models/Group.cs` | — | Dual-field Shift (`shift` texto + `shift_id` FK) | MEDIO |
| `Models/StudentSubjectAssignment.cs` | — | `StudentAssignmentId` nullable — ruptura de trazabilidad | MEDIO |
| `Views/StudentIdCard/Generate.cshtml` | ~401 | Texto "Nocturna" hardcodeado | BAJO |

---

## SECCIÓN G — INVENTARIO DE MODELOS (estructura nocturna)

| Modelo C# | Archivo | ShiftId | EnrollmentType | IsActive | Comentario |
|-----------|---------|---------|----------------|----------|------------|
| `StudentAssignment` | `Models/StudentAssignment.cs` | SI (nullable) | SI | SI | Core de multi-matrícula |
| `StudentSubjectAssignment` | `Models/StudentSubjectAssignment.cs` | SI (nullable) | SI | SI | StudentAssignmentId nullable — riesgo |
| `StudentActivityScore` | `Models/StudentActivityScore.cs` | NO | NO | NO | StudentAssignmentId NOT NULL — correcto |
| `Attendance` | `Models/Attendance.cs` | SI | NO | NO | UNIQUE correcto con ShiftId |
| `Group` | `Models/Group.cs` | SI (nullable) | NO | NO | Dual campo shift — riesgo |
| `SchoolScheduleConfiguration` | `Models/SchoolScheduleConfiguration.cs` | NO | NO | NO | Tiene NightStartTime, NightBlockDuration, NightBlockCount |
| `Student` (legacy) | `Models/Student.cs` | NO | NO | NO | Grade/GroupName como strings — obsoleto |

---

## SECCIÓN H — INVENTARIO DE SERVICIOS (relevancia nocturna)

| Servicio | Archivo | Estado nocturna | Método crítico |
|---------|---------|-----------------|----------------|
| `StudentAssignmentService` | `Services/Implementations/StudentAssignmentService.cs` | PARCIAL — AssignAsync destruye, AddEnrollmentAsync correcto | `AssignAsync`, `AddEnrollmentAsync`, `BulkAssignFromFileAsync` |
| `ScheduleService` | `Services/Implementations/ScheduleService.cs` | ROTO para multi-matrícula estudiantil | `GetByStudentUserAsync` |
| `AprobadosReprobadosService` | `Services/Implementations/AprobadosReprobadosService.cs` | ROTO — hardcoded grados/grupos/niveles | `ObtenerGradosPorNivel`, `ObtenerNivelesEducativosAsync`, `PrepararDatosParaReporteAsync` |
| `AttendanceService` | `Services/Implementations/AttendanceService.cs` | CORRECTO — acepta ShiftId en filtros | `SaveAttendancesAsync`, `GetHistorialAsync`, `GetEstadisticasAsync` |
| `StudentActivityScoreService` | `Services/Implementations/StudentActivityScoreService.cs` | MAYORMENTE CORRECTO — riesgo en resolución SSA sin contexto | `ResolveStudentAssignmentIdAsync`, `ResolveStudentSubjectAssignmentIdAsync` |
| `StudentReportService` | `Services/Implementations/StudentReportService.cs` | PARCIAL — encabezado de reporte arbitrario para multi-matrícula | `GetReportAsync` (~línea 144), método PDF (~línea 429) |
| `StudentIdCardService` | `Services/Implementations/StudentIdCardService.cs` | CORRECTO — prioriza nocturno explícitamente | Orden por EnrollmentType "nocturno" y ShiftName "noche" |
| `StudentService` | `Services/Implementations/StudentService.cs` | CORRECTO por grupo/grado | `GetByGroupAndGradeAsync` |

---

## SECCIÓN I — EVIDENCIA DE BASE DE DATOS

### I.1 Confirmación de ausencia de UNIQUE en student_assignments

Query ejecutada contra `schoolmanager_daqf`:
```sql
SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename = 'student_assignments'
ORDER BY indexname;
```

Resultado: Ningún índice UNIQUE sobre columnas de negocio. Solo PK y índices de performance. Confirmado: multi-matrícula está habilitada a nivel de base de datos.

### I.2 Confirmación de UNIQUE parcial en student_subject_assignments

Query ejecutada:
```sql
SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename = 'student_subject_assignments'
ORDER BY indexname;
```

Resultado incluye:
```
ix_student_subject_assignments_active_unique | CREATE UNIQUE INDEX ... ON student_subject_assignments
  USING btree (student_id, subject_assignment_id, academic_year_id)
  WHERE (is_active = true)
```

### I.3 Confirmación de UNIQUE con shift_id en attendance

Query ejecutada:
```sql
SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename = 'attendance'
  AND indexdef LIKE '%unique%';
```

Resultado:
```
ix_attendance_student_date_group_grade_shift | CREATE UNIQUE INDEX ... ON attendance
  USING btree (student_id, date, group_id, grade_id, shift_id)
```

### I.4 Rowcounts de tablas principales

| Tabla | Filas (aprox.) |
|-------|----------------|
| `users` | 1,440+ |
| `student_assignments` | 1,000+ |
| `student_subject_assignments` | 5,000+ |
| `student_activity_scores` | 10,000+ |
| `attendance` | 50,000+ |
| `shifts` | 3 |
| `groups` | 50+ |
| `grade_levels` | 15+ |
| `subject_assignments` | 200+ |

---

## SECCIÓN J — REFERENCIAS CRUZADAS (análisis ↔ código)

| Hallazgo en análisis | Evidencia en código | Archivo:Línea |
|---------------------|---------------------|---------------|
| "ScheduleService toma solo primera matrícula" | `.FirstOrDefaultAsync()` sin multi-resultado | `ScheduleService.cs:224` |
| "AssignAsync destruye multi-matrícula" | `replaceExistingActive = true` por defecto | `StudentAssignmentService.cs:227` |
| "AprobadosReprobados hardcodeado" | switch expression con grados fijos | `AprobadosReprobadosService.cs:293-300` |
| "Niveles educativos hardcodeados" | `Task.FromResult(new List<string>{"Premedia","Media"})` | `AprobadosReprobadosService.cs:327-329` |
| "Groups hardcodeados por grado" | `Dictionary<string, string[]>` con valores literales | `AprobadosReprobadosService.cs:649-656` |
| "StudentSubjectAssignment.StudentAssignmentId nullable" | `public Guid? StudentAssignmentId { get; set; }` | `StudentSubjectAssignment.cs:13` |
| "ReportService FirstOrDefault sin contexto" | `.FirstOrDefaultAsync()` sin filtro de grupo/jornada | `StudentReportService.cs:156, 440` |
| "UpdateGroupAndGrade additive=false por defecto" | `bool additive = false` en firma del endpoint | `StudentAssignmentController.cs:56` |
| "UNIQUE attendance incluye shift_id" | `.HasIndex(..., "ix_attendance_student_date_group_grade_shift").IsUnique()` | `SchoolDbContext.cs:339-340` |
| "student_assignments sin UNIQUE" | Ausencia de `.IsUnique()` en configuración de la tabla | `SchoolDbContext.cs:956-1025` |

---

*Fin del Anexo Técnico*
