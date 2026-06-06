# AUDITORÍA COMPLETA MULTITENANT + EDUCACIÓN NOCTURNA – SCHOOLMANAGER

**Fecha:** 2026-05-25  
**Alcance:** Código fuente (rama `main`, commits hasta `edd9fb5`) + esquema y datos PostgreSQL producción (`schoolmanager_daqf`, Render)  
**Tipo:** Solo lectura — **sin cambios** en código, BD, migraciones ni datos.  
**Consultas BD:** únicamente `SELECT` vía `psql` (PostgreSQL 18).

---

## 1. Resumen ejecutivo

SchoolManager **evolucionó de un diseño multi-tenant manual a un modelo híbrido**: columna `school_id`/`SchoolId` en **36 tablas**, filtros globales EF (`HasQueryFilter`) en **25 entidades**, middleware de tenant (`TenantContextMiddleware` + claim `school_id`), política global `RequireAuthenticatedUser`, y correcciones recientes en cargas masivas (`edd9fb5`).

**Producción hoy:** **1 escuela**, **317 usuarios** (316 con `school_id`, 1 sin escuela — probable SuperAdmin), **576 matrículas `Nocturno`**, **407 imparticiones**. Con una sola escuela activa, **no se ha probado aislamiento real entre dos colegios**; los riesgos son arquitectónicos y de código, no incidentes observados.

**Educación nocturna:** implementada a nivel funcional (`EnrollmentTypeConstants`, matrícula multi-nivel, arrastre/refuerzo, `NocturnalAdvancedEnrollment.EnableForAllSchools: true`). Aislada por escuela **en la mayoría de flujos**, con las mismas brechas multi-tenant residuales.

| Veredicto | Resultado |
|-----------|-----------|
| Multi-tenant general | **PARCIAL (~78 %)** |
| Multi-escuela nocturna | **PARCIAL (~82 %)** |
| ¿Puede una escuela ver datos de otra hoy? | **NO** en operación normal con filtros EF activos; **SÍ** vía SuperAdmin, `IgnoreQueryFilters`, o endpoints con IDOR/entidades sin filtro |

---

## 2. Estado actual multi-tenant

| Dimensión | Estado | Evidencia |
|-----------|--------|-----------|
| Columna tenant en BD | **Bueno** | 36 tablas con `school_id` / `"SchoolId"` (§5) |
| Filtros EF globales | **Implementado (parcial)** | 25 entidades en `SchoolDbContext.Tenant.cs` |
| Middleware tenant | **Implementado** | `TenantContextMiddleware` + `ITenantContext` |
| Claim `school_id` en auth | **Implementado** | `AuthService`, `ApiBearerTokenMiddleware` |
| Política auth global | **Implementado** | `FallbackPolicy = RequireAuthenticatedUser` |
| SuperAdmin bypass | **Intencional** | `BypassTenantFilter = true` para rol superadmin |
| Filtros manuales en servicios | **Heterogéneo** | Algunos explícitos; otros confían solo en EF |
| Entidades con `school_id` sin EF filter | **Riesgo medio** | `email_configurations`, `payment_concepts`, `messages`, etc. (§7) |
| Tablas sin columna tenant | **Riesgo medio** | `student_assignments`, `teacher_assignments`, carnets, staff (§6) |
| Restricciones BD por escuela | **Débil** | `users.email` único **global** (`users_email_key`); sin UNIQUE compuesto `(school_id, name)` en catálogos |
| Producción multi-escuela | **No probada** | 1 fila en `schools` |
| Vistas SQL | **Ninguna** | 0 vistas en esquema `public` |

---

## 3. Estado actual educación nocturna multi-tenant

