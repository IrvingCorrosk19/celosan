# ANÁLISIS DE SOPORTE PARA JORNADA NOCTURNA — EDUPLANER SCHOOLMANAGER

---

## PORTADA

| Campo        | Valor                                      |
|--------------|--------------------------------------------|
| **Título**   | Auditoría Técnica: Soporte Jornada Nocturna |
| **Fecha**    | 2026-04-18                                 |
| **Autor**    | Consultor Técnico (auditado por Claude Sonnet 4.6) |
| **Versión**  | 1.0 — Inicial                              |
| **Base de datos auditada** | `schoolmanager_daqf` @ Render PostgreSQL 18 |
| **Rama auditada** | `main` (commit `2520c69`)           |

---

## RESUMEN EJECUTIVO

SchoolManager ha recorrido un camino significativo hacia el soporte de jornada nocturna. La infraestructura de datos es sólida: existe la tabla `shifts`, `student_assignments` admite múltiples filas activas por estudiante sin constraint UNIQUE que lo impida, `attendance` tiene un índice UNIQUE particionado por shift, y la configuración de horarios soporta tres jornadas (Mañana, Tarde, Noche). Los modelos de dominio son correctos en su estructura.

Sin embargo, la aplicación **no está lista para operación nocturna en producción sin riesgo**. Existen al menos tres categorías críticas de problemas:

1. **Asignación de calificaciones ambigua cuando un estudiante cursa materias iguales en grupos distintos.** El constraint `uq_scores_assignment_activity` resuelve la duplicación por `StudentAssignmentId`, pero la lógica de resolución de notas usa `FirstOrDefault` sobre matrículas ordenadas por fecha, lo que puede asignar una nota al contexto incorrecto si el estudiante tiene dos matrículas activas del mismo grado+grupo nocturno.

2. **ReportService de Aprobados/Reprobados tiene grados y grupos hardcodeados** (`"7°"→["A","A1","A2"]`, etc.) que excluye completamente los grupos nocturnos especiales (ESP7, ESP8, ESP9, S, S1, S2, TM1, etc. que se observan en la BD real).

3. **El servicio de horarios para estudiantes (`GetByStudentUserAsync`) toma solo la primera matrícula activa** (`FirstOrDefault`), por lo que un estudiante nocturno con dos grupos distintos solo verá el horario de uno de ellos.

La arquitectura **es extensible**. La mayor parte del diseño es favorable: `ShiftId` existe en todas las tablas críticas, `EnrollmentType` diferencia "Nocturno" de "Regular", y hay endpoints dedicados (`AddEnrollment`, `BulkSaveSubjectEnrollments`) que ya contemplan múltiples matrículas. El problema no es de arquitectura sino de parches incompletos y supuestos diurnos residuales.

**Veredicto: Soporte parcial. La arquitectura aguanta nocturna; la lógica de negocio tiene brechas concretas que producirán datos incorrectos en módulos de notas, reportes y horarios de estudiante.**

---

## ALCANCE DEL ANÁLISIS

Se auditaron los siguientes artefactos:

- **Modelos de dominio** (52 entidades en `Models/`)
- **DbContext** con configuración ORM completa
- **Servicios** en `Services/Implementations/` (67 archivos)
- **Controladores** en `Controllers/` (47 archivos)
- **Vistas** en `Views/` (archivos `.cshtml`)
- **Base de datos PostgreSQL** en producción/desarrollo (host Render)
- **Migraciones** (57 migraciones registradas en `__EFMigrationsHistory`)
- **Scripts SQL** en raíz del proyecto (varios archivos `.sql`)

No se auditaron: código de integraciones externas (Cloudinary, Resend), módulo de pagos del Club de Padres (fuera del alcance académico).

---

## METODOLOGÍA

1. Lectura directa de todos los modelos relevantes en C#
2. Conexión psql a la BD de desarrollo/producción y ejecución de queries estructurales
3. Lectura de servicios y controladores críticos
4. Búsqueda semántica con Grep para patrones peligrosos
5. Revisión de vistas Razor críticas
6. Síntesis y clasificación por riesgo

---

## 1. MODELO DE NEGOCIO ACTUAL

### 1.1 Modelo Diurno (implícito)

El sistema fue diseñado originalmente asumiendo:
- 1 estudiante = 1 GradeLevel activo = 1 Group activo = 1 Jornada
- Un `StudentAssignment` activo por estudiante
- Horarios secuenciales (Mañana → Tarde, configurados globalmente)
- Grados estándar: `7°`, `8°`, `9°`, `10°`, `11°`, `12°`

### 1.2 Modelo Nocturno (requerido)

