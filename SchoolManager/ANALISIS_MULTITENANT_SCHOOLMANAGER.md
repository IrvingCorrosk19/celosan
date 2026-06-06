# ANÁLISIS MULTITENANT COMPLETO – SCHOOLMANAGER

**Fecha:** 2026-06-06  
**Alcance:** Código fuente + esquema PostgreSQL producción (Render `schoolmanager_daqf`)  
**Tipo:** Auditoría de solo lectura — sin cambios en código ni base de datos.

---

## 1. Resumen ejecutivo

SchoolManager **tiene diseño multi-tenant a nivel de modelo de datos**: la mayoría de entidades académicas y operativas incluyen `school_id` (o `"SchoolId"` en `subject_assignments`). Los usuarios (`users`) se asocian a una escuela mediante `users.school_id`.

Sin embargo, **el aislamiento no está garantizado por la plataforma**. No existe filtro global de tenant en Entity Framework; la separación depende de que cada servicio/controlador aplique manualmente `WHERE SchoolId = @currentSchool`. Esa aplicación es **heterogénea**: módulos recientes (AprobadosReprobados, pagos club padres, carnets, matrícula avanzada) filtran bien; módulos legacy (asistencia, actividades, catálogo académico, director dashboard, varios CRUD) **listan o modifican datos sin filtro de escuela** o **sin autenticación**.

**Estado en producción hoy:** consulta SELECT confirmó **1 escuela activa**, **316 usuarios con `school_id`**, **1 usuario sin escuela** (probable SuperAdmin). Con una sola escuela el riesgo de fuga cruzada **no se manifiesta en operación actual**, pero la arquitectura **no es segura para multi-escuela real** sin remediación.

**Veredicto:** compatibilidad multi-tenant **PARCIAL (~60%)**.

---

## 2. Nivel actual de compatibilidad multi-tenant

| Dimensión | Estado | Notas |
|-----------|--------|-------|
| Modelo de datos (columna tenant) | **Bueno** | 36 tablas con `school_id` / `SchoolId` |
| Aislamiento automático (EF) | **Ausente** | Solo `HasQueryFilter` en `School.IsActive` y `TimeSlot` |
| Resolución de tenant en runtime | **Manual** | `CurrentUserService` → `users.school_id` vía BD, no en claims |
| Autorización por rol | **Parcial** | Muchos controladores legacy sin `[Authorize]` |
| Consistencia de filtros en servicios | **Baja–Media** | Patrones correctos coexisten con `GetAllAsync()` globales |
| IDOR por GUID | **Presente** | Varios endpoints aceptan `studentId`/`userId` sin validar escuela |
| SuperAdmin cross-tenant | **Intencional** | Por diseño |
| Producción actual (1 escuela) | **Enmascara riesgos** | No prueba aislamiento real |

---

## 3. Respuestas a las 17 preguntas clave