| Funcionalidad | Estado | Notas |
|---------------|--------|-------|
| Matrícula nocturna (`Nocturno`) | **Operativo** | 576 `student_assignments` con `enrollment_type = 'Nocturno'` en prod |
| Materias pendientes / Refuerzo | **Operativo** | `EnrollmentTypeConstants.Refuerzo/Libre`; auto-refuerzo en carga masiva |
| Arrastre multi-nivel | **Operativo** | `StudentAssignmentService`, bulk upload |
| Multi-grupo / multi-nivel | **Operativo** | Múltiples `student_assignments` por estudiante |
| Docentes multinivel | **Operativo** | `teacher_assignments` por impartición |
| Flag avanzado nocturno | **Global ON** | `appsettings.json`: `EnableForAllSchools: true` |
| Promoción parcial | **Código listo; sin uso prod** | 0 filas en `subject_promotion_records` |
| Aislamiento por escuela en flujos nocturnos | **Parcial** | Depende de EF + `school_id` del operador; mismos gaps que multi-tenant general |

---

## 4. Arquitectura actual de aislamiento por escuela

```
Login → AuthService emite claim school_id
     → TenantContextMiddleware → ITenantContext.SchoolId / BypassTenantFilter (SuperAdmin)
     → EF HasQueryFilter en 25 entidades
     → Servicios/controladores: filtros manuales adicionales (heterogéneos)
     → SchoolTenantHelper / CanAccessSchool / StudentBelongsToTenantAsync (puntos críticos)
```

**Capas de defensa:**

1. **BD:** columna tenant en mayoría de tablas académicas.
2. **EF global filters:** bloquean lecturas cross-tenant para usuarios con `SchoolId` en claim.
3. **Validación explícita:** `SubjectPromotionController`, `PaymentController`, cargas masivas recientes, `ParentAcademicController`.
4. **SuperAdmin:** bypass controlado por rol.

**Puntos débiles:** entidades sin `HasQueryFilter`, tablas puente sin `school_id`, endpoints que aceptan IDs del cliente sin validar pertenencia, servicios con `IgnoreQueryFilters` o `GetAllAsync` sin escuela.

---

## 5. Tablas con `school_id` / `"SchoolId"` (evidencia BD)

Consulta ejecutada:

```sql
SELECT table_name, column_name
FROM information_schema.columns
WHERE table_schema = 'public'
  AND column_name IN ('school_id', 'SchoolId')
ORDER BY table_name;
```

**36 tablas:**

| Tabla | Columna | EF HasQueryFilter |
|-------|---------|-------------------|
| `academic_years` | `school_id` | Sí |
| `activities` | `school_id` | Sí |
| `activity_types` | `school_id` | **No** |
| `area` | `school_id` | **No** (catálogo global `IsGlobal`) |
| `attendance` | `school_id` | Sí |
| `audit_logs` | `school_id` | Sí |
| `counselor_assignments` | `school_id` | Sí |
| `discipline_reports` | `school_id` | Sí |
| `email_configurations` | `school_id` | **No** |
| `EmailConfigurations` | `SchoolId` | **No** (tabla legacy duplicada) |
| `email_jobs` | `school_id` | **No** |
| `grade_levels` | `school_id` | Sí |
| `groups` | `school_id` | Sí |
| `id_card_template_fields` | `school_id` | **No** |
| `messages` | `school_id` | **No** |
| `orientation_reports` | `school_id` | Sí |
| `payment_concepts` | `school_id` | **No** |
| `payments` | `school_id` | Sí |
| `prematriculation_periods` | `school_id` | **No** |
| `prematriculations` | `school_id` | Sí |
| `school_id_card_settings` | `school_id` | **No** |
| `school_schedule_configurations` | `school_id` | **No** |
| `security_settings` | `school_id` | Sí |
| `shifts` | `school_id` | Sí |
| `specialties` | `school_id` | Sí |
| `student_activity_scores` | `school_id` | Sí |
| `student_payment_access` | `school_id` | **No** |
| `student_subject_assignments` | `school_id` | Sí |
| `students` | `school_id` | Sí |
| `subject_assignments` | `"SchoolId"` | Sí |
| `subject_promotion_records` | `school_id` | Sí |
| `subjects` | `school_id` | Sí |
| `teacher_work_plans` | `school_id` | Sí |
| `time_slots` | `school_id` | Sí |
| `trimester` | `school_id` | Sí |
| `users` | `school_id` | Sí |

