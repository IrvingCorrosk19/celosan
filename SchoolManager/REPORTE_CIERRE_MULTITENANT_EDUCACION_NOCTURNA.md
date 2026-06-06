# REPORTE DE CIERRE MULTITENANT + EDUCACIÓN NOCTURNA

**Fecha:** 2026-05-25  
**Base:** `ANALISIS_MULTITENANT_EDUCACION_NOCTURNA.md`  
**Alcance:** Correcciones P0/P1 en código — **sin cambios en BD producción**  
**Compilación:** `dotnet build` → **0 errores, 0 warnings**

---

## 1. Resumen ejecutivo

Se cerraron los hallazgos **P0** (críticos) y **P1** (endurecimiento) identificados en la auditoría multi-tenant. El sistema queda con:

- **TeacherId** del gradebook tomado **solo de la sesión**, con validación de impartición docente.
- **SubjectAssignment** creadas siempre con **`SchoolId`**, área y especialidad.
- **EmailConfiguration**, **PaymentConcept** y **PrematriculationPeriod** con **filtros EF** y validación de escuela en servicios.
- **GetUserJson** sin exposición de `PasswordHash` y con control de tenant.
- **StudentReport** con roles explícitos y validación padre/acudiente.
- **Activities/Attendance** con preservación de `SchoolId` en actualizaciones.

No se ejecutaron migraciones ni consultas destructivas en producción.

---

## 2. Hallazgos corregidos

| ID | Hallazgo | Estado |
|----|----------|--------|
| P0-1 | TeacherGradebook aceptaba `TeacherId` del cliente | **Cerrado** |
| P0-2 | `SaveAssignmentsSingle` sin `SchoolId` | **Cerrado** |
| P0-2b | `TeacherAssignmentService.GetOrCreateSubjectAssignment` sin `SchoolId` | **Cerrado** |
| P0-3 | `EmailConfigurationService` cross-tenant | **Cerrado** |
| P0-4 | `PaymentConceptService` IDOR por GUID | **Cerrado** |
| P0-5 | `PrematriculationPeriodService` IDOR por GUID | **Cerrado** |
| P0-6 | `GetUserJson` expone `PasswordHash` | **Cerrado** |
| P1-7 | `StudentReportController` sin `[Authorize]` por rol | **Cerrado** |
| P1-7b | Padre/acudiente en reportes | **Cerrado** |
| P1-8 | Attendance/Activities CRUD sin preservar escuela | **Cerrado** |
| P1-9 | Staff/Carnets | **Verificado** (ya acotados; sin cambio destructivo) |

---

## 3. Archivos modificados

| Archivo |
|---------|
| `Helpers/SchoolTenantHelper.cs` |
| `Models/SchoolDbContext.Tenant.cs` |
| `Controllers/TeacherGradebookController.cs` |
| `Controllers/SubjectAssignmentController.cs` |
| `Controllers/UserController.cs` |
| `Controllers/StudentReportController.cs` |
| `Controllers/EmailConfigurationController.cs` |
| `Controllers/PrematriculationPeriodController.cs` |
| `Services/Implementations/TeacherAssignmentService.cs` |
| `Services/Implementations/EmailConfigurationService.cs` |
| `Services/Implementations/PaymentConceptService.cs` |
| `Services/Implementations/PrematriculationPeriodService.cs` |
| `Services/Implementations/AttendanceService.cs` |
| `Services/Implementations/ActivityService.cs` |

---

## 4. Cambios por módulo

### TeacherGradebook (P0)

- `GetTeacherId()` obligatorio en guardado/carga de notas, actividades, promedios y asistencia.
- `ValidateTeacherGradebookScopeAsync`: verifica impartición vía `TeacherAssignments`.
- `GuardarNotasTemp`, `GetNotasCargadas`, `SaveScores`, `CreateActivity`, `UpdateActivity`, `DeleteActivity`, `GradeBookJson`, `StudentsByGroupAndGrade`, `GetPromediosFinales` endurecidos.

### SubjectAssignment (P0)

- `SaveAssignmentsSingle`: exige `schoolId` del operador; crea SA con `SchoolId`, `AreaId`, `SpecialtyId`, `GradeLevelId`, auditoría.
- Duplicados validados por escuela.

### TeacherAssignmentService (P0)

- Inyecta `ICurrentUserService`.
- `GetOrCreateSubjectAssignment` asigna `SchoolId` y auditoría vía `AuditHelper`.

### EmailConfiguration (P0)

