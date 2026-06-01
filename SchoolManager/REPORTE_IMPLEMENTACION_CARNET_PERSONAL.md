# Reporte de implementación — Carnet institucional del personal

**Proyecto:** `C:\Proyectos\EduplanerNoche\SchoolManager`  
**Referencia (sin cambios):** `C:\Proyectos\EduplanerIIC\SchoolManager`  
**Fecha:** 2026-06-01

---

## Resumen

Se replicó el módulo completo de **credencial institucional del personal** y **directorio SuperAdmin**, alineado al proyecto fuente IIC. Compilación exitosa y migración aplicada en PostgreSQL (Render).

---

## Compilación

```
dotnet build
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## Base de datos

Migración aplicada: **`20260601232556_AddInstitutionalStaffCredentialTables`**

Tablas creadas en producción:

- `staff_institutional_profiles`
- `institutional_credential_cards`
- `staff_qr_tokens`

Comando ejecutado: `dotnet ef database update` (sin operaciones destructivas).

---

## Archivos creados

### Controladores
- `Controllers/InstitutionalCredentialController.cs`
- `Controllers/StaffInstitutionalProfileController.cs`

### Modelos
- `Models/InstitutionalCredentialCard.cs`
- `Models/StaffQrToken.cs`
- `Models/StaffInstitutionalProfile.cs`

### Servicios
- `Services/Interfaces/IInstitutionalCredentialService.cs`
- `Services/Interfaces/IInstitutionalCredentialPdfService.cs`
- `Services/Interfaces/IInstitutionalCredentialImageService.cs`
- `Services/Interfaces/IInstitutionalCredentialHtmlCaptureService.cs`
- `Services/Interfaces/IStaffInstitutionalProfileService.cs`
- `Services/Implementations/InstitutionalCredentialService.cs`
- `Services/Implementations/InstitutionalCredentialPdfService.cs`
- `Services/Implementations/InstitutionalCredentialImageService.cs`
- `Services/Implementations/InstitutionalCredentialHtmlCaptureService.cs`
- `Services/Implementations/StaffInstitutionalProfileService.cs`

### DTOs / ViewModels / Helpers / Options
- `Dtos/InstitutionalCredentialCardDto.cs`
- `Dtos/StaffCardRenderDto.cs`
- `ViewModels/InstitutionalCredentialGenerateViewModel.cs`
- `ViewModels/StaffMemberPublicProfileVm.cs`
- `ViewModels/StaffInstitutionalProfileViewModel.cs`
- `ViewModels/SuperAdminStaffDirectoryViewModels.cs`
- `Helpers/StaffInstitutionalProfileAccess.cs`
- `Helpers/StaffInstitutionalRoleFilter.cs`
- `Helpers/StaffMemberPublicLink.cs`
- `Helpers/InstitutionalCardNumberHelper.cs`
- `Options/InstitutionalCredentialOptions.cs`

### Vistas y assets
- `Views/InstitutionalCredential/Index.cshtml`
- `Views/InstitutionalCredential/Generate.cshtml`
- `Views/InstitutionalCredential/PublicMemberProfile.cshtml`
- `Views/InstitutionalCredential/PublicMemberInvalid.cshtml`
- `Views/SuperAdmin/StaffDirectory.cshtml`
- `wwwroot/css/superadmin-staff-pages.css`

### Migraciones
- `Migrations/20260601232556_AddInstitutionalStaffCredentialTables.cs`
- `Migrations/20260601232556_AddInstitutionalStaffCredentialTables.Designer.cs`

### Documentación
- `ANALISIS_INSTITUTIONAL_CREDENTIAL.md`
- `PROPUESTA_REPLICA_CARNET_PERSONAL.md`
- `REPORTE_IMPLEMENTACION_CARNET_PERSONAL.md` (este archivo)

---

## Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `Models/SchoolDbContext.cs` | DbSets + configuración EF |
| `Program.cs` | DI servicios + `InstitutionalCredentialOptions` |
| `appsettings.json` | `InstitutionalCredential:PublicBaseUrl` |
| `Controllers/SuperAdminController.cs` | StaffDirectory + foto + perfil |
| `Services/Interfaces/ISuperAdminService.cs` | `GetStaffDirectoryPageAsync` |
| `Services/Implementations/SuperAdminService.cs` | Implementación directorio |
| `Views/Shared/_SuperAdminLayout.cshtml` | Enlaces menú |
| `Migrations/SchoolDbContextModelSnapshot.cs` | Snapshot EF |

---

## Rutas operativas

| URL | Rol | Función |
|-----|-----|---------|
| `/SuperAdmin/StaffDirectory` | superadmin | Directorio personal |
| `/InstitutionalCredential/ui` | SuperAdmin | Listado credenciales |
| `/InstitutionalCredential/ui/generate/{userId}` | SuperAdmin | Vista previa |
| `/InstitutionalCredential/ui/print/{userId}` | SuperAdmin | PDF |
| `/InstitutionalCredential/api/generate/{userId}` | SuperAdmin | Emitir/regenerar |
| `/InstitutionalCredential/member?t=...` | Público | Perfil QR |
| `/StaffInstitutionalProfile` | Personal (roles allowlist) | Autoservicio perfil |

---

## Flujo completo

```mermaid
flowchart LR
  A[StaffDirectory] --> B[Foto + cargo]
  B --> C[InstitutionalCredential/ui]
  C --> D[Generar API]
  D --> E[Preview HTML]
  E --> F[Print PDF]
  D --> G[QR token]
  G --> H[/member público]
```

**Adaptación carnet:** en lugar de Grado/Grupo se muestran **Rol** y **Cargo** (`JobTitle` del perfil institucional).

---

## Evidencia de validación

| Prueba | Resultado |
|--------|-----------|
| `dotnet build` | OK — 0 errores |
| `dotnet ef database update` | OK — 3 tablas creadas |
| Proyecto fuente IIC | No modificado |
| StudentIdCard / módulos académicos | Sin cambios de código |

**Validación manual pendiente en navegador (SuperAdmin):**

1. Iniciar sesión como `superadmin`.
2. Abrir `/SuperAdmin/StaffDirectory` — listado, filtros, subir foto.
3. Abrir `/InstitutionalCredential/ui` — generar credencial de un docente/admin.
4. Imprimir PDF (Chrome/Edge).
5. Escanear QR → página pública sin email.

> Capturas: tomar en entorno desplegado tras login SuperAdmin (no ejecutado en esta sesión por requerir credenciales interactivas).

---

## Configuración recomendada

En producción, definir URL pública para QR:

```json
"InstitutionalCredential": {
  "PublicBaseUrl": "https://tu-dominio.eduplaner.com"
}
```

Si queda vacío, se usa el host del request actual al generar el QR.

---

## Restricciones respetadas

- No se modificó `C:\Proyectos\EduplanerIIC\SchoolManager`.
- No se alteraron tablas existentes (solo CREATE).
- No se tocó lógica de `StudentIdCard`, StudentDirectory académico, gradebook ni attendance.

---

## Próximo paso sugerido

Commit + push + despliegue en Render, luego prueba E2E de impresión PDF y escaneo QR con un usuario `teacher` o `director` de la escuela nocturna.