**Datos producción (SELECT):**

| Métrica | Valor |
|---------|-------|
| Escuelas | 1 |
| Usuarios totales | 317 |
| Usuarios con `school_id` | 316 |
| Usuarios sin `school_id` | 1 |
| `subject_assignments` | 407 |
| `student_assignments` Nocturno / Regular | 576 / 1 |
| `student_subject_assignments` Nocturno / Regular | 24 / 11 |
| `subject_promotion_records` | 0 |
| Vistas SQL | 0 |

**Índices relevantes:** `users_email_key` (email único global), `idx_email_configurations_school_id`.

---

## 6. Tablas sin `school_id` — relación indirecta

| Tabla | Relación indirecta con escuela | Aislada | Riesgo | Observaciones |
|-------|-------------------------------|---------|--------|---------------|
| `schools` | Raíz tenant | N/A | Bajo | Tabla global de instituciones |
| `student_assignments` | `student_id` → `users.school_id` | Parcial | **Medio** | Sin columna tenant; EF filtra vía `sa.Student.SchoolId` |
| `teacher_assignments` | `subject_assignment_id` → `"SchoolId"` | Parcial | **Medio** | EF filtra vía `SubjectAssignment.SchoolId` |
| `activity_attachments` | `activity_id` → `activities.school_id` | Parcial | Medio | Sin EF filter propio |
| `schedule_entries` | `teacher_assignment_id` → cadena SA | Parcial | Medio | Sin EF filter |
| `staff_institutional_profiles` | `user_id` → `users.school_id` | Parcial | **Medio** | Sin `school_id` ni EF filter |
| `institutional_credential_cards` | `user_id` → staff profile | Parcial | Medio | Credenciales mezclables si falla filtro usuario |
| `staff_qr_tokens` | staff profile | Parcial | Medio | Tokens públicos con validación en servicio |
| `student_id_cards` | `user_id` / estudiante | Parcial | Medio | PDF usa `IgnoreQueryFilters` acotado |
| `student_qr_tokens` | estudiante | Parcial | Medio | Similar carnet |
| `prematriculation_histories` | `prematriculation_id` | Parcial | Medio | Historial sin tenant directo |
| `teacher_work_plan_details` | `teacher_work_plans.school_id` | Parcial | Medio | Padre filtrado; hijo no |
| `teacher_work_plan_review_logs` | plan padre | Parcial | Medio | Idem |
| `user_groups`, `user_subjects`, `user_grades` | `user_id` → users | Parcial | Medio | Junction tables legacy |
| `email_queues` | job/user | Parcial | Medio | Worker usa `IgnoreQueryFilters` con rol |
| `email_api_configurations` | Plataforma | Global | Bajo | Config SMTP API global |
| `scan_logs` | QR scans | Parcial | Bajo | Auditoría transversal |
| `__EFMigrationsHistory` | Sistema | N/A | Bajo | Migraciones EF |

---

## 7. Tablas con `school_id` pero sin aislamiento EF suficiente

Estas tablas **tienen columna tenant** pero **no están en `HasQueryFilter`**. Dependen de filtros manuales en servicio/controlador.

| Tabla | Riesgo | Evidencia |
|-------|--------|-----------|
| `email_configurations` | **Alto** | `EmailConfigurationService.GetAllAsync()` lista **todas** las escuelas con SMTP/password |
| `payment_concepts` | **Alto** | `GetByIdAsync`/`UpdateAsync` por id sin validar escuela |
| `prematriculation_periods` | **Alto** | `GetByIdAsync` id-only |
| `school_schedule_configurations` | **Medio** | Confía en `schoolId` del caller |
| `messages` | **Medio** | Scoped por `userId`, no por escuela |
| `activity_types` | **Medio** | Catálogo con `school_id` sin filter EF |
| `email_jobs` | **Medio** | Jobs por escuela sin filter global |
| `id_card_template_fields` | **Medio** | Settings carnet |
| `school_id_card_settings` | **Medio** | Settings carnet |
| `student_payment_access` | **Medio** | Acceso plataforma por pago |
| `area` | **Bajo** | Diseño global compartido (`IsGlobal = true`) |