| # | Pregunta | Respuesta | Evidencia |
|---|----------|-----------|-----------|
| 1 | ¿Existe `school_id` en tablas principales? | **Sí, en la mayoría** | 36 tablas en BD (ver §4). Excepciones: `student_assignments`, `teacher_assignments`, carnets, junction tables |
| 2 | ¿Usuarios asociados a escuela? | **Sí** | `User.SchoolId` → `users.school_id` (`Models/User.cs:10`, `SchoolDbContext.cs:1450`) |
| 3 | ¿Estudiantes asociados a escuela? | **Sí** | Estudiantes son `users` con rol estudiante + `students.school_id`; tabla `students` también tiene `school_id` |
| 4 | ¿Docentes asociados a escuela? | **Sí** | Docentes son `users` con `school_id`; asignaciones vía `teacher_assignments` → `subject_assignments."SchoolId"` |
| 5 | ¿Grupos y grados separados por escuela? | **En BD sí; en UI/servicios a veces no** | `groups.school_id`, `grade_levels.school_id`; `GroupService.GetAllAsync()` / `GradeLevelService.GetAllAsync()` **sin filtro** |
| 6 | ¿Materias separadas por escuela? | **En BD sí; servicios inconsistentes** | `subjects.school_id`; `SubjectService.GetAllAsync()` global |
| 7 | ¿Matrículas filtran por escuela? | **Parcial** | `StudentAssignmentService.GetActiveAssignmentsForCurrentSchoolAsync()` sí; lecturas por `studentId` no validan caller |
| 8 | ¿Calificaciones filtran por escuela? | **Parcial** | `student_activity_scores.school_id` en escritura; gradebook lee por grupo/docente sin `SchoolId` explícito |
| 9 | ¿Asistencia filtra por escuela? | **Parcial** | `attendance.school_id` en modelo; `AttendanceService.GetAllAsync()` **sin filtro** |
| 10 | ¿Reportes filtran por escuela? | **Depende del módulo** | AprobadosReprobados: **sí**; StudentReport GetTrimesterData: **no**; DirectorService: **no** en agregados |
| 11 | ¿SuperAdmin ve todas las escuelas? | **Sí** | `[Authorize(Roles = "superadmin")]` en `SuperAdminController.cs:13` |
| 12 | ¿Admin/Director solo ve su escuela? | **Parcial** | `UserService.GetAllAsync()` filtra; catálogo/dashboard/director AJAX **no siempre** |
| 13 | ¿Teacher solo ve su escuela? | **Parcial** | Gradebook/orientación filtran por asignación; actividades legacy globales |
| 14 | ¿Student solo ve sus datos? | **Parcial** | Perfil/boletín Index usa usuario actual; `GetTrimesterData(studentId)` **acepta cualquier GUID** |
| 15 | ¿Parent solo ve hijos? | **Parcial** | Lista filtra por `parent.SchoolId`; `IsParentOfStudentAsync` usa `schoolId: null` → vínculo cross-school posible |
| 16 | ¿Consultas LINQ/SQL sin filtro escuela? | **Sí, múltiples** | Ver §6 y §8 |
| 17 | ¿Riesgos de fuga entre escuelas? | **Sí** | Alto en legacy sin auth + GetAll global; Medio en IDOR autenticado |

---

## 4. Tablas con `school_id` (evidencia BD producción)

Consulta ejecutada (solo SELECT):

```sql
SELECT table_name, column_name
FROM information_schema.columns
WHERE table_schema = 'public'
  AND column_name IN ('school_id', 'SchoolId')
ORDER BY 1;
```

**36 tablas con columna tenant:**

| Tabla | Columna |
|-------|---------|
| `academic_years` | `school_id` |
| `activities` | `school_id` |
| `activity_types` | `school_id` |
| `area` | `school_id` |
| `attendance` | `school_id` |
| `audit_logs` | `school_id` |
| `counselor_assignments` | `school_id` |
| `discipline_reports` | `school_id` |
| `email_configurations` / `EmailConfigurations` | `school_id` / `SchoolId` |
| `email_jobs` | `school_id` |
| `grade_levels` | `school_id` |
| `groups` | `school_id` |
| `id_card_template_fields` | `school_id` |
| `messages` | `school_id` |
| `orientation_reports` | `school_id` |
| `payment_concepts` | `school_id` |
| `payments` | `school_id` |
| `prematriculation_periods` | `school_id` |
| `prematriculations` | `school_id` |
| `school_id_card_settings` | `school_id` |
| `school_schedule_configurations` | `school_id` |
| `security_settings` | `school_id` |
| `shifts` | `school_id` |
| `specialties` | `school_id` |
| `student_activity_scores` | `school_id` |
| `student_payment_access` | `school_id` |
| `student_subject_assignments` | `school_id` |
| `students` | `school_id` |
| `subject_assignments` | **`"SchoolId"`** (PascalCase, inconsistente) |
| `subject_promotion_records` | `school_id` |
| `subjects` | `school_id` |
| `teacher_work_plans` | `school_id` |
| `time_slots` | `school_id` |
| `trimester` | `school_id` |
| `users` | `school_id` |

**Datos producción (SELECT):**

| Métrica | Valor |
|---------|-------|
| Escuelas (`schools`) | 1 |
| Usuarios con `school_id` | 316 |
| Usuarios sin `school_id` | 1 |

---

## 5. Tablas sin `school_id` directo (aislamiento indirecto)

**20 tablas operativas sin columna tenant** (consulta SELECT a `information_schema`):