Un estudiante nocturno puede tener:
- Múltiples `StudentAssignment` activos simultáneamente
- Cada uno con diferente GradeLevel y/o Group
- Materias repetidas o equivalentes en distintas combinaciones nivel/grupo/periodo
- Matrícula de tipo `"Nocturno"` vs `"Regular"`

### 1.3 Gap entre ambos modelos

El sistema ha incorporado aproximadamente 60% del soporte nocturno a nivel de datos. El 40% restante está en la lógica de negocio y reportes.

---

## 2. BASE DE DATOS — ESTRUCTURA Y HALLAZGOS

### 2.1 Tablas existentes (52 tablas en el esquema `public`)

Las tablas relevantes para nocturna son:

| Tabla | Filas | Estado nocturna |
|-------|-------|-----------------|
| `shifts` | 3 | Mañana, Tarde, **Noche** — OK |
| `groups` | 30 | Incluye grupos ESP7, ESP8, ESP9, S, S1, S2, TM1 — parcialmente OK |
| `student_assignments` | 0 | Vacío en prod (datos de prueba) |
| `subject_assignments` | 1,142 | El mayor volumen de datos reales |
| `attendance` | 0 | Sin datos |
| `student_activity_scores` | 0 | Sin datos |
| `prematriculations` | 0 | Sin datos |

### 2.2 Estructura de `student_assignments` — hallazgo crítico

```sql
student_assignments:
  id, student_id, grade_id, group_id, created_at, is_active,
  end_date, academic_year_id, shift_id, enrollment_type, start_date
```

**Índices relevantes:**
- `student_assignments_pkey` (id) — PK
- `IX_student_assignments_student_id` (student_id) — no UNIQUE
- `ix_student_assignments_student_active` (student_id, is_active) — no UNIQUE
- `ix_student_assignments_student_academic_year` (student_id, academic_year_id) — no UNIQUE

**No existe ningún constraint UNIQUE sobre (student_id, grade_id, group_id) o (student_id, grade_id, group_id, shift_id).** Esto es correcto para nocturna: permite múltiples matrículas activas. Pero también significa que no hay protección automática contra duplicados accidentales en el mismo contexto. La deduplicación es completamente responsabilidad de la capa de aplicación.

### 2.3 Estructura de `student_subject_assignments` — hallazgo crítico

```sql
UNIQUE INDEX ix_student_subject_assignments_active_unique
ON student_subject_assignments (student_id, subject_assignment_id, academic_year_id)
WHERE is_active = true
```

Este índice **impide que un mismo estudiante tenga la misma materia ofertada activa dos veces en el mismo año académico**. Para nocturna, donde un estudiante puede cursar la misma materia en dos grupos distintos, esto crea un problema: si `SubjectAssignment` A y B ofrecen la misma materia pero en grupos distintos, son registros distintos y ambos pueden inscribirse. Sin embargo, si la misma materia pertenece al mismo `SubjectAssignment`, no se puede duplicar. La lógica es correcta cuando los grupos tienen sus propias `SubjectAssignment`.

### 2.4 Estructura de `attendance` — hallazgo positivo

```sql
UNIQUE INDEX ix_attendance_student_date_group_grade_shift
ON attendance (student_id, date, group_id, grade_id, shift_id)
```

Este índice **sí soporta nocturna correctamente**: incluye `shift_id` como parte de la clave de unicidad, permitiendo que un estudiante tenga asistencia en dos grupos/jornadas distintos el mismo día. Diseño correcto.

### 2.5 Estructura de `student_activity_scores` — hallazgo crítico

```sql
UNIQUE INDEX uq_scores_assignment_activity
ON student_activity_scores (student_assignment_id, activity_id)

UNIQUE INDEX uq_scores_subject_enrollment_activity
ON student_activity_scores (student_subject_assignment_id, activity_id)
```

**El constraint está amarrado a `student_assignment_id` (no a `student_id`).** Esto es correcto en teoría: si el estudiante tiene dos `StudentAssignment` distintos (dos grupos), puede tener dos notas para actividades distintas. El problema surge cuando la actividad es la misma materia en ambos grupos — entonces habría dos `StudentAssignment` y potencialmente dos notas para la misma actividad si el sistema no controla el contexto.

### 2.6 Grupos nocturnos detectados en BD real

Los grupos con jornada Noche en la BD son: `A`, `ESP7`, `ESP8`, `ESP9`. El grupo `P` y `S` no tienen jornada asignada. Esto revela inconsistencia en los datos maestros.

---

## 3. ENTIDADES Y MODELOS DE DOMINIO

### 3.1 `StudentAssignment` (Models/StudentAssignment.cs)