---

## 8. Controladores seguros (evidencia)

| Controlador | Motivo |
|-------------|--------|
| `SubjectPromotionController` | `StudentBelongsToTenantAsync` en acciones sensibles |
| `ParentAcademicController` | `IsParentOfStudentAsync` + filtro `parent.SchoolId` |
| `PaymentController` | `CanAccessSchool`, ownership prematrícula/estudiante |
| `CounselorAssignmentController` | `GetBySchoolIdAsync(currentUser.SchoolId)` |
| `AprobadosReprobadosController` | Pasa `currentUser.SchoolId` al servicio |
| `ClubParentsController` | Servicio acota por escuela del operador |
| `StaffInstitutionalProfileController` | Self-only + roles |
| `AcademicCatalogController` / `AcademicAssignmentController` | Carga masiva con `schoolId` operador + validación email cross-tenant |
| `StudentAssignmentController` (bulk reciente) | `SchoolTenantHelper`, jornada por escuela |
| `SuperAdminController` | Cross-tenant intencional, rol superadmin |
| `SubjectAssignmentController` (Index/Create/bulk principal) | Filtro explícito `sa.SchoolId == schoolId` |
| `ScheduleController` | Valida `u.SchoolId` / `g.SchoolId` |
| `PaymentConceptController` | Compara `concept.SchoolId` vs usuario |
| Catálogo CRUD (`Group`, `GradeLevel`, `Subject`, `Specialty`) | EF filters + `[Authorize]` admin |

---

## 9. Controladores con riesgo

| Controlador | Método | Riesgo | Issue |
|-------------|--------|--------|-------|
| `SubjectAssignmentController` | `SaveAssignmentsSingle` | **Alto** | Crea `SubjectAssignment` **sin `SchoolId`**, sin área/especialidad/grado |
| `TeacherGradebookController` | `GuardarNotasTemp`, `GetNotasCargadas` | **Alto** | Acepta `TeacherId` del **body** del cliente; IDOR horizontal dentro de la escuela |
| `EmailConfigurationController` | `Details`, `Edit`, `Delete` | **Alto** | `GetByIdAsync(id)` sin validar escuela del caller |
| `UserController` | `GetUserJson` | **Alto** | Expone `PasswordHash` (secreto, no tenant pero crítico) |
| `StudentReportController` | Clase completa | **Medio** | Sin `[Authorize(Roles=...)]`; solo `FallbackPolicy`; `GetTrimesterData` sí valida tenant |
| `StudentAssignmentController` | `AddSubjectEnrollment`, `Assign`, etc. | **Medio** | Mutaciones por id sin `SchoolTenantHelper` explícito |
| `TeacherAssignmentController` | `SaveAssignments` | **Medio** | `TeacherId` del cliente; admin puede reasignar cualquier docente del tenant |
| `AttendanceController` | CRUD por `id` | **Medio** | Sin validación explícita de escuela en controlador |
| `ActivityController` | CRUD | **Medio** | Idem |
| `AcademicCatalogController` | `EditarTrimestre`, `DeleteShift` | **Medio** | Opera por entity id |
| `DirectorController` | Dashboard | **Bajo–Medio** | Usa `UserService.GetAllAsync()` (filtra escuela) + EF en materias; sin `SchoolId` explícito en controlador |
| `InstitutionalCredentialController` | Rutas SuperAdmin | **Bajo** | `IgnoreQueryFilters` intencional con rol |

---

## 10. Servicios seguros

Servicios que combinan **EF filter** + **filtro explícito** o alcance acotado:

`UserService` (listas), `TrimesterService`, `SubjectAssignmentService`, `ActivityService`, `AcademicYearService`, `StudentAssignmentService`, `StudentReportService`, `SubjectPromotionService`, `PaymentService`, `PrematriculationService`, `AprobadosReprobadosService`, `ClubParentsPaymentService`, `CounselorAssignmentService`, `UserPasswordManagementService`, `ScheduleService`, `ScheduleConfigurationService`, `TeacherWorkPlanService`, `AuthService`, `SuperAdminService` (by design), `NocturnalEnrollmentSettingsService`, catálogos con EF filter (`Group`, `GradeLevel`, `Subject`, `Shift`, `Specialty`).

