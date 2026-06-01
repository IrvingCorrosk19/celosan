# Análisis de referencia: `/StaffInstitutionalProfile/Index`

**Proyecto referencia (solo lectura):** `C:\Proyectos\EduplanerIIC\SchoolManager`  
**Fecha:** 25 de mayo de 2026

---

## 1. Propósito del módulo

Autogestión del **perfil institucional del personal** (docentes, directores, secretaría, etc.): datos personales, contacto, cargo/departamento/código empleado y fotografía. Alimenta la credencial institucional emitida desde `InstitutionalCredential`.

**No incluye:** generación de QR, impresión PDF ni página pública de verificación (eso es responsabilidad de `InstitutionalCredential`).

---

## 2. Controlador

**Archivo:** `Controllers/StaffInstitutionalProfileController.cs`

| Atributo | Valor |
|----------|-------|
| Ruta | `[Route("StaffInstitutionalProfile")]` |
| Auth | `[Authorize(Roles = StaffInstitutionalProfileAccess.AuthorizeRoles)]` |

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `""`, `Index` | Carga perfil del usuario autenticado |
| POST | `Update` | Guarda datos editables + cargo/depto/código |
| POST | `UpdatePhoto` | Sube foto (máx. 12 MB, JPEG/PNG) |
| POST | `RemovePhoto` | Elimina foto |
| GET | `CheckEmailAvailability` | AJAX unicidad email |
| GET | `CheckDocumentAvailability` | AJAX unicidad documento |

**Dependencias inyectadas:** `IStaffInstitutionalProfileService`, `IUserPhotoService`, `ICurrentUserService`, `ILogger`.

---

## 3. Servicios e interfaces

| Capa | Archivo |
|------|---------|
| Interfaz | `Services/Interfaces/IStaffInstitutionalProfileService.cs` |
| Implementación | `Services/Implementations/StaffInstitutionalProfileService.cs` |

**Métodos principales:**

- `GetProfileAsync(Guid userId)` — Lee `users` + `staff_institutional_profiles` + escuela.
- `UpdateProfileAsync(StaffInstitutionalProfileViewModel, Guid actorId)` — Actualiza `users` y fila extendida.
- `IsEmailAvailableAsync` / `IsDocumentIdAvailableAsync` — Validación de unicidad.
- `EnsureStaffProfileRowAsync` — Crea fila en `staff_institutional_profiles` si no existe.

**Registro DI:** `Program.cs` → `AddScoped<IStaffInstitutionalProfileService, StaffInstitutionalProfileService>()`.

---

## 4. Repositorios

No hay repositorio dedicado. Acceso directo vía `SchoolDbContext` en el servicio (patrón del proyecto).

---

## 5. ViewModels y DTOs

| Tipo | Archivo | Notas |
|------|---------|-------|
| ViewModel | `ViewModels/StaffInstitutionalProfileViewModel.cs` | DataAnnotations, flags `HasSchoolAssigned`, `CanOpenInstitutionalCredentialUi` |
| Entidad EF | `Models/StaffInstitutionalProfile.cs` | Tabla `staff_institutional_profiles` |
| DTOs dedicados | — | No existen; el ViewModel actúa como contrato de vista |

---

## 6. Helpers

| Archivo | Función |
|---------|---------|
| `Helpers/StaffInstitutionalProfileAccess.cs` | Roles permitidos, `AuthorizeRoles`, `CanOpenInstitutionalCredentialUi` (solo superadmin) |
| `Helpers/StaffInstitutionalRoleFilter.cs` | Query filter personal institucional, formato de rol |

---

## 7. Razor Views

**Directorio:** `Views/StaffInstitutionalProfile/`

| Vista | Layout | Contenido |
|-------|--------|-----------|
| `Index.cshtml` | `_AdminLayout` | Formulario perfil, panel foto, datos institucionales, botón credencial (solo superadmin) |

**Estilos:** CSS inline en la vista (gradiente teal `#0f766e`, tarjetas, secciones info).

