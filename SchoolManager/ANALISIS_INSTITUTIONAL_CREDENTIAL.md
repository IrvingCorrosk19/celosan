# Análisis — Credencial institucional del personal (proyecto fuente)

**Proyecto de referencia (solo lectura):** `C:\Proyectos\EduplanerIIC\SchoolManager`  
**Fecha:** 2026-06-01  
**Alcance:** `/InstitutionalCredential/ui` y `/SuperAdmin/StaffDirectory`

---

## 1. Arquitectura encontrada

Capas MVC clásicas con servicios de dominio, render PDF dual (Puppeteer + SkiaSharp/QuestPDF) y QR firmado HMAC.

```
SuperAdmin (StaffDirectory)          InstitutionalCredential (UI + APIs)
        |                                        |
        v                                        v
 IUserPhotoService                    IInstitutionalCredentialService
 StaffInstitutionalProfile (BD)      IInstitutionalCredentialPdfService
        |                              IInstitutionalCredentialImageService
        +------------------------------+ IInstitutionalCredentialHtmlCaptureService
                                       IQrSignatureService + QrHelper
                                       SchoolIdCardSetting (branding compartido)
```

### Tablas nuevas (migración `20260514104759`)

| Tabla | Propósito |
|-------|-----------|
| `staff_institutional_profiles` | Cargo, departamento, código empleado (PK = `user_id`) |
| `institutional_credential_cards` | Carnet emitido, número, vigencia, impresión |
| `staff_qr_tokens` | Token QR por usuario, revocable |

---

## 2. Controladores

### `InstitutionalCredentialController`
- **Ruta base:** `[Route("InstitutionalCredential")]`
- **Auth:** `[Authorize(Roles = "SuperAdmin,superadmin")]` en el controlador
- **Público:** `[AllowAnonymous]` + rate limit `ScanApiPolicy` en rutas `member`

| Método | Ruta | Función |
|--------|------|---------|
| GET | `/ui` | Listado DataTables |
| GET | `/ui/generate/{userId}` | Vista previa HTML (frente/reverso) |
| GET | `/ui/print/{userId}` | PDF + marca `IsPrinted` |
| POST | `/api/generate/{userId}` | Emite carnet + QR |
| GET | `/api/list-json` | Feed paginado |
| GET | `/api/list-filters` | Escuelas y roles |
| GET | `/api/qr-preview/{userId}` | PNG QR + URL pública |
| GET | `/member?t={signed}` | Perfil público (QR firmado) |
| GET | `/member/{token}` | Perfil público (token crudo) |

### `SuperAdminController` (acciones StaffDirectory)
- **Auth:** `[Authorize(Roles = "superadmin")]`
- GET `/SuperAdmin/StaffDirectory`
- POST `/SuperAdmin/StaffDirectoryUpdatePhoto`
- POST `/SuperAdmin/StaffDirectoryRemovePhoto`
- POST `/SuperAdmin/StaffDirectorySaveProfile`

### Adjunto: `StaffInstitutionalProfileController`
- Autoservicio del personal (`/StaffInstitutionalProfile`) — roles amplios, no SuperAdmin.

---

## 3. Servicios

| Interfaz | Implementación | Responsabilidad |
|----------|----------------|-----------------|
| `IInstitutionalCredentialService` | `InstitutionalCredentialService` | Emitir, revocar, resolver perfil público |
| `IInstitutionalCredentialPdfService` | `InstitutionalCredentialPdfService` | PDF nativo Skia + QuestPDF |
| `IInstitutionalCredentialImageService` | `InstitutionalCredentialImageService` | PNG frente/reverso |
| `IInstitutionalCredentialHtmlCaptureService` | `InstitutionalCredentialHtmlCaptureService` | Captura Puppeteer de `/ui/generate` |
| `IStaffInstitutionalProfileService` | `StaffInstitutionalProfileService` | Perfil laboral |
| `ISuperAdminService` | `SuperAdminService.GetStaffDirectoryPageAsync` | Paginación directorio |
| `IUserPhotoService` | `UserPhotoService` | Fotos (compartido) |
| `IQrSignatureService` | `QrSignatureService` | Firma HMAC QR |