---

## 11. Servicios con riesgo

| Servicio | Método | Riesgo | Evidencia |
|----------|--------|--------|-----------|
| `EmailConfigurationService` | `GetAllAsync`, `GetByIdAsync`, `UpdateAsync` | **Alto** | Entidad sin EF filter; expone credenciales SMTP cross-tenant |
| `SchoolService` | `GetAllAsync`, `GetByIdAsync` | **Alto** | Lista todas las escuelas sin check |
| `PaymentConceptService` | `GetByIdAsync`, `UpdateAsync`, `DeleteAsync` | **Alto** | Sin EF filter ni ownership |
| `PrematriculationPeriodService` | `GetByIdAsync` | **Alto** | Id-only |
| `TeacherAssignmentService` | `GetOrCreateSubjectAssignment` | **Medio** | Crea SA sin `SchoolId` |
| `UserService` | `GetByIdAsync`, `UpdateAsync` | **Medio** | Solo EF filter |
| `StudentAssignmentService` | `GetAssignmentsByStudentIdAsync` | **Medio** | Por `studentId` sin check caller |
| `InstitutionalCredentialService` / PDF services | Varios | **Medio** | `IgnoreQueryFilters` en schools/settings |
| `MessagingService` | Inbox | **Medio** | `Message` sin EF filter; scope por userId |
| `DirectorService` | `GetDashboardViewModelAsync` | **Bajo** | Usa servicios filtrados; agregados confían en EF |

---

## 12. Vistas / reportes con riesgo

| Pantalla / export | Riesgo | Observación |
|-------------------|--------|-------------|
| `TeacherGradebook/*` (guardado notas) | **Alto** | TeacherId manipulable desde cliente |
| `SubjectAssignment/Upload` → `SaveAssignmentsSingle` | **Alto** | Imparticiones huérfanas |
| `EmailConfiguration/Details` | **Alto** | IDOR config SMTP otra escuela |
| `User/*` JSON admin | **Alto** | Password hash en respuesta |
| `StudentReport/*` | **Medio** | Sin rol explícito; trimestre validado |
| `Attendance/*`, `Activity/*` | **Medio** | Listados OK con EF; CRUD por id |
| `Director/Dashboard` | **Bajo** | Datos agregados del tenant visible |
| `AprobadosReprobados/*` PDF/Excel | **Bajo** | Scoped por `SchoolId` |
| `StudentAssignment/Index` | **Bajo** | Servicios + EF |
| `ParentAcademic/*` | **Bajo** | Vinculación padre-hijo |
| `InstitutionalCredential` público | **Bajo** | Token firmado; diseño público |
| Exportaciones masivas Upload | **Bajo** | Post-`edd9fb5`: escuela del operador |

**Vistas SQL en BD:** ninguna — reportes son generados en aplicación (Razor + servicios PDF).

---

## 13. Matriz por módulo