```csharp
public Guid? ShiftId { get; set; }      // FK a shifts
public bool IsActive { get; set; }      // Soporte para múltiples activos
public string EnrollmentType { get; set; } = "Regular"; // "Nocturno" para nocturna
public DateTime? StartDate { get; set; }
public DateTime? EndDate { get; set; }
public Guid? AcademicYearId { get; set; }
```

**Valoración: Correcto para nocturna.** No tiene restricciones que impidan múltiples activos.

### 3.2 `StudentSubjectAssignment` (Models/StudentSubjectAssignment.cs)

```csharp
public Guid? StudentAssignmentId { get; set; }  // Opcional (herencia histórica)
public Guid? ShiftId { get; set; }              // Trazabilidad de jornada
public string EnrollmentType { get; set; }      // Heredado
```

**Valoración: Correcto.** La opcionalidad de `StudentAssignmentId` es un riesgo de coherencia (si es null, no hay trazabilidad al contexto de matrícula).

### 3.3 `Group` (Models/Group.cs)

```csharp
public string? Shift { get; set; }    // Texto libre — DUPLICIDAD con ShiftId
public Guid? ShiftId { get; set; }   // FK a shifts (moderno)
```

**Riesgo**: Dos campos para el mismo propósito. El campo `Shift` (texto libre) existe por "compatibilidad". Código que lee `group.Shift` en lugar de navegar por `ShiftNavigation` puede leer datos desactualizados.

### 3.4 `Attendance` (Models/Attendance.cs)

Correctamente incluye `ShiftId`. El modelo soporta nocturna.

### 3.5 `StudentActivityScore` (Models/StudentActivityScore.cs)

```csharp
public Guid StudentAssignmentId { get; set; }    // NOT NULL
public Guid? StudentSubjectAssignmentId { get; set; }  // Nullable
```

**Valoración**: El `StudentAssignmentId` es NOT NULL, por lo que siempre hay contexto de matrícula. Correcto.

### 3.6 `Student` (Models/Student.cs) — hallazgo negativo

```csharp
public string? Grade { get; set; }      // Cadena libre — legado
public string? GroupName { get; set; }  // Cadena libre — legado
```

Esta tabla (`students`) es una tabla **legada o espejo** separada de `users`. La tabla real de estudiantes es `users` con `Role = "estudiante"`. Los campos `Grade` y `GroupName` son strings libres sin FK, que asumen 1 grado y 1 grupo. Esta tabla aparentemente no se usa en los flujos principales (el sistema consulta `users + student_assignments`), pero su existencia puede causar confusión.

---

## 4. MATRÍCULA Y ASIGNACIÓN ACADÉMICA

### 4.1 Flujo de matrícula actual

Existen tres vías de matrícula:
1. **Manual por secretaria**: `StudentAssignmentController.UpdateGroupAndGrade` (modo replace o additive)
2. **Carga masiva Excel**: `StudentAssignmentController.SaveAssignments` (via `SaveAssignments`)
3. **Carga masiva de materias**: `StudentAssignmentController.BulkSaveSubjectEnrollments`

### 4.2 Soporte de múltiples matrículas — PARCIALMENTE OK

**Lo que sí funciona:**
- `UpdateGroupAndGrade` con `additive=true` agrega una matrícula sin quitar las demás
- `AddEnrollment` está específicamente diseñado para nocturna
- La deduplicación es por `(student_id, grade_id, group_id, shift_id)` usando `ExistsWithShiftAsync`

**Lo que NO funciona bien:**
- `UpdateGroupAndGrade` con `additive=false` (modo por defecto) **llama `RemoveAssignmentsAsync` que inactiva TODAS las matrículas activas del estudiante**. Si un estudiante nocturno tiene dos matrículas y la secretaria usa la UI sin marcar "additive", pierde ambas y solo queda la nueva.
- `AssignAsync` con `replaceExistingActive=true` (también por defecto) hace lo mismo: inactiva todo antes de agregar.
- La UI de `StudentAssignment/Index.cshtml` no hace obvio cuál modo está activo.

### 4.3 Validación de duplicados

El método `ExistsWithShiftAsync` filtra por `(student_id, grade_id, group_id, shift_id)`, lo que es correcto. Pero en `BulkAssignFromFileAsync` la validación es solo por `(student_id, grade_id, group_id)` sin shift, lo que puede crear duplicados en distintas jornadas para el mismo grado/grupo.

### 4.4 Sincronización StudentSubjectAssignment