---

## 4. ViewModels y DTOs

- `InstitutionalCredentialGenerateViewModel` — preview; usa `SchoolIdCardSetting`
- `InstitutionalCredentialCardDto` — payload API/preview
- `StaffCardRenderDto` — bytes logo/foto/watermark para Skia
- `StaffMemberPublicProfileVm` / `StaffMemberPublicInvalidVm` — páginas públicas
- `SuperAdminStaffDirectoryFilterVm`, `SuperAdminStaffDirectoryRowVm`, `SuperAdminStaffDirectoryPageVm`

---

## 5. Helpers y opciones

- `StaffInstitutionalRoleFilter` — excluye estudiantes; formatea rol
- `StaffInstitutionalProfileAccess` — allowlists directorio / UI credencial
- `StaffMemberPublicLink` — URL pública firmada
- `InstitutionalCardNumberHelper` — `IC-YYYYMMDD-XXXXXXXX-XXXXXX`
- `InstitutionalCredentialOptions.PublicBaseUrl` — base URL QR fuera de HTTP request

---

## 6. Vistas Razor

| Vista | Layout | JS |
|-------|--------|-----|
| `Views/InstitutionalCredential/Index.cshtml` | `_SuperAdminLayout` | Inline (~380 líneas), DataTables |
| `Views/InstitutionalCredential/Generate.cshtml` | `_SuperAdminLayout` | Inline, preview + print |
| `Views/InstitutionalCredential/PublicMemberProfile.cshtml` | Sin layout | Página pública |
| `Views/InstitutionalCredential/PublicMemberInvalid.cshtml` | Sin layout | QR inválido |
| `Views/SuperAdmin/StaffDirectory.cshtml` | `_SuperAdminLayout` | Inline (~400 líneas), modales foto/perfil |

**CSS:** `wwwroot/css/superadmin-staff-pages.css`

---

## 7. Flujo completo — listado → impresión

1. SuperAdmin abre `/SuperAdmin/StaffDirectory` → foto + cargo (perfil laboral).
2. Enlace a `/InstitutionalCredential/ui`.
3. DataTables carga `/api/list-json` (personal no estudiante).
4. **Generar:** POST `/api/generate/{userId}` → revoca carnets/tokens previos → crea card + QR → redirect `/ui/generate/{userId}`.
5. **Imprimir:** GET `/ui/print/{userId}` → Puppeteer captura HTML; fallback Skia PDF → `IsPrinted=true`.

**Campos en carnet (vs estudiante):** Rol + cargo (`JobTitle`) en lugar de grado/grupo.

---

## 8. Flujo completo — QR → visualización pública

1. QR contiene URL firmada: `{PublicBaseUrl}/InstitutionalCredential/member?t=...`
2. `StaffMemberPublicLink` valida HMAC o ruta `/member/{token}` consulta `staff_qr_tokens`.
3. `ResolvePublicProfileByQrTokenAsync` valida no revocado / no expirado.
4. Vista `PublicMemberProfile`: foto, nombre, cargo, rol, escuela, estado activo.
5. **No expone:** email interno, datos administrativos sensibles.

**Seguridad:** Rate limit 60 req/min (`ScanApiPolicy`), tokens revocables al re-emitir.

---

## 9. Roles involucrados

| Contexto | Roles |
|----------|-------|
| Credencial UI | `SuperAdmin`, `superadmin` |
| StaffDirectory | `superadmin` |
| Personal elegible (carnet) | Cualquier rol ≠ estudiante |
| Directorio allowlist | admin, director, teacher, secretaria, inspector, contable, contabilidad, superadmin |

---

## 10. Dependencias NuGet

QRCoder, QuestPDF, PuppeteerSharp, SkiaSharp (mismas que carnet estudiantil).

---

## 11. Diseño visual

- Réplica del layout institucional vertical del carnet estudiantil (`SchoolIdCardSetting`: colores, logo, watermark).
- Tarjeta 55×85 mm portrait (`IdCardPhysicalDimensions`).
- Listado SuperAdmin con badges de rol/jornada, thumbnails vía `/File/GetUserPhoto?variant=thumb`.