| Módulo | Multi-tenant seguro | Filtra por escuela | Riesgo | Evidencia | Observación |
|--------|--------------------|--------------------|--------|-----------|-------------|
| StudentAssignment | Parcial | Sí (EF + bulk) | Medio | `StudentAssignmentController`, EF `StudentAssignment` | Bulk reciente OK; mutaciones AJAX sin helper explícito |
| TeacherAssignment | Parcial | Sí (EF) | Medio | `TeacherAssignmentController` | Admin puede asignar cualquier docente del tenant |
| TeacherGradebook | **No** | Parcial | **Alto** | `GuardarNotasTemp` | TeacherId del body |
| Attendance | Parcial | Sí (EF) | Medio | `AttendanceController`, `AttendanceService` | CRUD legacy |
| Activities | Parcial | Sí (EF) | Medio | `ActivityController` | Idem |
| StudentReport | Parcial | Sí (parcial) | Medio | `StudentReportController` | Falta `[Authorize]` por rol |
| StudentProfile | Sí | Sí | Bajo | Self-only | |
| Parent Portal | Sí | Sí | Bajo | `ParentAcademicController`, `ClubParentsController` | |
| AprobadosReprobados | Sí | Sí | Bajo | `schoolId` explícito | Áreas globales |
| AcademicCatalog | Parcial | Sí | Medio | Bulk + EF | Edición entidades por id |
| AcademicAssignment | Parcial | Sí | Medio | Bulk profesores OK | Assign manual legacy |
| InstitutionalCredential | Parcial | Sí | Medio | SuperAdmin + tokens | Staff sin `school_id` en tabla |
| StaffDirectory | Parcial | Parcial | Medio | Perfiles institucionales | Depende de user.school_id |
| StaffInstitutionalProfile | Sí | Sí | Bajo | Self-only | |
| Payments | Sí | Sí | Bajo | `PaymentController` | |
| Dashboard Director | Parcial | Sí (EF) | Bajo | `DirectorService` | Sin filtro explícito en controlador |
| User Management | Parcial | Sí (EF) | Medio–Alto | `UserController` | GetUserJson crítico |
| Reports (PDF/Excel) | Parcial | Depende módulo | Medio | Varios servicios | Mayoría scoped |

---

## 14. Matriz base de datos

| Tabla | Tiene school_id | Relación indirecta | Riesgo multitenant | Observaciones |
|-------|-----------------|--------------------|--------------------|---------------|
| users | Sí | — | Bajo | EF filter; email único global |
| schools | No (raíz) | — | Bajo | |
| students | Sí | users | Bajo | EF filter |
| groups | Sí | — | Bajo | EF filter |
| grade_levels | Sí | — | Bajo | EF filter |
| subjects | Sí | — | Bajo | EF filter |
| subject_assignments | `"SchoolId"` | — | Medio | EF filter; inconsistencia nombre columna |
| student_assignments | No | users | Medio | EF vía student; sin columna propia |
| student_subject_assignments | Sí | SA + student | Bajo | EF filter |
| teacher_assignments | No | SA | Medio | EF vía SA |
| activities | Sí | — | Bajo | EF filter |
| attendance | Sí | — | Bajo | EF filter |
| student_activity_scores | Sí | — | Bajo | EF filter |
| academic_years | Sí | — | Bajo | EF filter |
| trimester | Sí | — | Bajo | EF filter |
| payments | Sí | — | Bajo | EF filter |
| payment_concepts | Sí | — | **Alto** | Sin EF filter |
| prematriculations | Sí | — | Bajo | EF filter |
| student_id_cards | No | user | Medio | |
| institutional_credential_cards | No | staff | Medio | |
| staff_institutional_profiles | No | user | Medio | |
| staff_qr_tokens | No | staff | Medio | |
| audit_logs | Sí | — | Bajo | EF filter |
| email_configurations | Sí | — | **Alto** | Sin EF filter; credenciales |
| messages | Sí | — | Medio | Sin EF filter |
| subject_promotion_records | Sí | — | Bajo | EF filter; 0 en prod |
| area | Sí | — | Bajo | Global compartido |
| reportes (app) | N/A | servicios | Medio | Sin vistas SQL |

---

## 15. Matriz educación nocturna multi-tenant

| Funcionalidad nocturna | Soporte actual | Riesgo multitenant | Observaciones |
|------------------------|----------------|--------------------|---------------|
| Matrícula nocturna | **Sí** | Bajo | Default `Nocturno`; 576 filas prod |
| Materias pendientes | **Sí** | Bajo | SSA tipo Refuerzo/Libre |
| Arrastre | **Sí** | Bajo | Auto en bulk + `EnrollmentTypeConstants` |
| Multi-grupo | **Sí** | Bajo | Múltiples SA activas |
| Multi-nivel | **Sí** | Medio | Grupos deben existir por escuela |
| TeacherGradebook | **Sí** funcional | **Alto** | Riesgo IDOR TeacherId, no cross-school directo |
| Attendance | **Sí** | Medio | Mismo patrón gradebook |
| AprobadosReprobados | **Sí** | Bajo | Filtrado por escuela |
| StudentReport | **Sí** | Medio | Boletín nocturno en servicio |
| Portal padres | **Sí** | Bajo | |
| Promoción parcial | **Código sí; prod no** | Bajo | Tabla vacía |
| Carga masiva estudiantes | **Sí** | Bajo | Post `edd9fb5` |
| Flag `EnableForAllSchools` | **ON** | Bajo | Todas las escuelas nocturnas avanzadas |