| Tabla | Aislamiento indirecto | Riesgo si falta filtro en app |
|-------|----------------------|-------------------------------|
| `student_assignments` | `student_id` → `users.school_id`; `group_id` → `groups.school_id` | **Medio** |
| `teacher_assignments` | `subject_assignment_id` → `subject_assignments."SchoolId"` | **Medio** |
| `student_id_cards` | `student_id` → `users.school_id` | **Medio** |
| `institutional_credential_cards` | `user_id` → `users.school_id` | **Medio** |
| `staff_institutional_profiles` | `user_id` → `users.school_id` | **Medio** |
| `student_qr_tokens`, `staff_qr_tokens`, `scan_logs` | vía usuario | **Medio** |
| `activity_attachments` | `activity_id` → `activities.school_id` | **Bajo** |
| `prematriculation_histories` | `prematriculation_id` → `prematriculations.school_id` | **Bajo** |
| `schedule_entries` | `time_slots.school_id`, `academic_years.school_id` | **Medio** |
| `teacher_work_plan_details`, `teacher_work_plan_review_logs` | plan padre | **Bajo** |
| `user_grades`, `user_groups`, `user_subjects` | M2M con entidades tenant | **Medio** |
| `email_api_configurations`, `email_queues` | Global / vía job | **Bajo–Medio** |
| `schools` | Raíz tenant | N/A |

**Tablas que deberían considerar `school_id` directo** (recomendación, no implementada):

- `student_assignments` — consulta frecuente, cadena de joins larga
- `teacher_assignments` — mismo motivo
- `student_id_cards` / `institutional_credential_cards` — credenciales sensibles

---

## 6. Servicios con filtros correctos

| Servicio | Patrón | Evidencia |
|----------|--------|-----------|
| `AprobadosReprobadosService` | Parámetro `Guid schoolId` en todas las consultas | `AprobadosReprobadosService.cs:44+`, `:519`, `:668` |
| `UserService.GetAllAsync()` | `u.SchoolId == currentUser.SchoolId` | `UserService.cs:238-246` |
| `UserService.GetAllStudentsAsync()` | Filtra por escuela del caller | `UserService.cs:26-36` |
| `SubjectAssignmentService.GetAllSubjectAssignments()` | `sa.SchoolId == usercurrent.SchoolId` | `SubjectAssignmentService.cs:86-92` |
| `StudentAssignmentService.GetActiveAssignmentsForCurrentSchoolAsync()` | JOIN users por escuela | `StudentAssignmentService.cs:204-218` |
| `TrimesterService.GetAllAsync()` | Filtra por escuela actual | Patrón alineado con `DirectorController` |
| `ClubParentsPaymentService` | `GetCurrentUserSchoolAsync()` + filtros | `ClubParentsPaymentService.cs:37-50` |
| `StudentIdCardController.BuildEligibleStudentQuery()` | `u.SchoolId == schoolId` (excepto SuperAdmin) | `StudentIdCardController.cs:657-668` |
| `ActivityService` (portal docente) | `a.SchoolId == currentUserSchool.Id` en create/list | `ActivityService.cs:47-75`, `:245-249` |
| `CounselorAssignmentService.GetBySchoolIdAsync()` | Explícito por escuela | Usado en `CounselorAssignmentController` |
| `PaymentService.GetBySchoolAsync()` / listados | Filtra por `schoolId` | `PaymentController.cs:47-51` |

---

## 7. Servicios con filtros faltantes o parciales

| Servicio | Problema | Riesgo |
|----------|----------|--------|
| `GroupService.GetAllAsync()` | Sin `SchoolId` | **Alto** |
| `SubjectService.GetAllAsync()` | Sin `SchoolId` | **Alto** |
| `GradeLevelService.GetAllAsync()` | Sin `SchoolId` | **Alto** |
| `ShiftService.GetAllAsync()` | Sin `SchoolId` | **Alto** |
| `SpecialtyService.GetAllAsync()` | Sin `SchoolId` | **Alto** |
| `AttendanceService.GetAllAsync()` | Sin `SchoolId` | **Alto** |
| `ActivityService.GetAllAsync()` / `GetByIdAsync()` | Sin `SchoolId` | **Alto** |
| `DisciplineReportService.GetAllAsync()` | Sin `SchoolId` | **Alto** |
| `OrientationReportService.GetAllAsync()` | Sin `SchoolId` | **Alto** |
| `TeacherAssignmentService.GetAllWithIncludesAsync()` | Sin `SchoolId` | **Alto** |
| `StudentActivityScoreService` (lecturas gradebook) | Por grupo/docente, no escuela | **Alto** |
| `UserService.GetByIdAsync()` | `FindAsync(id)` sin tenant | **Alto** (IDOR) |
| `UserService.GetAllWithAssignmentsByRoleSA()` | Global, todas las escuelas | **Alto** |
| `DirectorService` (AJAX agregados) | `_context.Subjects`, `_context.Users`, scores sin filtro | **Crítico** |
| `PaymentService.GetByIdAsync()` | Sin validación escuela | **Medio** |
| `StudentReportService` | Por `studentId`, no valida caller | **Medio** |
| `StudentAssignmentService` (reads por ID) | Por estudiante/grupo, no caller school | **Medio** |
| `SubjectPromotionService` | Por `studentId` sin check escuela | **Alto** |