`SyncStudentSubjectAssignmentsAsync` crea automáticamente los `StudentSubjectAssignment` cuando se crea una `StudentAssignment`. Esto es correcto. Sin embargo, si el estudiante ya tiene un `StudentSubjectAssignment` activo para la misma `SubjectAssignment` en el mismo año académico (constraint `ix_student_subject_assignments_active_unique`), se salta la creación. Esto podría fallar silenciosamente si el mismo estudiante se matricula en el mismo grupo desde dos rutas distintas.

---

## 5. HORARIOS

### 5.1 Configuración de jornadas

`SchoolScheduleConfiguration` soporta tres jornadas configurables:
- `MorningStartTime` / `MorningBlockDurationMinutes` / `MorningBlockCount`
- `AfternoonStartTime` / `AfternoonBlockDurationMinutes` / `AfternoonBlockCount`
- `NightStartTime` / `NightBlockDurationMinutes` / `NightBlockCount`

Los bloques nocturnos se crean como `TimeSlot` con `ShiftId` de "Noche". **Este diseño es correcto y soporta nocturna a nivel de configuración.**

### 5.2 Detección de conflictos — PROBLEMA para nocturna

El `ScheduleService.CreateEntryAsync` detecta conflictos por:
- A) Mismo docente, mismo año, mismo día, mismo bloque
- B) Mismo grupo, mismo año, mismo día, mismo bloque

El conflicto B busca por `SubjectAssignment.GroupId == groupId`. Para un estudiante nocturno en dos grupos, esto es correcto (los conflictos son por grupo, no por estudiante). **Sin embargo**, si la misma asignatura tiene TimeSlots tanto de mañana como de noche (via el mismo GroupId), podría haber confusión.

### 5.3 Horario del estudiante — RIESGO ALTO

```csharp
// ScheduleService.cs línea 215-225
var assignment = await _context.StudentAssignments
    .AsNoTracking()
    .Include(sa => sa.Group)
    .Where(sa =>
        sa.StudentId == studentUserId &&
        sa.IsActive &&
        (sa.AcademicYearId == academicYearId || sa.AcademicYearId == null))
    .OrderByDescending(sa => sa.AcademicYearId != null)
    .ThenByDescending(sa => sa.CreatedAt)
    .FirstOrDefaultAsync();  // SOLO TOMA LA PRIMERA
```

**Un estudiante nocturno con dos matrículas activas solo verá el horario del grupo de su matrícula más reciente.** El horario del otro grupo es invisible para él.

---

## 6. ASISTENCIA

### 6.1 Registro de asistencia — CORRECTO

`AttendanceService.SaveAttendancesAsync` resuelve el `StudentAssignment` correcto filtrando también por `ShiftId` cuando está disponible. El constraint UNIQUE en BD incluye `shift_id`. La lógica es correcta para nocturna.

### 6.2 Historial y estadísticas — CORRECTO

Los métodos `GetHistorialAsync` y `GetEstadisticasAsync` admiten `shiftId` como filtro opcional. Si se filtra correctamente, no hay duplicados.

### 6.3 Riesgo de lista de asistencia — RIESGO MEDIO

El método `GetByGroupAndGradeAsync` en `StudentService` hace un JOIN directo `StudentAssignment → users` filtrando por `GroupId` y `GradeId`. Si un estudiante tiene dos matrículas (dos grupos distintos del mismo grado), aparecerá solo una vez. **Si un estudiante está en el mismo grupo dos veces por error (datos incorrectos)**, aparecería duplicado. La deduplicación no existe en este método.

---

## 7. CALIFICACIONES

### 7.1 Libro de calificaciones (`GetGradeBookAsync`) — CORRECTO

El servicio obtiene estudiantes vía `StudentSubjectAssignments` activos, que ya están particionados por `SubjectAssignment` (que incluye GroupId). No hay riesgo de mezcla entre grupos si la data está correctamente segmentada.

### 7.2 Guardado de notas (`SaveBulkFromNotasAsync`) — RIESGO MEDIO

```csharp
private async Task<Guid> ResolveStudentAssignmentIdForGroupAsync(Guid studentId, Guid groupId, Guid gradeLevelId)
{
    var id = await _context.StudentAssignments
        .Where(sa => sa.StudentId == studentId && sa.IsActive && sa.GroupId == groupId && sa.GradeId == gradeLevelId)
        .OrderByDescending(sa => sa.CreatedAt ?? DateTime.MinValue)
        .Select(sa => (Guid?)sa.Id)
        .FirstOrDefaultAsync();
    ...
}
```

Si un estudiante tiene **dos matrículas activas en el mismo grupo y grado** (lo cual no debería ocurrir pero tampoco hay constraint que lo impida), la nota se asigna a la más reciente. Esto podría crear situaciones donde notas antiguas quedan huérfanas.