**JavaScript en vista:**

- Validación AJAX email/documento contra endpoints del controlador.
- Preview de foto antes de subir.
- Sin librerías externas adicionales al stack AdminLTE/Bootstrap del layout.

---

## 8. CSS y assets

- Estilos embebidos en `Index.cshtml` (no archivo `.css` separado).
- Iconos: Bootstrap Icons (`bi-*`) y Font Awesome del layout.

---

## 9. Endpoints del módulo

```
GET  /StaffInstitutionalProfile
GET  /StaffInstitutionalProfile/Index
POST /StaffInstitutionalProfile/Update
POST /StaffInstitutionalProfile/UpdatePhoto
POST /StaffInstitutionalProfile/RemovePhoto
GET  /StaffInstitutionalProfile/CheckEmailAvailability?email=&userId=
GET  /StaffInstitutionalProfile/CheckDocumentAvailability?documentId=&userId=
```

---

## 10. Flujo de navegación

```mermaid
flowchart TD
    A[Usuario autenticado staff] --> B{Menú}
    B -->|Admin layout| C[/StaffInstitutionalProfile/Index]
    B -->|SuperAdmin layout| C
    C --> D[Editar datos + foto]
    D --> E[POST Update / UpdatePhoto]
    E --> C
    C -->|CanOpenInstitutionalCredentialUi| F[/InstitutionalCredential/ui/generate/userId]
    F --> G[/InstitutionalCredential/ui/print/userId]
```

**Menús en referencia:**

- `_AdminLayout.cshtml`: variable `showStaffInstitutionalProfileMenu` → enlace "Mi perfil institucional".
- `_SuperAdminLayout.cshtml`: enlace `/StaffInstitutionalProfile` bajo sección CREDENCIALES.

---

## 11. Flujo QR (módulo relacionado)

No implementado en `StaffInstitutionalProfile`. Cadena en `InstitutionalCredential`:

1. SuperAdmin genera credencial desde `/InstitutionalCredential/ui/generate/{userId}`.
2. `InstitutionalCredentialService` crea/actualiza `institutional_credential_cards` y `staff_qr_tokens`.
3. QR apunta a `/InstitutionalCredential/member/{token}` (vista pública).
4. Preview QR: `GET /InstitutionalCredential/api/qr-preview/{userId}`.

**Servicios involucrados:** `IInstitutionalCredentialService`, `IInstitutionalCredentialPdfService`, `IInstitutionalCredentialHtmlCaptureService`, `IInstitutionalCredentialImageService`.

---

## 12. Flujo de impresión (módulo relacionado)

1. Desde perfil (solo superadmin): botón en `Index.cshtml` → `/InstitutionalCredential/ui/generate/{userId}`.
2. Desde `StaffDirectory`: acciones en `SuperAdminController`.
3. Impresión: `GET /InstitutionalCredential/ui/print/{userId}` → PDF vía captura HTML o servicio PDF nativo.

---

## 13. Tablas de base de datos

| Tabla | Uso |
|-------|-----|
| `users` | Datos personales y `photo_url` |
| `staff_institutional_profiles` | Cargo, departamento, código empleado |
| `institutional_credential_cards` | Carnet emitido |
| `staff_qr_tokens` | Token QR público |
| `schools` | Nombre de escuela en perfil |

**Migración referencia:** `20260514104759_AddInstitutionalStaffCredentialTables`.

---

## 14. Integración con SuperAdmin

- `SuperAdmin/StaffDirectory`: listado de personal con enlace a generación de credencial.
- `SuperAdminService`: consultas filtradas por `StaffInstitutionalProfileAccess.StaffDirectoryAllowlist`.

---

## 15. Roles autorizados

`superadmin`, `admin`, `director`, `teacher`, `docente`, `secretaria`, `inspector`, `contable`, `contabilidad`.

Excluidos explícitamente: estudiantes, padres/acudientes, club de padres.
