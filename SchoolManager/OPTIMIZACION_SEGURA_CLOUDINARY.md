# Optimización segura: Cloudinary y módulo StudentIdCard/ui

**Principio:** ningún activo en Cloudinary se elimina, sobrescribe ni se re-encodea en origen. Las URLs guardadas en base de datos **no cambian**. Las mejoras son **incrementales** (nuevas URLs de entrega, caché de lectura y uso de miniaturas en listados).

---

## 1. Cambios seguros implementados

| Área | Qué se hizo | Riesgo |
|------|-------------|--------|
| **Entrega Cloudinary (miniatura lista)** | Query opcional `variant=thumb` en `GET /File/GetUserPhoto`: redirección a la misma `public_id` con cadena de transformación `w_128,h_128,c_fill,g_auto,q_auto:eco,f_auto` insertada tras `/image/upload/`. | Cero en almacenamiento: transformación on-the-fly estándar de Cloudinary. |
| **Refactor URL carnet** | `CloudinaryCarnetDeliveryUrl` delega en `CloudinaryTransformUrl.InsertAfterUpload` (misma semántica que antes). | Solo refactor interno. |
| **Listados** | `StudentIdCard/Index` (DataTables) y `SuperAdmin/StudentDirectory` usan miniatura vía `UserPhotoLinks.HrefListThumbnail` o `&variant=thumb`. Vista previa modal en directorio sigue usando URL completa (`buildUserPhotoHref` sin variant). | Si no fuera Cloudinary, `variant` se ignora en redirect (solo aplica a host permitido). |
| **Caché backend HTTP** | `IHttpBytesDownloadCache` + `HttpBytesDownloadCache`: memoria (`IMemoryCache` con `SizeLimit`) + disco bajo `{ContentRoot}/cache/http-images` (configurable). Usado por `LocalFileStorageService.GetUserPhotoBytesAsync` (URLs https) y `StudentIdCardPdfService.SafeDownloadBytesAsync` (logos/fotos remotas). | No escribe en Cloudinary; TTL y tamaños acotados; carpeta en `.gitignore`. |
| **Cache-Control (rutas locales)** | Respuestas `File(bytes)` de fotos en disco: `Cache-Control: private, max-age=15m`. | No afecta redirects a CDN. |
| **Configuración** | Sección `UserPhotoCache` en `appsettings.json`; `AddMemoryCache` con tope de tamaño desde configuración. | Desactivable con `"Enabled": false`. |

---

## 2. Código exacto (referencia; sin romper producción)

Archivos tocados (resumen):

- `Helpers/CloudinaryTransformUrl.cs` — inserción genérica de transformación.
- `Helpers/CloudinaryCarnetDeliveryUrl.cs` — usa `CloudinaryTransformUrl`.
- `Helpers/UserPhotoDeliveryTransforms.cs` — constante `ListThumbnail`.
- `Helpers/UserPhotoLinks.cs` — `HrefListThumbnail`.
- `Controllers/FileController.cs` — parámetro `variant`, `CacheControl` en bytes locales.
- `Options/UserPhotoCacheOptions.cs` — opciones de caché.
- `Services/Interfaces/IHttpBytesDownloadCache.cs`
- `Services/Implementations/HttpBytesDownloadCache.cs`
- `Services/Implementations/LocalFileStorageService.cs` — descarga remota vía caché.
- `Services/Implementations/StudentIdCardPdfService.cs` — descarga HTTP vía caché.
- `Program.cs` — `AddMemoryCache`, registro singleton del caché HTTP.
- `Views/StudentIdCard/Index.cshtml` — `&variant=thumb` en miniatura de tabla.
- `Views/SuperAdmin/StudentDirectory.cshtml` — `HrefListThumbnail` + `buildUserPhotoThumbHref` en celdas miniatura.
- `.gitignore` — `cache/`
- `appsettings.json` — `UserPhotoCache`

**Parámetros Cloudinary (miniatura lista):** ver `UserPhotoDeliveryTransforms.ListThumbnail` en código.

---

## 3. Estrategia de fallback

1. **URL almacenada no es Cloudinary** (rutas locales, histórico): `variant=thumb` **no altera** la respuesta en el branch redirect; en rutas locales se sirven bytes como antes (sin transform CDN).
2. **Cloudinary con path no estándar:** `InsertAfterUpload` devuelve la URL original si no encuentra `/image/upload/`; el usuario sigue viendo imagen.
3. **Caché deshabilitada:** `"UserPhotoCache": { "Enabled": false }` → cada descarga va directo a HTTP (comportamiento equivalente al previo en coste, sin capa extra).
4. **Vista previa / carnet PDF en alta calidad:** siguen usando la URL original o `carnetEdge` / bytes completos según flujo existente; **no** se sustituye por `variant=thumb` en `Generate.cshtml`.

---

## 4. Impacto en bandwidth

| Flujo | Antes | Después |
|-------|-------|---------|
| Tabla carnets (`StudentIdCard/ui`) | Navegador pedía imagen completa vía redirect a URL almacenada. | Redirect a variante **128×128** aprox., `q_auto:eco`, menor peso por fila. |
| Directorio estudiantes (miniatura) | Igual. | Igual reducción vía `variant=thumb`. |
| PDF (misma foto/logo varias veces en corto tiempo) | Descarga HTTP repetida por proceso. | Reutilización en **RAM** y, si aplica, **disco** bajo `cache/http-images` hasta TTL / límite de tamaño. |
| CDN Cloudinary | Ya cachea entregas transformadas según políticas de Cloudinary. | Sin cambio de contrato; solo se piden URLs con transformación más pequeña donde basta miniatura. |

*Nota:* el ancho de banda **entre su servidor y Cloudinary** en `GetUserPhotoBytesAsync` también se beneficia del caché al generar varios PDFs seguidos con los mismos assets.

---

## 5. Plan futuro de migración opcional (sin riesgo)

1. **Eager transformations en subida (solo nuevas fotos):** en `CloudinaryService.UploadImageAsync`, añadir `EagerTransforms` para generar versiones precomputadas (p. ej. thumb y print). **No sustituye** la URL `SecureUrl` guardada hoy; sería **opcional** persistir URLs derivadas en columnas nuevas (`PhotoUrlThumb`, `PhotoUrlPrint`) con **nullable** y lectura preferente con fallback a transform on-the-fly — migración de datos **no requerida**.
2. **Proxy con Cache-Control propio:** si en algún momento se dejara de redirigir a Cloudinary y se sirvieran bytes desde la app, se podría unificar política de caché HTTP; hoy se prioriza **redirect** al CDN para aprovechar su edge.
3. **Métricas:** contadores en `HttpBytesDownloadCache` (hit/miss) para afinar TTL y `MemoryCacheSizeLimitBytes` según RAM del host.

---

## Validación frente a requisitos

- **No se pierden imágenes:** no se llama a Destroy ni a Replace sobre activos existentes.
- **URLs actuales en BD:** intactas; solo cambian las URLs **generadas en vistas** para listados (query `variant`) y las ya existentes `carnetEdge` para carnet.
- **Preview carnet y PDF:** siguen usando rutas de alta calidad / bytes completos donde ya correspondía.
- **Producción:** todo es incremental; desactivar caché o quitar `variant` en una vista revierte el comportamiento de red sin tocar datos.