---

## 8. Controladores con riesgo

### 8.1 Sin `[Authorize]` a nivel de clase (acceso anónimo posible)

| Controlador | Riesgo | Evidencia |
|-------------|--------|-----------|
| `AttendanceController` | **Alto** | `GetAllAsync()` global — `AttendanceController.cs:8-20` |
| `ActivityController` | **Alto** | `ActivityController.cs:6-20` |
| `DisciplineReportController` | **Alto** | Sin `[Authorize]` |
| `AcademicCatalogController` | **Alto** | `Index` carga catálogos globales — `:54-58` |
| `StudentReportController` | **Alto** | Sin `[Authorize]`; `GetTrimesterData` IDOR — `:16`, `:123-130` |
| `SubjectAssignmentController` | **Alto** | Sin `[Authorize]` en clase; algunos métodos filtran por escuela manualmente |
| `GroupController`, `SubjectController`, `GradeLevelController` | **Alto** | CRUD catálogo sin auth |
| `SchoolController`, `AuditLogController`, `SecuritySettingController` | **Alto** | Administración global sin auth aparente |

### 8.2 Autenticados pero con IDOR / tenant débil

| Controlador | Riesgo | Evidencia |
|-------------|--------|-----------|
| `UserController` | **Alto** | `Edit`/`Delete`/`GetById` → `GetByIdAsync` sin validar escuela |
| `SubjectPromotionController` | **Alto** | `Records`, `Promote`, `CloseYear` por `studentId` sin `SchoolId` — `:30-48` |
| `ParentAcademicController` | **Medio** | `IsParentOfStudentAsync` con `schoolId: null` — `:80-83` |
| `PaymentController.Details` | **Medio** | `GetByIdAsync` sin check escuela |
| `DirectorController` | **Medio–Alto** | Autorizado, pero servicio agrega datos cross-tenant |

### 8.3 Patrones correctos (referencia)

| Controlador | Evidencia |
|-------------|-----------|
| `AprobadosReprobadosController` | Pasa `currentUser.SchoolId.Value` al servicio — `:95-96` |
| `StudentAssignmentController` | Valida misma escuela en mutaciones — `:78-81` |
| `SuperAdminController` | Cross-tenant intencional con rol `superadmin` |

---

## 9. Reportes con riesgo

| Reporte | Filtra escuela | Riesgo | Evidencia |
|---------|----------------|--------|-----------|
| Aprobados/Reprobados | **Sí** | **Bajo** | `AprobadosReprobadosController.cs:95-96` |
| Boletín estudiante (Index) | **Sí** (usuario actual) | **Bajo** | `StudentReportController.Index` |
| Boletín AJAX `GetTrimesterData` | **No** | **Alto** | Cualquier `studentId` + sin auth |
| Dashboard Director | **Parcial** | **Crítico** | Estudiantes filtrados; materias/scores globales en `DirectorService` |
| Portal acudiente | **Parcial** | **Medio** | Lista por escuela; parentesco sin escuela |
| Carnet estudiante | **Sí** | **Bajo** | `StudentIdCardController` |
| Credencial institucional | SuperAdmin | **Bajo** (diseño) | `InstitutionalCredentialController` |

---

## 10. Matriz por módulo

