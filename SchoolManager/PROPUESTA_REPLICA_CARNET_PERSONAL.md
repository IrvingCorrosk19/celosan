# Propuesta — Réplica carnet institucional del personal

**Proyecto destino:** `C:\Proyectos\EduplanerNoche\SchoolManager`  
**Referencia:** `C:\Proyectos\EduplanerIIC\SchoolManager` (sin modificaciones)

---

## 1. Arquitectura propuesta

Replicar el módulo IIC **tal cual**, integrado al stack existente de Eduplaner Noche:

- Reutilizar `User`, `IUserPhotoService`, `FileController.GetUserPhoto`, `SchoolIdCardSetting`, `IQrSignatureService`, `QrHelper`, paquetes PDF/QR ya instalados.
- Añadir 3 tablas + servicios + controlador + vistas copiadas del fuente.
- **No tocar** `StudentIdCardController`, flujos de pago carnet, ni módulos académicos.

---

## 2. Qué se copia (desde IIC)

| Área | Acción |
|------|--------|
| `InstitutionalCredentialController` + 4 vistas | Copia directa |
| Servicios `InstitutionalCredential*` (4) | Copia directa |
| `StaffInstitutionalProfile*` (opcional autoservicio) | Copia directa |
| Helpers, DTOs, ViewModels, Options | Copia directa |
| `StaffDirectory.cshtml` + ViewModels | Copia directa |
| `superadmin-staff-pages.css` | Copia directa |

---

## 3. Qué se reutiliza (destino)

| Componente | Uso |
|------------|-----|
| `User` | Identidad del personal |
| `IUserPhotoService` / Cloudinary | Fotos |
| `SchoolIdCardSetting` | Branding carnet |
| `StudentIdCardImageService` patterns | Layout institucional vertical |
| `Program.cs` rate limiter `ScanApiPolicy` | Ya existía |
| `IQrSignatureService` | Firma QR |
| `_SuperAdminLayout` | Menú (solo añadir enlaces) |

---

## 4. Qué se adapta

| Punto | Adaptación |
|-------|------------|
| Migración EF | Generada en destino (`20260601232556_AddInstitutionalStaffCredentialTables`) |
| `SuperAdminController` | Pegar acciones StaffDirectory |
| `SuperAdminService` | Método `GetStaffDirectoryPageAsync` |
| `SchoolDbContext` | DbSets + fluent API |
| `appsettings.json` | Sección `InstitutionalCredential:PublicBaseUrl` |
| Carnet campos | Grado/Grupo → **Rol** + **Cargo** (`JobTitle`) |

---

## 5. Rutas propuestas (implementadas)

| Ruta | Descripción |
|------|-------------|
| `/SuperAdmin/StaffDirectory` | Directorio personal |
| `/InstitutionalCredential/ui` | Gestión credenciales |
| `/InstitutionalCredential/ui/generate/{userId}` | Preview |
| `/InstitutionalCredential/ui/print/{userId}` | PDF |
| `/InstitutionalCredential/member?t=` | QR público firmado |
| `/InstitutionalCredential/member/{token}` | QR público alterno |

> Nota: el requerimiento mencionaba `/InstitutionalCredential/View/{id}`; el proyecto fuente usa `/member` por seguridad (token firmado). Se mantiene el diseño fuente.

---

## 6. Flujos

### QR
Generar → token en `staff_qr_tokens` → URL firmada → escaneo → página pública sin datos sensibles.

### Impresión
HTML capture (primario) → QuestPDF; fallback Skia nativo; marca impreso en BD.

### Directorio
Paginación server-side → filtros escuela/rol/estado/búsqueda → modales foto y perfil laboral.

---

## 7. Base de datos

**Solo adición** (aplicada en Render):

- `staff_institutional_profiles`
- `institutional_credential_cards`
- `staff_qr_tokens`

Sin borrado ni alteración destructiva de tablas existentes.

---

## 8. Riesgos mitigados

| Riesgo | Mitigación |
|--------|------------|
| Colisión con carnet estudiantil | Tablas y tokens separados |
| Exponer email en QR | Vista pública filtrada en fuente |
| Romper StudentIdCard | Cero cambios en ese módulo |
| Grupos A1-A4 vs nocturnos | Módulo independiente del catálogo académico |