### 7.3 Reporte de Aprobados/Reprobados — RIESGO ALTO

```csharp
// AprobadosReprobadosService.cs línea 293-300
private List<string> ObtenerGradosPorNivel(string nivelEducativo)
{
    return nivelEducativo.ToLower() switch
    {
        "premedia" => new List<string> { "7°", "8°", "9°" },
        "media" => new List<string> { "10°", "11°", "12°" },
        _ => new List<string>()
    };
}
```

**Esta función hardcodea los grados exactos y devuelve lista vacía para cualquier otro nivel.** Grupos nocturnos con grados diferentes a este formato (ej. `"N/A"`, grados con nombres personalizados, niveles específicos de nocturna) quedarán excluidos del reporte.

Además, el método `PrepararDatosParaReporte` hardcodea el mapeo grado→grupos:
```csharp
["7°"] = new[] { "A", "A1", "A2" },
["8°"] = new[] { "B", "C", "C1", "C2" },
// ... etc.
```

Los grupos nocturnos reales (`ESP7`, `ESP8`, `ESP9`, `S1`, `S2`, `TM1`) están completamente ausentes de este mapeo.

---

## 8. REPORTES, PORTALES Y VISTAS

### 8.1 Vista `StudentAssignment/Index.cshtml` — CORRECTO

Muestra múltiples matrículas por estudiante correctamente: el controlador itera `GradeGroupPairs` que es una lista. El UI puede mostrar "Grado A - Grupo ESP7 | Jornada: Noche | Matrícula: Nocturno" junto a otra matrícula activa.

### 8.2 Reporte del estudiante (`StudentReportService`) — RIESGO MEDIO

El servicio carga notas por `StudentId` directamente sin particionar por matrícula. Si el estudiante tiene materias en dos grupos distintos, las notas de ambos grupos se mezclan en un único reporte. El reporte podría mostrar materias repetidas (con distintas notas) o promedios incorrectos.

### 8.3 Vista de asistencia general — RIESGO BAJO

La vista `Attendance/Index.cshtml` muestra todos los registros con Grado y Grupo, lo que es suficientemente informativo. No hay problema funcional.

### 8.4 Configuración de horarios — CORRECTO

La vista `ScheduleConfiguration/Index.cshtml` ya expone campos de jornada nocturna (`NightStartTime`, `NightBlockDurationMinutes`, `NightBlockCount`) con etiquetas en español. La vista es completa.

---

## 9. CARNETS E IDENTIDAD ACADÉMICA

### 9.1 Lógica de selección de matrícula activa — PARCIALMENTE CORRECTO

```csharp
// StudentIdCardService.cs línea 74-91
Grade = x.StudentAssignments.Where(a => a.IsActive)
    .OrderByDescending(a => a.EnrollmentType != null && a.EnrollmentType.ToLower() == "nocturno")
    .ThenByDescending(a => a.Shift != null && a.Shift.Name != null && a.Shift.Name.ToLower().Contains("noche"))
    .ThenByDescending(a => a.CreatedAt ?? DateTime.MinValue)
    .Select(a => a.Grade.Name)
    .FirstOrDefault()
```

El carnet **prioriza la matrícula de tipo "Nocturno"** y luego la de jornada "noche". Esta es una implementación razonable y deliberada: muestra el grado de la matrícula nocturna en el carnet.

**El riesgo**: si el estudiante tiene dos matrículas nocturnas en dos grados distintos, el carnet solo muestra el grado de la más reciente entre ellas. El carnet no lista múltiples grados.

### 9.2 Texto fijo "Nocturna" en carnet

En `Views/StudentIdCard/Generate.cshtml` (línea 401) existe el texto hardcodeado `Nocturna` que se muestra en lugar del grado. Esto es un parche deliberado para el contexto nocturno actual.

---

## 10. REGLAS IMPLÍCITAS Y HARD-CODED