| Módulo | Filtra por escuela | Riesgo | Evidencia | Observación |
|--------|-------------------|--------|-----------|-------------|
| **StudentAssignment** | Parcial | **Medio** | `GetActiveAssignmentsForCurrentSchoolAsync` OK; reads por GUID | Controller valida escuela en mutaciones recientes |
| **TeacherAssignment** | No (servicio) | **Alto** | `TeacherAssignmentService.cs:112-122` | UI puede listar asignaciones de otras escuelas |
| **TeacherGradebook** | Parcial | **Medio** | `[Authorize(Roles = "teacher")]`; scores por grupo | Depende de IDs de asignación |
| **Attendance** | No (lecturas) | **Alto** | `AttendanceController` sin auth + `GetAllAsync()` | Crítico en multi-escuela |
| **Activities** | Parcial | **Alto** | Legacy `GetAllAsync` global; portal docente OK | Dos code paths |
| **StudentReport** | Parcial | **Alto** | Index OK; `GetTrimesterData` IDOR | Sin `[Authorize]` en controller |
| **Parent Portal** | Parcial | **Medio** | `ParentAcademicController.cs:35`, `:80-83` | Parentesco cross-school posible |
| **Student Portal** | Parcial | **Medio** | `StudentProfileController` autorizado | Perfil atado a usuario actual |
| **AprobadosReprobados** | **Sí** | **Bajo** | Servicio + controller | Modelo de referencia |
| **AcademicCatalog** | Parcial | **Alto** | `SaveCatalog` OK; `Index` global | Mezcla catálogos en UI |
| **AcademicAssignment** | Parcial | **Alto** | Bulk save OK; modals `GetAllAsync` | Carga masiva docentes |
| **InstitutionalCredential** | SuperAdmin | **Bajo** | Por diseño plataforma | No es bug |
| **StaffDirectory** | SuperAdmin | **Bajo** | `SuperAdminController` | Intencional |
| **Payments** | Parcial | **Medio** | Listados OK; `Details` IDOR | |
| **Dashboard (Director)** | No (agregados) | **Crítico** | `DirectorService.cs:46-47`, `:304+` | Mezcla estadísticas globales |
| **SubjectPromotion** | No | **Alto** | `SubjectPromotionController` sin check escuela | Promoción cross-tenant |
| **StudentIdCard** | **Sí** | **Bajo** | Query por `schoolId` | SuperAdmin bypass intencional |
| **Messaging** | Parcial | **Medio** | Filtra destinatarios por escuela del sender | |
| **Prematriculation** | **Sí** | **Bajo** | `prematriculations.school_id` | |
| **EmailQueue** | Parcial | **Medio** | Jobs por escuela; API config global | |

---

## 11. Infraestructura de tenant (código)

### CurrentUserService

```29:55:SchoolManager/Services/Implementations/CurrentUserService.cs
public async Task<User?> GetCurrentUserAsync()
{
    var userId = await GetCurrentUserIdAsync();
    ...
    return await _context.Users.FindAsync(userId.Value);
}

public async Task<School?> GetCurrentUserSchoolAsync()
{
    var user = await GetCurrentUserAsync();
    if (user == null || user.SchoolId == null)
        return null;
    return await _context.Schools.FindAsync(user.SchoolId.Value);
}
```

- **`SchoolId` no está en claims/cookie** — se resuelve en cada request desde BD.
- **No hay middleware global de tenant** ni `HasQueryFilter(e => e.SchoolId == current)`.

### Authorization (Program.cs)

```334:342:SchoolManager/Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdmin", policy => policy.RequireRole("SuperAdmin"));
    ...
});
```

- Políticas declaradas; **uso real mayormente `[Authorize(Roles = "...")]`** con roles en minúsculas en BD (`admin`, `superadmin`, `teacher`).
- **Sin `FallbackPolicy`** que exija autenticación en todos los endpoints.

### Roles — alcance esperado vs observado

| Rol | Esperado | Observado |
|-----|----------|-----------|
| SuperAdmin | Todas las escuelas | ✅ `SuperAdminController` |
| Admin | Solo su escuela | ⚠️ Parcial — usuarios OK; catálogo/legacy no |
| Director | Solo su escuela | ⚠️ Dashboard agrega datos globales |
| Teacher | Sus grupos/materias | ⚠️ Gradebook OK; actividades legacy no |
| Student | Solo sus datos | ⚠️ IDOR en report AJAX |
| Parent/Acudiente | Solo hijos | ⚠️ Parentesco sin filtro escuela |

---

## 12. Riesgos críticos (clasificados)

### Alto — permite ver o modificar datos de otra escuela

1. **Controladores legacy sin `[Authorize]`** — asistencia, actividades, catálogo, reportes estudiante, CRUD grupos/materias.
2. **`GetAllAsync()` globales** en servicios de catálogo y operación.
3. **IDOR por GUID** — `UserController` (edit/delete), `StudentReportController.GetTrimesterData`, `SubjectPromotionController`.
4. **`DirectorService` agregados** — consultas a `Subjects`, `Users`, `StudentActivityScores` sin `SchoolId`.
5. **`UserPasswordManagementService.GetAllUsersAsync()`** — usuarios de todas las escuelas (si endpoint expuesto).

### Medio — mezcla datos o depende de indirectos

