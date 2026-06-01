# Validación: `/StaffInstitutionalProfile/Index`

**Proyecto:** `C:\Proyectos\EduplanerNoche\SchoolManager`  
**Referencia:** `C:\Proyectos\EduplanerIIC\SchoolManager` (solo lectura)  
**Fecha:** 25 de mayo de 2026

---

## Resumen ejecutivo

| Estado global | Detalle |
|---------------|---------|
| **Estado inicial** | **Existe parcialmente — incompleto y oculto** |
| **Estado final (tras Fase 4)** | **Completo y accesible desde menú** |

El backend (controlador, servicio, ViewModel, helpers, tabla BD) ya existía. Faltaban la vista Razor y los enlaces de menú. QR, vista pública e impresión del carnet viven en el módulo `InstitutionalCredential`, ya implementado previamente en este proyecto.

---

## Checklist de validación

| # | Componente | Estado inicial | Estado final |
|---|------------|----------------|--------------|
| 1 | `StaffInstitutionalProfileController` | ✅ Existe | ✅ Existe |
| 2 | Ruta `/StaffInstitutionalProfile/Index` | ⚠️ Controlador sin vista | ✅ Ruta + vista |
| 3 | `Views/StaffInstitutionalProfile/` | ❌ No existía | ✅ `Index.cshtml` |
| 4 | Servicios relacionados | ✅ `StaffInstitutionalProfileService` registrado en `Program.cs` | ✅ |
| 5 | ViewModels relacionados | ✅ `StaffInstitutionalProfileViewModel` | ✅ |
| 6 | Menús | ❌ Sin acceso en `_AdminLayout` ni `_SuperAdminLayout` | ✅ Añadidos |
| 7 | Endpoints asociados | ✅ Index, Update, UpdatePhoto, RemovePhoto, CheckEmail/Document | ✅ |
| 8 | Generación de QR | ⚠️ No en este módulo | ✅ Vía `InstitutionalCredential` (`/api/qr-preview`, carnet) |
| 9 | Visualización pública del perfil | ⚠️ No en este módulo | ✅ Vía `/InstitutionalCredential/member/{token}` |
| 10 | Impresión del carnet | ⚠️ No en este módulo | ✅ Vía `/InstitutionalCredential/ui/print/{userId}` |

---

## Detalle por capa (proyecto actual)

### Controlador

- **Archivo:** `Controllers/StaffInstitutionalProfileController.cs`
- **Ruta base:** `[Route("StaffInstitutionalProfile")]`
- **Autorización:** `[Authorize(Roles = StaffInstitutionalProfileAccess.AuthorizeRoles)]`
- **Acciones:**
  - `GET /StaffInstitutionalProfile` y `/StaffInstitutionalProfile/Index`
  - `POST /StaffInstitutionalProfile/Update`
  - `POST /StaffInstitutionalProfile/UpdatePhoto`
  - `POST /StaffInstitutionalProfile/RemovePhoto`
  - `GET /StaffInstitutionalProfile/CheckEmailAvailability`
  - `GET /StaffInstitutionalProfile/CheckDocumentAvailability`

### Servicios e interfaces

| Archivo | Registro DI |
|---------|-------------|
| `Services/Interfaces/IStaffInstitutionalProfileService.cs` | Sí |
| `Services/Implementations/StaffInstitutionalProfileService.cs` | `Program.cs` línea ~276 |
| `IUserPhotoService` (foto) | Ya registrado |

### ViewModels y helpers

| Archivo | Propósito |
|---------|-----------|
| `ViewModels/StaffInstitutionalProfileViewModel.cs` | Formulario de autogestión |
| `Helpers/StaffInstitutionalProfileAccess.cs` | Roles permitidos y flag `CanOpenInstitutionalCredentialUi` |
| `Helpers/StaffInstitutionalRoleFilter.cs` | Filtro de personal institucional |

### Modelo y base de datos

| Elemento | Estado |
|----------|--------|
| `Models/StaffInstitutionalProfile.cs` | ✅ |
| Tabla `staff_institutional_profiles` | ✅ Existe en Render |
| Migración `20260601232556_AddInstitutionalStaffCredentialTables` | ✅ Aplicada |
| Filas en `staff_institutional_profiles` | 0 (se crean bajo demanda con `EnsureStaffProfileRowAsync`) |
| Usuarios elegibles (roles staff) | 18 |

### Vistas

| Vista | Estado inicial | Estado final |
|-------|----------------|--------------|
| `Views/StaffInstitutionalProfile/Index.cshtml` | ❌ | ✅ Copiada desde referencia IIC |

### Menús

| Layout | Estado inicial | Estado final |
|--------|----------------|--------------|
| `_AdminLayout.cshtml` | ❌ Sin ítem | ✅ "Mi perfil institucional" (`showStaffInstitutionalProfileMenu`) |
| `_SuperAdminLayout.cshtml` | ❌ Sin ítem | ✅ Enlace `/StaffInstitutionalProfile` |

### Módulos relacionados (QR / carnet / público)

Ya presentes en el proyecto actual:

| Funcionalidad | Módulo | Rutas clave |
|---------------|--------|-------------|
| Emisión credencial | `InstitutionalCredentialController` | `/InstitutionalCredential/ui`, `/ui/generate/{userId}` |
| Impresión PDF | `InstitutionalCredentialController` | `/InstitutionalCredential/ui/print/{userId}` |
| Vista pública QR | `InstitutionalCredentialController` | `/InstitutionalCredential/member/{token}` |
| Directorio personal | `SuperAdminController` | `/SuperAdmin/StaffDirectory` |

---

## Clasificación por criterio solicitado

- **Existe:** Controlador, servicio, ViewModel, helpers, entidad, migración, endpoints CRUD del perfil.
- **Existía parcialmente:** Ruta definida en controlador pero sin vista → error 404 al navegar.
- **Estaba incompleto:** Vista y menús faltantes.
- **Estaba oculto:** Sin enlaces en layouts de navegación.
- **Estaba sin acceso desde menú:** Confirmado en `_AdminLayout` y `_SuperAdminLayout` antes de la corrección.

---

## Conclusión Fase 1

La funcionalidad **no requería replicación total del stack backend**; requería completar la capa de presentación y navegación para igualar la experiencia del proyecto referencia IIC. Los flujos de QR, vista pública e impresión ya estaban cubiertos por `InstitutionalCredential`.