| Hallazgo | Archivo | Línea | Riesgo |
|----------|---------|-------|--------|
| Grados hardcoded `"7°","8°","9°","10°","11°","12°"` | `AprobadosReprobadosService.cs` | 297-298 | ALTO |
| Grupos por grado hardcoded `"A","A1","A2"...` | `AprobadosReprobadosService.cs` | 651-656 | ALTO |
| Niveles disponibles devuelve solo `["Premedia","Media"]` | `AprobadosReprobadosService.cs` | 329 | ALTO |
| `ScheduleService.GetByStudentUserAsync` usa `FirstOrDefault` en StudentAssignment | `ScheduleService.cs` | 224 | ALTO |
| `AssignAsync` con `replaceExistingActive=true` inactiva todas las matrículas | `StudentAssignmentService.cs` | 233-248 | ALTO |
| `Shift` (texto libre) duplicado con `ShiftId` FK en `groups` | `Group.cs` | 28-29 | MEDIO |
| `Student` tabla legada con `Grade` y `GroupName` como strings | `Student.cs` | 16-17 | BAJO |
| `BulkAssignFromFileAsync` no filtra por ShiftId en check de duplicados | `StudentAssignmentService.cs` | 427-431 | MEDIO |
| Jornada por defecto `"Noche"` hardcoded en carga masiva | `StudentAssignmentController.cs` | 887-890 | BAJO |
| `GetByGroupAndGradeAsync` sin `.Distinct()` explícito (puede duplicar si hay 2 matrículas mismo grupo) | `StudentService.cs` | 60-77 | MEDIO |

---

## 11. MATRIZ DE RIESGOS

### RIESGO ALTO — Rompería funcionalidad crítica o corrompería datos

| ID | Módulo | Descripción del riesgo | Archivo : Método |
|----|--------|------------------------|------------------|
| R01 | Horarios | Estudiante con 2 matrículas activas ve solo el horario de 1 grupo | `ScheduleService.cs:GetByStudentUserAsync` |
| R02 | Reportes | Reporte Aprobados/Reprobados excluye todos los grados y grupos nocturnos | `AprobadosReprobadosService.cs:ObtenerGradosPorNivel` |
| R03 | Matrícula | UI por defecto (`additive=false`) borra todas las matrículas existentes al reasignar | `StudentAssignmentService.cs:AssignAsync` |
| R04 | Matrícula | `AssignAsync` con `replaceExistingActive=true` destruye matrículas nocturnas en cascada | `StudentAssignmentService.cs:AssignAsync` |
| R05 | Calificaciones | Reporte individual del estudiante mezcla notas de dos grupos distintos sin partición | `StudentReportService.cs:GetReportByStudentIdAsync` |

### RIESGO MEDIO — Genera inconsistencia o UX degradada

| ID | Módulo | Descripción | Archivo : Método |
|----|--------|-------------|------------------|
| M01 | Asistencia | Lista de asistencia puede duplicar estudiante si tiene 2 matrículas en mismo grupo | `StudentService.cs:GetByGroupAndGradeAsync` |
| M02 | Matrícula | Carga masiva Excel no incluye ShiftId en check de duplicados | `StudentAssignmentService.cs:BulkAssignFromFileAsync` |
| M03 | Carnets | Carnet muestra un solo grado aunque el estudiante tenga 2 matrículas nocturnas distintas | `StudentIdCardService.cs:GetCurrentCardAsync` |
| M04 | Datos | Campo `Shift` texto libre en `groups` puede diferir de `ShiftId` | `Group.cs` |
| M05 | Datos | `StudentSubjectAssignmentId` nullable en `StudentActivityScore` crea inconsistencia de trazabilidad | `StudentActivityScoreService.cs` |

### RIESGO BAJO — Cosmético o fácilmente ajustable

| ID | Módulo | Descripción | Archivo |
|----|--------|-------------|---------|
| B01 | UI | Ninguna vista muestra explícitamente "usted tiene N matrículas activas" | Múltiples vistas |
| B02 | Datos | Tabla `students` legada con strings `Grade`/`GroupName` no sincronizados | `Student.cs` |
| B03 | Config | Jornada por defecto hardcodeada como `"Noche"` en carga masiva | `StudentAssignmentController.cs` |
| B04 | Grupos | Grupos `P` y `S` sin jornada asignada en BD | BD real |

---

## 12. DIAGNÓSTICO FINAL

### ¿La aplicación YA está lista para nocturna?

**No completamente. Está al 60-65%.**

### ¿Qué sí soporta hoy?

- Registro y almacenamiento de múltiples matrículas activas por estudiante con jornada
- Asistencia con partición por shift (UNIQUE index correcto)
- Carnet que prioriza la matrícula nocturna
- Carga masiva Excel de estudiantes con asignación a jornada Noche
- Carga masiva de materias por estudiante nocturno (BulkSaveSubjectEnrollments)
- Configuración de horarios para jornada nocturna
- Filtro por shift en historial de asistencia y estadísticas
- Portal del docente (gradebook) correctamente particionado por SubjectAssignment
- La estructura de BD es correcta y no impide múltiples matrículas

### ¿Qué se rompería inmediatamente con un estudiante nocturno en dos grupos?

