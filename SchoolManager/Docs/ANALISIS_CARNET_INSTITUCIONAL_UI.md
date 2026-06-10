# Análisis: carnet institucional — `InstitutionalCredential/ui/generate/`

**Fecha:** 2026-06-08  
**Alcance:** Presentación visual (HTML preview + PDF nativo + captura HTML). Sin cambios de BD ni lógica de negocio/QR.

---

## 1. Archivos del flujo

| Capa | Archivo | Función |
|------|---------|---------|
| Ruta | `Controllers/InstitutionalCredentialController.cs` | `GenerateView`, `Print`, `GenerateApi` |
| ViewModel | `ViewModels/InstitutionalCredentialGenerateViewModel.cs` | Datos escuela + flags de plantilla |
| DTO API | `Dtos/InstitutionalCredentialCardDto.cs` | Carnet activo (nombre, cargo, QR, etc.) |
| DTO render | `Dtos/StaffCardRenderDto.cs` | Datos para Skia/PDF nativo |
| Vista | `Views/InstitutionalCredential/Generate.cshtml` | Preview HTML + descarga PDF |
| CSS | Inline en `Generate.cshtml` | Estilos preview (CR80 ~210×334 px) |
| Imagen | `Services/Implementations/InstitutionalCredentialImageService.cs` | PNG frente/reverso (Skia) |
| PDF nativo | `Services/Implementations/InstitutionalCredentialPdfService.cs` | QuestPDF 1–2 caras |
| PDF HTML | `Services/Implementations/InstitutionalCredentialHtmlCaptureService.cs` | Puppeteer captura `#idCardFront` + reverso |
| Tamaño físico | `Services/IdCardPhysicalDimensions.cs` | CR80 vertical: 53.98 × 85.60 mm |

---

## 2. Variables dinámicas (no fijas)

### Desde escuela (`School` + `SchoolIdCardSetting`)

| Variable VM | Origen | Uso actual |
|-------------|--------|------------|
| `SchoolName` | `school.Name` | Encabezado frente/reverso |
| `SchoolLogoUrl` | `school.LogoUrl` | Logo + watermark |
| `SchoolPhone` | `school.Phone` | **Reverso** (eliminar) |
| `IdCardPolicy` | `school.IdCardPolicy` | PDF nativo reverso (eliminar) |
| `PrimaryColor`, `BackgroundColor`, `TextColor` | `SchoolIdCardSetting` | Tema visual |
| `ShowQr`, `ShowPhoto`, `ShowDocumentId`, `ShowWatermark`, `ShowSchoolPhone` | Settings | Visibilidad por escuela |

### Desde colaborador (`InstitutionalCredentialCardDto` / `StaffCardRenderDto`)

| Campo | Origen BD | Mostrado hoy | Acción |
|-------|-----------|--------------|--------|
| `FullName` | `users.name` + `last_name` | Frente | **Mantener** |
| `JobTitle` | `staff_institutional_profiles.job_title` | Frente | **Mantener** |
| `Department` | `staff_institutional_profiles.department` | Frente “Área” | **Ocultar** |
| `RoleDisplay` | `users.role` formateado | Frente “Rol” | **Ocultar** |
| `CardNumber` | `institutional_credential_cards` | Frente + reverso | **Ocultar UI** (sigue en BD/DTO) |
| `DocumentId` | `users.document_id` | Frente si `ShowDocumentId` | **Mantener** (opcional por escuela) |
| `EmployeeCode` | perfil institucional | Solo PDF nativo | **Ocultar** |
| `PhotoUrl` | `users.photo_url` | Frente | **Mantener** |
| `QrImageDataUrl` / `QrToken` | `staff_qr_tokens` + firma | Reverso HTML; frente PDF nativo | **Mover al frente** |

---

## 3. Elementos eliminados (solo visual)

1. **Rol** — línea `Rol: …`
2. **Área** — línea `Área: …`
3. **Código credencial** — `IC-…` en frente (y todo texto “Credencial:” del reverso)
4. **Reverso completo** — escuela, teléfono, política, QR reverso, pie “Personal autorizado”
5. **Teléfono / política** — no se mueven al frente

**Se mantiene en backend:** generación de `CardNumber`, tokens QR, roles, departamentos.

---

## 4. Nueva distribución propuesta (una sola cara)

```
┌──────────────────────────────────┐  ← banda PrimaryColor
│         [Logo escuela]           │
│    NOMBRE INSTITUCIÓN (auto)      │
├──────────────────────────────────┤
│         ┌────────────┐           │
│         │    FOTO    │           │  ShowPhoto / placeholder FOTO
│         └────────────┘           │
│      NOMBRE PERSONA (auto)       │
│   Cédula: … (si ShowDocumentId)  │
│      Cargo: … (auto truncado)    │
│                                  │
│         ┌──────────┐             │  ShowQr
│         │    QR    │             │
│         └──────────┘             │
├──────────────────────────────────┤  ← banda PrimaryColor (opcional)
│    Credencial institucional      │
└──────────────────────────────────┘
     CR80 vertical 53.98 × 85.60 mm
```

### Comportamiento dinámico

| Caso | Tratamiento |
|------|-------------|
| Nombre largo | `AutoText` (PDF) reduce fuente / elipsis; CSS `line-clamp` + `word-break` (HTML) |
| Cargo largo | Igual |
| Escuela larga | Fuente reducida en header (Skia) / `line-clamp: 2` (HTML) |
| Sin foto | Placeholder “FOTO” |
| Sin QR (`ShowQr=false`) | Bloque QR oculto; más espacio para texto |
| QR sin URL/token | No renderizar imagen (sin romper layout) |

---

## 5. Impresión / PDF

| Canal | Antes | Después |
|-------|-------|---------|
| Preview HTML | 2 columnas (frente + reverso) | 1 cara |
| PDF Puppeteer | 2 páginas si hay reverso | 1 página |
| PDF nativo (Skia) | Hoja 2× ancho (frente+reverso) | Hoja CR80 simple |

Constantes físicas sin cambio: `IdCardPhysicalDimensions`.

---

## 6. Mockup conceptual (resultado final)

```
╔══════════════════════════════╗
║ ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓ ║  azul institucional
║      [LOGO]                  ║
║  INSTITUTO PROFESIONAL…      ║
╠══════════════════════════════╣
║        [  FOTO  ]            ║
║   MARÍA DEL CARMEN…          ║
║   Cargo: Coordinadora…       ║
║                              ║
║        █████████             ║
║        █ QR CODE █           ║
║        █████████             ║
╠══════════════════════════════╣
║   Credencial institucional   ║
╚══════════════════════════════╝
```

Sin: Rol, Área, IC-XXXX, reverso, teléfono.

---

## 7. Implementación aplicada

- `Views/InstitutionalCredential/Generate.cshtml` — layout una cara + QR en frente
- `InstitutionalCredentialImageService.cs` — frente rediseñado; reverso sin uso en PDF
- `InstitutionalCredentialPdfService.cs` — PDF solo frente
- `InstitutionalCredentialHtmlCaptureService.cs` — PDF captura solo frente