**Escenario Escuela Nocturna A vs B:** con filtros EF activos y usuarios con `school_id` correcto, **no deberían mezclarse** matrículas, grupos ni reportes. **No validado en prod** (solo 1 escuela). Riesgo residual en endpoints **Alto** y catálogos sin UNIQUE por escuela.

---

## 16. Riesgos críticos (clasificación)

### Riesgo alto — ver/modificar datos otra escuela o secretos

1. `EmailConfigurationService.GetAllAsync` / `GetByIdAsync` — credenciales SMTP cross-tenant.
2. `SubjectAssignmentController.SaveAssignmentsSingle` — imparticiones sin `SchoolId`.
3. `TeacherGradebookController.GuardarNotasTemp` — suplantación de docente dentro del tenant.
4. `PaymentConceptService` / `PrematriculationPeriodService` — IDOR por GUID.
5. `UserController.GetUserJson` — exposición de hash de contraseña.

### Riesgo medio — mezcla en listados, combos, IDOR intra-escuela

1. CRUD Attendance/Activity/AcademicCatalog por id sin helper explícito.
2. Tablas puente sin `school_id` (`student_assignments`, `teacher_assignments`).
3. `StudentReportController` sin restricción de rol.
4. Staff/carnets sin columna tenant directa.
5. Email global único impide mismo email en dos escuelas.
6. Catálogos sin UNIQUE `(school_id, name)` — duplicados posibles entre escuelas en migraciones manuales.

### Riesgo bajo — visual, inconsistencia menor

1. Áreas globales compartidas entre escuelas (diseño).
2. `DirectorController` sin banner de escuela.
3. SuperAdmin sin escuela no puede usar cargas masivas (mensaje claro).
4. 1 usuario sin `school_id` en prod.

---

## 17. Recomendaciones (solo documentación — no implementadas)

1. Añadir `HasQueryFilter` a: `EmailConfiguration`, `PaymentConcept`, `PrematriculationPeriod`, `Message`, `SchoolScheduleConfiguration`, `StaffInstitutionalProfile`, `StudentIdCard`, `InstitutionalCredentialCard`.
2. Corregir `SaveAssignmentsSingle` y `TeacherAssignmentService.GetOrCreateSubjectAssignment` para siempre setear `SchoolId`.
3. En gradebook, **ignorar `TeacherId` del cliente** y usar `GetTeacherId()` del claim.
4. Añadir `[Authorize(Roles = "student,estudiante")]` a `StudentReportController`.
5. En `EmailConfigurationController.Details/Edit`, validar `configuration.SchoolId == currentUser.SchoolId`.
6. Eliminar `PasswordHash` de respuestas JSON admin.
7. Añadir UNIQUE compuestos en BD: `(school_id, name)` para grupos, grados, materias donde aplique.
8. Considerar UNIQUE `(school_id, email)` o emails con dominio por escuela (decisión de producto).
9. **Prueba de aceptación multi-tenant:** dos escuelas en staging, admin A no debe ver COUNT de usuarios de B en ningún módulo.
10. Columna `school_id` en `student_assignments` y `teacher_assignments` (defensa en profundidad).

---

## 18. Cambios mínimos requeridos para SaaS multi-escuela