1. El horario del estudiante solo mostrará un grupo
2. El reporte de Aprobados/Reprobados no lo incluirá (si su grado no es `7°`-`12°` exacto)
3. El reporte individual del estudiante mezclará notas de ambos grupos
4. Si la secretaria usa la UI de reasignación (modo reemplazar), perderá ambas matrículas y quedará solo con una

### ¿La arquitectura es extensible?

**Sí, en su mayoría.** El modelo de datos con `ShiftId` en todas las tablas críticas, `EnrollmentType` y la ausencia de constraints UNIQUE excluyentes en `student_assignments` es la base correcta. Los problemas son de lógica de negocio y supuestos residuales del modelo diurno, no de arquitectura de datos.

---

## 13. RECOMENDACIÓN ESTRATÉGICA

### CAMINO A: Ajuste Mínimo (Parches Rápidos)

**Alcance**: Corregir los 5 riesgos ALTO sin refactorizar la arquitectura.

**Cambios concretos**:
1. `ScheduleService.GetByStudentUserAsync`: cambiar a `ToListAsync()` y devolver el horario del grupo que coincida con la jornada del estudiante, o devolver todos los horarios de todos sus grupos.
2. `AprobadosReprobadosService.ObtenerGradosPorNivel`: leer los grados desde la BD en lugar de hardcodearlos.
3. Documentar en UI que `UpdateGroupAndGrade` sin `additive=true` reemplaza matrículas, y agregar confirmación explícita.
4. `StudentReportService.GetReportByStudentIdAsync`: particionar las notas por grupo/matrícula activa antes de calcular promedios.

**Impacto**: Bajo, cambios acotados.
**Riesgo**: Bajo, no toca arquitectura.
**Dificultad**: Media (lógica de negocio sensible).
**Tiempo estimado**: 2-3 semanas de un desarrollador.
**Módulos que toca**: ScheduleService, AprobadosReprobadosService, StudentReportService, UI de matrícula.

---

### CAMINO B: Refactor Controlado (Rediseño por Módulo)

**Alcance**: Además de los parches del Camino A, refactorizar los módulos con supuestos diurnos para que sean nativamente multi-matrícula.

**Cambios concretos adicionales**:
1. Crear un concepto explícito de "contexto académico" (NocturnalAcademicContext) que agrupe StudentAssignment + grupo de materias.
2. Refactorizar `StudentReportService` para generar reportes separados por contexto académico.
3. Eliminar el campo `Shift` texto libre de `groups` (migración).
4. Agregar constraint de unicidad explícita en `student_assignments` que prevenga duplicados del mismo grupo+grado+shift en el mismo año académico.
5. Unificar el sistema de deduplicación de matrículas.

**Impacto**: Medio, requiere trabajo coordinado entre módulos.
**Riesgo**: Medio, implica migraciones de BD y cambios en múltiples servicios.
**Dificultad**: Alta.
**Tiempo estimado**: 6-10 semanas de un desarrollador.
**Módulos que toca**: StudentAssignmentService, StudentReportService, ScheduleService, GroupService, migraciones de BD.

---

### CAMINO C: Rediseño Formal (Arquitectura Nocturna desde Base)

**Alcance**: Introducir el concepto de "Inscripción Académica" (`AcademicEnrollment`) como agregado raíz que encapsula la relación Estudiante-GradeLevel-Group-Shift-Period, y refactorizar todos los módulos para girar alrededor de este concepto.

**Cambios concretos**:
1. Nueva entidad `AcademicEnrollment` con estado propio y historial.
2. Todos los reportes, calificaciones, asistencia y horarios operan sobre `AcademicEnrollmentId` como contexto.
3. Reporte de aprobados/reprobados dinámico (sin grados hardcodeados).
4. Portal del estudiante multi-contexto.
5. Carnet multi-matrícula con QR que identifica contexto.

**Impacto**: Alto, toca toda la aplicación.
**Riesgo**: Alto, requiere migración de datos y pruebas exhaustivas.
**Dificultad**: Muy alta.
**Tiempo estimado**: 4-6 meses.
**Módulos que toca**: Todos.

---

### RECOMENDACIÓN

**Iniciar por Camino A inmediatamente** (parches para los 5 riesgos ALTO), seguido de **Camino B módulo por módulo** comenzando con el módulo de reportes (el más crítico para la dirección). El Camino C es deseable a largo plazo pero no es la prioridad inmediata.

**Orden de prioridad de correcciones**:
1. Reporte Aprobados/Reprobados (afecta dirección, datos erróneos)
2. Horario del estudiante (afecta experiencia del estudiante nocturno)
3. Reporte individual del estudiante (afecta tutor/padre)
4. Protección UI contra borrado accidental de matrículas múltiples
5. Constraint de unicidad en student_assignments por contexto