- `HasQueryFilter` en EF por `SchoolId`.
- Servicio filtra `GetAllAsync`; `GetById`/`Update`/`Delete`/`TestConnection` validan tenant.
- Controller: POST Create valida `model.SchoolId == currentUser.SchoolId`.

### PaymentConcept (P0)

- `HasQueryFilter` en EF.
- `GetByIdAsync`, `UpdateAsync`, `DeleteAsync` validan `CanAccessResource`.

### PrematriculationPeriod (P0)

- `HasQueryFilter` en EF.
- Servicio y controller validan escuela en Get/Update/Delete.

### UserController (P0)

- `GetUserJson`: eliminado `PasswordHash`; `Forbid` si usuario de otra escuela (SuperAdmin bypass vía tenant).

### StudentReport (P1)

- `[Authorize(Roles = "student,estudiante,admin,director,secretaria,acudiente,parent")]`.
- `GetTrimesterData` / `ExportDisciplinePdf`: validación padre/acudiente vía prematrícula.

### Attendance / Activities (P1)

- `AttendanceService.UpdateAsync`: preserva `SchoolId` del registro existente.
- `ActivityService.UpdateAsync(Activity)`: carga entidad existente y valida escuela antes de actualizar.

### Staff / Carnets (P1)

- **Sin cambios de código** adicionales: `StudentIdCardController` ya filtra por escuela del operador; `InstitutionalCredential` es SuperAdmin; `StaffInstitutionalProfile` es self-only; QR público intacto.

---

## 5. Migraciones

**Ninguna.** Los `HasQueryFilter` agregados son configuración EF en código, no alteran esquema PostgreSQL.

---

## 6. Riesgos cerrados

- IDOR horizontal docente en gradebook.
- Imparticiones huérfanas (`SchoolId` null).
- Fuga SMTP cross-tenant en EmailConfiguration.
- IDOR PaymentConcept / PrematriculationPeriod.
- Exposición de hash de contraseña en JSON admin.
- Reportes accesibles sin rol explícito.
- Sobrescritura de `SchoolId` en edición Attendance/Activity.

---

## 7. Riesgos pendientes (post-cierre)

| Riesgo | Nivel | Notas |
|--------|-------|-------|
| Email único global (`users_email_key`) | Medio | Decisión producto multi-escuela |
| Tablas puente sin `school_id` (`student_assignments`, `teacher_assignments`) | Medio | EF filtra indirectamente |
| `DirectorService` agregados sin `SchoolId` explícito | Bajo | Confía en servicios filtrados |
| Prueba E2E con ≥2 escuelas nocturnas | **Pendiente UAT** | Usuario realizará en prod/staging |
| UNIQUE compuesto `(school_id, name)` en catálogos | Bajo | Mejora integridad futura |
| Entidades sin EF filter (`messages`, `staff_institutional_profiles`, etc.) | Medio | Fuera de alcance P0/P1 |

---

## 8. Resultado de compilación

```text
dotnet build SchoolManager.csproj
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## 9. Confirmación: datos de producción

- **No** se ejecutaron INSERT, UPDATE, DELETE ni ALTER en producción.
- **No** se ejecutaron pruebas funcionales contra producción.
- Solo cambios en código fuente local.

---

## 10. Recomendaciones para pruebas manuales (usuario)

### Multi-tenant

1. Admin escuela A: abrir EmailConfiguration Details con ID de escuela B → debe **404/Forbid**.
2. Admin escuela A: editar PaymentConcept de escuela B → **Unauthorized**.
3. Admin escuela A: `GetUserJson` de usuario escuela B → **403**.
4. SuperAdmin: debe seguir viendo todas las escuelas donde aplique bypass.

### Gradebook (docente)

5. Docente A: guardar notas en su materia/grupo → OK.
6. Manipular `TeacherId` en DevTools → debe **403** (Forbid).
7. Intentar materia/grupo no asignado → **403**.

### SubjectAssignment

8. Carga `SaveAssignmentsSingle`: verificar en BD que nuevas SA tienen `"SchoolId"` poblado.

### Educación nocturna

9. Carga masiva estudiantes con tipos Nocturno/Refuerzo → sin regresión.
10. Boletín estudiante y portal padre → acceso solo a datos propios/vinculados.

### Carnets / credenciales

11. QR público staff sigue abriendo perfil.
12. Admin genera carnet solo de estudiantes de su escuela.

---

*Fin del reporte de cierre P0/P1.*
