# Mejora de resolución de la foto del carnet (sin romper layout)

**Ámbito:** vista previa HTML del carnet en `StudentIdCard/ui/generate/{id}` (`Views/StudentIdCard/Generate.cshtml`) y captura PDF vía Puppeteer (`StudentIdCardHtmlCaptureService`).

**Regla respetada:** el marco de la foto sigue siendo **100px × 100px** (`.idcard-photo-inner`). No se modifican `width`/`height` del contenedor ni su posición en el flex del carnet.

---

## 1. Cambios CSS exactos

**Archivo:** `Views/StudentIdCard/Generate.cshtml` (bloque `<style>`).

| Selector | Cambio |
|----------|--------|
| `.idcard-photo-inner` | Se añade `contain: layout paint` para aislar pintado y evitar fugas visuales sin alterar tamaño. |
| `.idcard-photo-inner img` | Se mantiene **100% × 100%** dentro del mismo marco; se añaden `max-width`/`max-height` 100%, `object-position: center center`, `display: block`, `flex-shrink: 0`, `backface-visibility: hidden` (suaviza composición en Chromium). |

**Sin cambiar:** `width: 100px; height: 100px` de `.idcard-photo-inner`.

**Garantías de recorte:**

- `overflow: hidden` (ya existía en el contenedor).
- `object-fit: cover` (mantenido): sin estiramiento; recorte con foco centrado; la imagen no sobresale del marco.

---

## 2. Cambios en HTML (`<img>`)

**Archivo:** `Views/StudentIdCard/Generate.cshtml`.

- **`src`:** URL generada con `UserPhotoLinks.HrefForCarnetPreview(photoUrl, 360)` → pide ~3,6× píxeles de borde respecto al marco de 100px.
- **`srcset`:** variantes `280` (≈2,8×) y `420` (≈4,2×) con descriptores `2x` y `3x` para que el navegador elija densidad según `devicePixelRatio`.
- **`sizes="100px"`:** alinea la elección de `srcset` con el ancho visual del marco.
- **`decoding="async"`** y **`fetchpriority="high"`:** priorizan decodificación estable en vista previa y en captura.

**Origen de alta resolución (Cloudinary):** el query `carnetEdge` en `/File/GetUserPhoto` hace que, si la foto es una URL `res.cloudinary.com`, se redirija a una URL con transformación `w_N,h_N,c_fill,g_auto,q_auto:good,f_auto` insertada tras `/image/upload/` (ver `Helpers/CloudinaryCarnetDeliveryUrl.cs`). Fotos locales u otras rutas siguen el flujo anterior (bytes completos).

---

## 3. Ajustes para Puppeteer

**Archivos:** `Services/Implementations/StudentIdCardPdfPrintOptions.cs`, `appsettings.json` → sección `StudentIdCardPdf`.

| Parámetro | Antes | Después | Efecto |
|-----------|-------|---------|--------|
| `DeviceScaleFactor` | `2` | `3` | Viewport de Chromium con mayor DPR por defecto; la captura de `#idCardFront` conserva el mismo tamaño lógico del DOM pero con más píxeles por unidad CSS. |

El perfil **CardPrinter** ya recalcula DPR óptimo frente al bounding box del carnet y respeta `MaxDeviceScaleFactor` (4): la lógica existente en `StudentIdCardHtmlCaptureService` sigue vigente.

**Sin cambiar:** dimensiones CSS del carnet, `ContentScale`, ni el flujo `GoToAsync` → captura por elemento.

---

## 4. Ejemplo antes vs después

| Aspecto | Antes | Después |
|---------|-------|---------|
| URL de foto en vista carnet | Redirección directa al asset Cloudinary tal cual está almacenado (a veces grande, a veces ya transformado en otro flujo). | Con `carnetEdge`, se fuerza un cuadrado de entrega **N×N** con `c_fill` y `g_auto`, optimizado para rostro y compresión (`q_auto:good`, `f_auto`). |
| Píxeles decodificados vs marco 100px | A menudo 1:1 o poco margen frente al DPR de captura. | **2×–3×** explícitos vía `src`/`srcset` + recorte CDN acorde al marco. |
| CSS `img` | `width/height 100%` + `object-fit: cover`. | Igual tamaño visual; más control (`object-position`, `max-*`, `contain` en el padre). |
| Puppeteer | DPR base 2. | DPR base 3 (hasta 4 si el ajuste dinámico lo pide). |

---

## 5. Impacto visual esperado

- **+ nitidez** en contorno facial y textura de piel al imprimir o exportar PDF HTML, al combinar más píxeles fuente con el mismo recorte `cover` en 100×100.
- **Sin deformación:** `object-fit: cover` evita stretching; proporción preservada.
- **Sin desbordamiento:** `overflow: hidden` + `max-width`/`max-height` en la imagen.
- **Rostro centrado:** `g_auto` en Cloudinary + `object-position: center` en CSS.
- **Rutas no Cloudinary:** no empeoran; se sirve el archivo completo como antes.

**Nota:** el beneficio máximo se obtiene con fotos alojadas en **Cloudinary** y URLs estándar `.../image/upload/...`. Plantillas PDF **100% personalizadas** (Skia/QuestPDF sin esta vista) no usan este pipeline HTML.

---

## Referencia de código

- `Helpers/UserPhotoLinks.cs` — `HrefForCarnetPreview`
- `Helpers/CloudinaryCarnetDeliveryUrl.cs` — inserción de cadena de transformación
- `Controllers/FileController.cs` — `GetUserPhoto(..., int? carnetEdge)`
- `Views/StudentIdCard/Generate.cshtml` — CSS `.idcard-photo-inner` / `img` y etiqueta `<img>` del frente