| Prioridad | Cambio | Impacto |
|-----------|--------|---------|
| P0 | EF filter + ownership en `EmailConfiguration`, `PaymentConcept` | Cierra fuga credenciales/datos |
| P0 | Fix `SaveAssignmentsSingle` + SA sin SchoolId | Imparticiones válidas por escuela |
| P0 | Gradebook: TeacherId solo desde sesión | Cierra IDOR docente |
| P1 | `[Authorize]` roles en controladores expuestos | Reduce superficie |
| P1 | Validación `SchoolId` en Details/Edit IDOR | Patrón reutilizable |
| P1 | Prueba E2E dos escuelas nocturnas | Validación real |
| P2 | UNIQUE compuestos catálogo | Integridad multi-escuela |
| P2 | `school_id` en tablas puente | Auditoría y queries directas |

Estimación esfuerzo mínimo viable: **3–5 días** desarrollo + **2 días** QA multi-escuela antes de comercializar SaaS.

---

## 19. Conclusión final

SchoolManager **no está listo al 100 %** como SaaS multi-escuela, pero **supera el umbral de “single-tenant con columnas tenant”**: tiene infraestructura EF + middleware + auth reciente y módulos clave (matrícula nocturna, pagos, promoción, cargas masivas) alineados al operador por escuela.

**Bloqueadores SaaS:** servicios sin EF filter con datos sensibles, endpoints legacy (`SaveAssignmentsSingle`, gradebook), falta de prueba con ≥2 escuelas, restricción email global.

**Educación nocturna multi-escuela:** funcionalmente **preparada**; multitenant **parcialmente preparada** con los mismos gaps.

---

# RESPUESTA FINAL OBLIGATORIA

### 1. ¿SchoolManager está preparado hoy para multi-tenant?

**PARCIAL**

### 2. ¿SchoolManager está preparado hoy para múltiples escuelas nocturnas?

**PARCIAL**

### 3. Porcentaje estimado de compatibilidad multi-tenant

**~78 %**

### 4. Porcentaje estimado de compatibilidad educación nocturna multi-tenant

**~82 %**

### 5. ¿Puede una escuela ver datos de otra?

**NO** en operación normal con usuario admin/docente/estudiante de una escuela y filtros EF activos (commit `e696596`+).  
**SÍ** vía SuperAdmin (diseño), servicios sin filter (`EmailConfigurationService.GetAllAsync`), o explotando endpoints IDOR si se conocen GUIDs de otra escuela (riesgo teórico; **no verificado en prod** con 2 escuelas).

### 6. ¿Qué módulos son seguros?

StudentProfile, ParentAcademic, ClubParents (pagos), SubjectPromotion, Payment (portal y admin), AprobadosReprobados, CounselorAssignment, StaffInstitutionalProfile (self), AcademicCatalog/AcademicAssignment **carga masiva reciente**, StudentAssignment bulk reciente, Schedule (validación explícita), SuperAdmin (controlado).

### 7. ¿Qué módulos tienen riesgo alto?

TeacherGradebook (notas/asistencia), SubjectAssignment (`SaveAssignmentsSingle`), EmailConfiguration (servicio + Details IDOR), UserManagement (`GetUserJson`), PaymentConcept / PrematriculationPeriod (servicios).

### 8. ¿Qué módulos tienen riesgo medio?

StudentAssignment (AJAX legacy), TeacherAssignment, Attendance, Activities, StudentReport, AcademicCatalog (edición por id), Director Dashboard, InstitutionalCredential/StaffDirectory, Messaging, carnets PDF.

### 9. ¿Qué módulos necesitan corrección antes de vender como SaaS multi-escuela?

EmailConfiguration, PaymentConcept, TeacherGradebook, SubjectAssignment upload single, UserController JSON admin, PrematriculationPeriod; más **prueba formal con 2+ escuelas nocturnas**.

### 10. ¿Qué cambios mínimos se requieren para estar listo?

Ver §18 (P0: EF filter credenciales/conceptos, fix SA sin SchoolId, gradebook TeacherId desde sesión, QA dos escuelas).

---

*Documento generado por auditoría de solo lectura. Código analizado: `main` @ `edd9fb5`. Base de datos: Render `schoolmanager_daqf` (SELECT only).*