1. Matrículas/asignaciones sin `school_id` directo — joins omitidos en queries ad-hoc.
2. Parent portal — validación parentesco sin escuela.
3. Payment details por ID.
4. Gradebook/scores — filtro por grupo, no por escuela del caller.
5. `subject_assignments."SchoolId"` nullable + naming inconsistente.

### Bajo — visual, global intencional o una escuela en prod

1. Áreas/catálogos globales con flag `IsGlobal`.
2. SuperAdmin cross-tenant (diseño).
3. Configuración email API global.
4. Riesgo no visible hoy con **1 escuela** en producción.

---

## 13. Recomendaciones (sin implementar)

1. **Filtro global EF** — `HasQueryFilter` por `ISchoolEntity` + interceptor que inyecte `CurrentSchoolId` (excepto SuperAdmin).
2. **Claim `SchoolId` en login** — reducir round-trips y habilitar validación en middleware.
3. **`[Authorize]` global** — `FallbackPolicy = RequireAuthenticatedUser()` + lista blanca pública (`Auth`, QR público).
4. **Helper `EnsureSameSchool(resourceSchoolId)`** — usar en todos los controllers que reciben GUIDs.
5. **Eliminar/restringir `GetAllAsync()` sin tenant** — reemplazar por `GetAllForSchoolAsync(Guid schoolId)`.
6. **Auditar controladores legacy** — Attendance, Activity, AcademicCatalog, StudentReport, SubjectAssignment, Group, Subject, GradeLevel.
7. **Añadir `school_id` a `student_assignments` y `teacher_assignments`** — migración futura (fuera de alcance de esta auditoría).
8. **Unificar columna `subject_assignments.SchoolId`** → `school_id` snake_case.
9. **Unique constraints por escuela** — revisar `grade_levels.name`, `specialties.name` (globales hoy).
10. **Tests de aislamiento** — dos escuelas fixture; probar IDOR en endpoints críticos.

---

## 14. Conclusión final

SchoolManager **no es multi-tenant seguro end-to-end hoy**. Tiene **fundamentos de datos correctos** (columna tenant en la mayoría de tablas, usuarios ligados a escuela, módulos nuevos bien filtrados), pero **la capa de aplicación no aplica aislamiento de forma sistemática**. Varios módulos legacy permitirían **lectura o modificación cross-tenant** si hubiera más de una escuela en la misma base de datos.

En producción actual (**1 escuela**) el sistema **opera como single-tenant de facto**, lo que **oculta** vulnerabilidades de diseño.

---

## RESPUESTA FINAL OBLIGATORIA

### 1. ¿SchoolManager es realmente multi-tenant hoy?

**PARCIAL**

### 2. Porcentaje estimado de compatibilidad multi-tenant

**~60%** (modelo de datos ~85%; capa aplicación ~45%; auth/IDOR ~50%)

### 3. ¿Puede una escuela ver datos de otra?

**SÍ** — en múltiples módulos legacy y endpoints IDOR, **si coexistieran dos o más escuelas en la misma BD**. En producción actual con 1 escuela, **no hay segunda escuela de la cual filtrar**, pero el código **no garantiza** el aislamiento.

### 4. ¿Qué módulos son seguros?

- Aprobados/Reprobados  
- Prematriculación / pagos (listados principales)  
- Club padres (pagos)  
- Carnet estudiante (listado/generación)  
- Matrícula estudiante (`GetActiveAssignmentsForCurrentSchoolAsync`, mutaciones con check escuela)  
- SuperAdmin (cross-tenant intencional, no es fuga)  
- Actividades — **solo** flujo portal docente con `SchoolId` explícito  

### 5. ¿Qué módulos tienen riesgo?

- **Crítico:** Director Dashboard (`DirectorService`), controladores sin auth  
- **Alto:** Asistencia, Actividades legacy, Catálogo académico (Index), TeacherAssignment global, StudentReport AJAX, SubjectPromotion, User edit/delete por ID, SubjectAssignment sin auth  
- **Medio:** Parent portal (parentesco), Gradebook reads, Payment details, StudentAssignment reads por GUID  

### 6. ¿Qué cambios serían necesarios?

Ver **§13 Recomendaciones**. Prioridad: (1) auth global, (2) filtro tenant EF o helper obligatorio, (3) cerrar IDOR, (4) refactor `GetAllAsync`, (5) remediar DirectorService y controladores legacy.

---

*Documento generado por auditoría estática de código y consultas SELECT en PostgreSQL producción. No se ejecutó UPDATE/DELETE/INSERT/ALTER.*