---

## ANEXO A — EVIDENCIA: ARCHIVOS Y RUTAS REVISADAS

| Archivo | Propósito auditado |
|---------|-------------------|
| `Models/Student.cs` | Tabla legada con Grade/GroupName como strings |
| `Models/StudentAssignment.cs` | Modelo de matrícula — admite múltiples activas |
| `Models/StudentSubjectAssignment.cs` | Inscripción por materia — constraint unique |
| `Models/SubjectAssignment.cs` | Oferta académica (materia + grado + grupo) |
| `Models/Group.cs` | Grupo — campo Shift duplicado |
| `Models/Attendance.cs` | Asistencia — incluye ShiftId |
| `Models/StudentActivityScore.cs` | Notas — ancladas a StudentAssignmentId |
| `Models/Activity.cs` | Actividad — anclada a GroupId |
| `Models/AcademicYear.cs` | Año académico |
| `Models/Shift.cs` | Jornada — entidad maestra |
| `Models/SchoolScheduleConfiguration.cs` | Configuración de horarios con campos de noche |
| `Models/SchoolDbContext.cs` | ORM — constraints e índices |
| `Services/Implementations/StudentAssignmentService.cs` | Matrícula — lógica multi-activo |
| `Services/Implementations/AttendanceService.cs` | Asistencia — soporte de shift |
| `Services/Implementations/ScheduleService.cs` | Horarios — FirstOrDefault crítico |
| `Services/Implementations/AprobadosReprobadosService.cs` | Reportes — grados hardcodeados |
| `Services/Implementations/StudentActivityScoreService.cs` | Notas — ResolveStudentAssignmentId |
| `Services/Implementations/StudentService.cs` | GetByGroupAndGrade sin distinct |
| `Services/Implementations/StudentIdCardService.cs` | Carnet — priorización nocturna |
| `Services/Implementations/StudentReportService.cs` | Reporte individual — sin partición |
| `Services/Implementations/ScheduleConfigurationService.cs` | Generación bloques — soporta noche |
| `Controllers/StudentAssignmentController.cs` | UI matrícula — additive flag |
| `Controllers/TeacherGradebookController.cs` | Portal docente |
| `Controllers/AttendanceController.cs` | Registro de asistencia |
| `Controllers/AcademicAssignmentController.cs` | Carga masiva de asignaciones |
| `Views/TeacherGradebook/Index.cshtml` | Vista libro de calificaciones |
| `Views/Attendance/Index.cshtml` | Vista asistencia general |
| `Views/ScheduleConfiguration/Index.cshtml` | Configuración horarios — campos noche |
| `Views/StudentIdCard/Generate.cshtml` | Carnet — texto "Nocturna" hardcodeado |
| `Views/StudentAssignment/Index.cshtml` | Listado matrículas — multi-pair correcto |

---

## ANEXO B — EVIDENCIA: CONSULTAS SQL EJECUTADAS Y RESULTADOS

### B.1 Jornadas configuradas
```sql
SELECT name, is_active, display_order FROM shifts ORDER BY display_order;
-- Resultado: Mañana, Tarde, Noche (3 jornadas, todas activas)
```

### B.2 Conteo de filas por tabla (tablas con datos)
```
subject_assignments:  1,142 filas
subjects:                75 filas
groups:                  30 filas
time_slots:              19 filas
users:                   13 filas
shifts:                   3 filas
```

Tablas sin datos (environment de desarrollo limpio): `student_assignments`, `attendance`, `student_activity_scores`, `prematriculations`.

### B.3 Constraints UNIQUE confirmados
- `attendance`: UNIQUE por `(student_id, date, group_id, grade_id, shift_id)` — correcto para nocturna
- `student_subject_assignments`: UNIQUE parcial `(student_id, subject_assignment_id, academic_year_id) WHERE is_active = true`
- `student_activity_scores`: UNIQUE por `(student_assignment_id, activity_id)` y por `(student_subject_assignment_id, activity_id)`
- `student_assignments`: **SIN constraint UNIQUE** — permite múltiples matrículas activas

### B.4 Grupos y jornadas en BD real
```
A → Noche | B → Mañana | C → Mañana | D → Mañana | E → Mañana
ESP7 → Noche | ESP8 → Noche | ESP9 → Noche
P → sin jornada | S → sin jornada
S1 → Mañana | S2 → Tarde | TM1 → Mañana
```

### B.5 Índice problemático en student_assignments
No hay UNIQUE index sobre `(student_id, grade_id, group_id, shift_id)`. La deduplicación es 100% responsabilidad de la aplicación.
