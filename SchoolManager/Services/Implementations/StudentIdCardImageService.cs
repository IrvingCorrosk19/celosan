using SkiaSharp;
using SchoolManager.Services;
using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.Services.Security;

namespace SchoolManager.Services.Implementations;

public class StudentIdCardImageService : IStudentIdCardImageService
{
    private static readonly int LandW = IdCardPhysicalDimensions.LandscapeWidthPx;
    private static readonly int LandH = IdCardPhysicalDimensions.LandscapeHeightPx;
    private static readonly int PortW = IdCardPhysicalDimensions.PortraitWidthPx;
    private static readonly int PortH = IdCardPhysicalDimensions.PortraitHeightPx;

    private readonly IQrSignatureService _qrSig;

    public StudentIdCardImageService(IQrSignatureService qrSig) => _qrSig = qrSig;

    private static bool IsInstitutionalVertical(SchoolIdCardSetting s) =>
        s.UseModernLayout && !string.Equals((s.Orientation ?? "Vertical").Trim(),
            "Horizontal", StringComparison.OrdinalIgnoreCase);

    private static (int w, int h) GetDims(SchoolIdCardSetting s) =>
        IsInstitutionalVertical(s) ? (PortW, PortH) : (LandW, LandH);

    public (float WidthMm, float HeightMm) GetCardMmDimensions(SchoolIdCardSetting s) =>
        IsInstitutionalVertical(s)
            ? (IdCardPhysicalDimensions.ShortMm, IdCardPhysicalDimensions.LongMm)
            : (IdCardPhysicalDimensions.LongMm, IdCardPhysicalDimensions.ShortMm);

    // ─────────────────────────────────────────────────────────────────────────
    public byte[] GenerateCardImage(StudentCardRenderDto dto, SchoolIdCardSetting settings,
        IReadOnlyList<IdCardTemplateField>? customFields = null)
    {
        var (w, h) = GetDims(settings);
        using var bitmap = new SKBitmap(w, h);
        using var canvas = new SKCanvas(bitmap);

        if (customFields is { Count: > 0 })
            DrawCustomFields(canvas, w, h, dto, settings, customFields);
        else if (IsInstitutionalVertical(settings))
            DrawInstitutionalVerticalFront(canvas, w, h, dto, settings);
        else if (settings.UseModernLayout)
            DrawModernHorizontalFront(canvas, w, h, dto, settings);
        else
            DrawClassicFront(canvas, w, h, dto, settings);

        return ToPng(bitmap);
    }

    public byte[] GenerateCardBackImage(StudentCardRenderDto dto, SchoolIdCardSetting settings)
    {
        var (w, h) = GetDims(settings);
        using var bitmap = new SKBitmap(w, h);
        using var canvas = new SKCanvas(bitmap);
        DrawBack(canvas, w, h, dto, settings);
        return ToPng(bitmap);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CLÁSICO — landscape (85×55 mm @300dpi)
    // ══════════════════════════════════════════════════════════════════════════
    private void DrawClassicFront(SKCanvas canvas, int w, int h,
        StudentCardRenderDto dto, SchoolIdCardSetting settings)
    {
        var bg      = Col(settings.BackgroundColor, SKColors.White);
        var primary = Col(settings.PrimaryColor,    new SKColor(13, 110, 253));
        var textCol = Col(settings.TextColor,        SKColors.Black);

        canvas.Clear(bg);

        float headerH = h * 0.18f;
        float footerH = h * 0.12f;
        float bodyH   = h - headerH - footerH;
        float bodyTop = headerH;
        float hPad    = w * 0.035f;
        float vPad    = h * 0.03f;

        // ── Watermark ─────────────────────────────────────────────────────────
        DrawWatermark(canvas, dto.WatermarkBytes, w, h, 0.42f);

        // ── Header ────────────────────────────────────────────────────────────
        using (var p = Fill(primary)) canvas.DrawRect(0, 0, w, headerH, p);

        float logoSz = headerH * 0.7304f;
        float nameX  = hPad;
        if (dto.LogoBytes != null)
        {
            using var lb = Decode(dto.LogoBytes);
            if (lb != null)
            {
                var lr = FitRect(lb.Width, lb.Height, logoSz, logoSz, hPad, (headerH - logoSz) / 2f);
                BmpDraw(canvas, lb, lr);
                nameX = lr.Right + hPad * 0.5f;
            }
        }
        AutoText(canvas, dto.SchoolName, nameX, headerH * 0.62f, w - nameX - hPad,
            headerH * 0.28f, SKColors.White, bold: true);

        // ── Foto ──────────────────────────────────────────────────────────────
        float photoSz = bodyH * 0.80f;
        float photoX  = hPad;
        float photoY  = bodyTop + (bodyH - photoSz) / 2f;
        float borderW = Math.Max(2f, h * 0.003f);

        using (var bp = Stroke(primary, borderW)) canvas.DrawRect(photoX, photoY, photoSz, photoSz, bp);
        if (settings.ShowPhoto)
            DrawPhoto(canvas, dto.PhotoBytes, photoX + borderW, photoY + borderW,
                photoSz - borderW * 2f, photoSz - borderW * 2f, textCol);

        // ── QR ────────────────────────────────────────────────────────────────
        float qrSz = bodyH * 0.42f;
        float qrX  = w - hPad - qrSz;
        float qrY  = bodyTop + (bodyH - qrSz) / 2f;
        if (settings.ShowQr)
            DrawQr(canvas, dto.QrToken, SKRect.Create(qrX, qrY, qrSz, qrSz));

        // ── Datos ─────────────────────────────────────────────────────────────
        float dataX   = photoX + photoSz + hPad;
        float dataW   = (settings.ShowQr ? qrX - hPad * 0.5f : w - hPad) - dataX;
        float nameFs  = h * 0.058f;
        float cardFs  = h * 0.045f;
        float smallFs = h * 0.038f;
        float lineH   = h * 0.085f;
        float ty      = bodyTop + vPad + nameFs;

        AutoText(canvas, dto.FullName,              dataX, ty, dataW, nameFs,  textCol, bold: true); ty += lineH;
        AutoText(canvas, $"Carnet: {dto.CardNumber}", dataX, ty, dataW, cardFs,  primary);            ty += lineH;
        AutoText(canvas, dto.Shift,                  dataX, ty, dataW, smallFs, textCol);

        // ── Footer ────────────────────────────────────────────────────────────
        float footerY = h - footerH;
        using (var lp = Stroke(Col("#e5e7eb", SKColors.LightGray), Math.Max(1f, h * 0.0016f)))
            canvas.DrawLine(0, footerY, w, footerY, lp);
        AutoText(canvas, $"Emitido: {DateTime.UtcNow:dd/MM/yyyy}",
            hPad, footerY + footerH * 0.65f, w - hPad * 2f, h * 0.038f, textCol);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // INSTITUCIONAL VERTICAL — portrait (55×85 mm @300dpi)
    // ══════════════════════════════════════════════════════════════════════════
    private void DrawInstitutionalVerticalFront(SKCanvas canvas, int w, int h,
        StudentCardRenderDto dto, SchoolIdCardSetting settings)
    {
        var bg      = Col(settings.BackgroundColor, SKColors.White);
        var primary = Col(settings.PrimaryColor,    new SKColor(13, 110, 253));
        var textCol = Col(settings.TextColor,        SKColors.Black);

        canvas.Clear(bg);

        float headerH  = h * 0.20f;
        float footerH  = h * 0.08f;
        float bodyH    = h - headerH - footerH;
        float hPad     = w * 0.06f;
        float textW    = w - hPad * 2f;
        float borderW  = Math.Max(2f, h * 0.002f);

        // Sub-zonas proporcionales al body
        float photoZH  = bodyH * 0.34f;
        float dataZH   = bodyH * 0.30f;
        float bottomZH = bodyH * 0.36f;

        float photoZoneTop  = headerH;
        float dataZoneTop   = headerH + photoZH;
        float bottomZoneTop = headerH + photoZH + dataZH;

        DrawWatermark(canvas, dto.WatermarkBytes, w, h, 0.45f);

        // ── Header ────────────────────────────────────────────────────────────
        using (var p = Fill(primary)) canvas.DrawRect(0, 0, w, headerH, p);

        float logoSz = headerH * 0.5435f;
        float logoY  = headerH * 0.06f;
        if (dto.LogoBytes != null)
        {
            using var lb = Decode(dto.LogoBytes);
            if (lb != null)
            {
                var lr = FitRect(lb.Width, lb.Height, logoSz, logoSz, (w - logoSz) / 2f, logoY);
                BmpDraw(canvas, lb, lr);
                AutoText(canvas, dto.SchoolName, hPad, lr.Bottom + headerH * 0.175f, textW,
                    headerH * 0.175f, SKColors.White, bold: true, center: true);
            }
            else
            {
                AutoText(canvas, dto.SchoolName, hPad, headerH * 0.62f, textW,
                    headerH * 0.22f, SKColors.White, bold: true, center: true);
            }
        }
        else
        {
            AutoText(canvas, dto.SchoolName, hPad, headerH * 0.62f, textW,
                headerH * 0.22f, SKColors.White, bold: true, center: true);
        }

        if (settings.ShowSecondaryLogo && dto.SecondaryLogoBytes != null)
        {
            using var sb = Decode(dto.SecondaryLogoBytes);
            if (sb != null)
            {
                float sbSz = headerH * 0.38f;
                BmpDraw(canvas, sb, FitRect(sb.Width, sb.Height, sbSz, sbSz,
                    w - hPad - sbSz, (headerH - sbSz) / 2f));
            }
        }

        // ── Foto ──────────────────────────────────────────────────────────────
        float photoSz = Math.Min(photoZH * 0.88f, w * 0.52f);
        float photoX  = (w - photoSz) / 2f;
        float photoY  = photoZoneTop + (photoZH - photoSz) / 2f;

        using (var bp = Stroke(primary, borderW)) canvas.DrawRect(photoX, photoY, photoSz, photoSz, bp);
        if (settings.ShowPhoto)
            DrawPhoto(canvas, dto.PhotoBytes, photoX + borderW, photoY + borderW,
                photoSz - borderW * 2f, photoSz - borderW * 2f, textCol);

        // ── Datos ─────────────────────────────────────────────────────────────
        float nameFs  = h * 0.042f;
        float smallFs = h * 0.028f;
        float lineH   = dataZH * 0.27f;
        float ty      = dataZoneTop + nameFs * 1.1f;

        AutoText(canvas, dto.FullName, hPad, ty, textW, nameFs, textCol, bold: true, center: true);
        ty += lineH;

        if (settings.ShowDocumentId && !string.IsNullOrWhiteSpace(dto.DocumentId))
        {
            AutoText(canvas, $"Cédula: {dto.DocumentId}", hPad, ty, textW, smallFs, textCol, center: true);
            ty += lineH;
        }
        if (settings.ShowAcademicYear && !string.IsNullOrWhiteSpace(dto.AcademicYear))
            AutoText(canvas, $"Año: {dto.AcademicYear}", hPad, ty, textW, smallFs, textCol, center: true);

        // ── Bottom: QR + póliza ───────────────────────────────────────────────
        using (var p = Fill(new SKColor(230, 238, 247)))
            canvas.DrawRect(0, bottomZoneTop, w, bottomZH, p);

        float qrSz = bottomZH * 0.52f;
        float qrX  = w - hPad - qrSz;
        float qrY  = bottomZoneTop + (bottomZH - qrSz) / 2f;
        if (settings.ShowQr)
            DrawQr(canvas, dto.QrToken, SKRect.Create(qrX, qrY, qrSz, qrSz));

        float leftW    = qrX - hPad * 2f;
        float polFs    = h * 0.028f;
        float polIdFs  = h * 0.025f;
        float lty      = bottomZoneTop + bottomZH * 0.20f;

        if (settings.ShowPolicyNumber && !string.IsNullOrWhiteSpace(dto.PolicyNumber))
        {
            AutoText(canvas, "Póliza de Seguro Educativo", hPad, lty, leftW, polFs, primary, bold: true);
            lty += polFs * 1.5f;
            AutoText(canvas, Trunc(dto.PolicyNumber, 30), hPad, lty, leftW, polFs, textCol, bold: true);
            lty += polFs * 1.55f;
        }
        AutoText(canvas, Trunc($"ID: {dto.CardNumber}", 22), hPad, lty, leftW, polIdFs, textCol, bold: true);

        // ── Footer ────────────────────────────────────────────────────────────
        float footerY = h - footerH;
        using (var p = Fill(primary)) canvas.DrawRect(0, footerY, w, footerH, p);
        AutoText(canvas, "Documento de identificación estudiantil",
            hPad, footerY + footerH * 0.65f, textW, footerH * 0.38f, SKColors.White, center: true);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // MODERNO HORIZONTAL — landscape (85×55 mm @300dpi)
    // ══════════════════════════════════════════════════════════════════════════
    private void DrawModernHorizontalFront(SKCanvas canvas, int w, int h,
        StudentCardRenderDto dto, SchoolIdCardSetting settings)
    {
        var bg      = Col(settings.BackgroundColor, SKColors.White);
        var primary = Col(settings.PrimaryColor,    new SKColor(13, 110, 253));
        var textCol = Col(settings.TextColor,        SKColors.Black);

        canvas.Clear(bg);

        float headerH = h * 0.20f;
        float footerH = h * 0.10f;
        float bodyH   = h - headerH - footerH;
        float bodyTop = headerH;
        float hPad    = w * 0.03f;
        float borderW = Math.Max(2f, h * 0.003f);

        DrawWatermark(canvas, dto.WatermarkBytes, w, h, 0.40f);

        // ── Header ────────────────────────────────────────────────────────────
        using (var p = Fill(primary)) canvas.DrawRect(0, 0, w, headerH, p);

        float logoSz = headerH * 0.7304f;
        float nameX  = hPad;
        float nameW  = w - hPad * 2f;

        if (dto.LogoBytes != null)
        {
            using var lb = Decode(dto.LogoBytes);
            if (lb != null)
            {
                var lr = FitRect(lb.Width, lb.Height, logoSz, logoSz, hPad, (headerH - logoSz) / 2f);
                BmpDraw(canvas, lb, lr);
                nameX = lr.Right + hPad * 0.5f;
                nameW = w - nameX - hPad;
            }
        }
        if (settings.ShowSecondaryLogo && dto.SecondaryLogoBytes != null)
        {
            using var sb = Decode(dto.SecondaryLogoBytes);
            if (sb != null)
            {
                var sr = FitRect(sb.Width, sb.Height, logoSz, logoSz,
                    w - hPad - logoSz, (headerH - logoSz) / 2f);
                BmpDraw(canvas, sb, sr);
                nameW = sr.Left - hPad * 0.5f - nameX;
            }
        }
        AutoText(canvas, dto.SchoolName, nameX, headerH * 0.62f, nameW, headerH * 0.28f,
            SKColors.White, bold: true);

        // ── Foto ──────────────────────────────────────────────────────────────
        float photoSz = bodyH * 0.80f;
        float photoX  = hPad;
        float photoY  = bodyTop + (bodyH - photoSz) / 2f;

        using (var bp = Stroke(primary, borderW)) canvas.DrawRect(photoX, photoY, photoSz, photoSz, bp);
        if (settings.ShowPhoto)
            DrawPhoto(canvas, dto.PhotoBytes, photoX + borderW, photoY + borderW,
                photoSz - borderW * 2f, photoSz - borderW * 2f, textCol);

        // ── QR ────────────────────────────────────────────────────────────────
        float qrSz = bodyH * 0.42f;
        float qrX  = w - hPad - qrSz;
        float qrY  = bodyTop + (bodyH - qrSz) / 2f;
        if (settings.ShowQr)
            DrawQr(canvas, dto.QrToken, SKRect.Create(qrX, qrY, qrSz, qrSz));

        // ── Datos ─────────────────────────────────────────────────────────────
        float dataX   = photoX + photoSz + hPad;
        float dataW   = (settings.ShowQr ? qrX - hPad * 0.5f : w - hPad) - dataX;
        float nameFs  = h * 0.055f;
        float cardFs  = h * 0.042f;
        float smallFs = h * 0.036f;
        float lineH   = h * 0.082f;
        float ty      = bodyTop + bodyH * 0.10f + nameFs;

        AutoText(canvas, dto.FullName,               dataX, ty, dataW, nameFs,  textCol, bold: true); ty += lineH;
        AutoText(canvas, $"Carnet: {dto.CardNumber}", dataX, ty, dataW, cardFs,  primary);             ty += lineH;
        AutoText(canvas, dto.Shift,                   dataX, ty, dataW, smallFs, textCol);             ty += lineH;
        if (settings.ShowDocumentId && !string.IsNullOrWhiteSpace(dto.DocumentId))
            AutoText(canvas, $"Cédula: {dto.DocumentId}", dataX, ty, dataW, smallFs, textCol);

        // ── Footer ────────────────────────────────────────────────────────────
        float footerY = h - footerH;
        using (var lp = Stroke(Col("#e5e7eb", SKColors.LightGray), Math.Max(1f, h * 0.0016f)))
            canvas.DrawLine(0, footerY, w, footerY, lp);
        AutoText(canvas, $"Emitido: {DateTime.UtcNow:dd/MM/yyyy}",
            hPad, footerY + footerH * 0.65f, w - hPad * 2f, h * 0.038f, textCol);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CAMPOS PERSONALIZADOS
    // ══════════════════════════════════════════════════════════════════════════
    private void DrawCustomFields(SKCanvas canvas, int w, int h,
        StudentCardRenderDto dto, SchoolIdCardSetting settings,
        IReadOnlyList<IdCardTemplateField> fields)
    {
        canvas.Clear(Col(settings.BackgroundColor, SKColors.White));
        var primary = Col(settings.PrimaryColor, new SKColor(13, 110, 253));
        var textCol = Col(settings.TextColor,     SKColors.Black);

        float hdrH = h * 0.18f;
        using (var p = Fill(primary)) canvas.DrawRect(0, 0, w, hdrH, p);

        foreach (var f in fields)
        {
            if (!f.IsEnabled) continue;
            // Coordenadas absolutas desde la DB, convertidas a px proporcional al canvas
            float scaleX = w / IdCardPhysicalDimensions.LongMm;
            float scaleY = h / IdCardPhysicalDimensions.ShortMm;
            float fx = (float)f.XMm * scaleX;
            float fy = (float)f.YMm * scaleY;
            float fw = (float)f.WMm * scaleX;
            float fh = (float)f.HMm * scaleY;
            float fs = (float)f.FontSize * (h / IdCardPhysicalDimensions.ShortMm) * 0.38f;

            switch (f.FieldKey)
            {
                case "SchoolName":
                    AutoText(canvas, dto.SchoolName, fx, fy + fs, fw, fs, SKColors.White, bold: true);
                    break;
                case "SchoolLogo":
                    if (dto.LogoBytes != null) { using var lb = Decode(dto.LogoBytes);
                        if (lb != null) BmpDraw(canvas, lb, FitRect(lb.Width, lb.Height, fw, fh, fx, fy)); }
                    break;
                case "Photo":
                    using (var bp = Stroke(textCol, 2f)) canvas.DrawRect(fx, fy, fw, fh, bp);
                    DrawPhoto(canvas, dto.PhotoBytes, fx + 2, fy + 2, fw - 4, fh - 4, textCol);
                    break;
                case "FullName":
                    AutoText(canvas, dto.FullName, fx, fy + fs, fw, fs, textCol, bold: true);
                    break;
                case "DocumentId":
                    AutoText(canvas, dto.DocumentId ?? "", fx, fy + fs, fw, fs, textCol);
                    break;
                case "Grade":
                case "Group":
                    // Requisito funcional: no mostrar grado/grupo en carnet.
                    break;
                case "Shift":
                    AutoText(canvas, dto.Shift, fx, fy + fs, fw, fs, textCol);
                    break;
                case "CardNumber":
                    AutoText(canvas, dto.CardNumber, fx, fy + fs, fw, fs, textCol);
                    break;
                case "Qr":
                    if (settings.ShowQr)
                        DrawQr(canvas, dto.QrToken, SKRect.Create(fx, fy, fw, fh));
                    break;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // REVERSO — adaptativo (landscape o portrait)
    // ══════════════════════════════════════════════════════════════════════════
    private void DrawBack(SKCanvas canvas, int w, int h,
        StudentCardRenderDto dto, SchoolIdCardSetting settings)
    {
        var bg      = Col(settings.BackgroundColor, SKColors.White);
        var primary = Col(settings.PrimaryColor,    new SKColor(13, 110, 253));
        var textCol = Col(settings.TextColor,        SKColors.Black);

        canvas.Clear(bg);
        DrawWatermark(canvas, dto.WatermarkBytes, w, h, 0.45f);

        float hPad     = w * 0.05f;
        float contentW = w - hPad * 2f;
        float footerH  = h * 0.12f;
        float availH   = h - footerH;
        float footerY  = h - footerH;

        var hasEmergencyQr = settings.ShowQr && !string.IsNullOrWhiteSpace(dto.EmergencyInfoPageUrl);
        float emgQrSz = hasEmergencyQr ? Math.Min(w, h) * 0.26f : 0f;
        float emgCaptionFs = h * 0.032f;
        float emgGap = h * 0.014f;
        float emgBlockH = hasEmergencyQr ? emgQrSz + emgCaptionFs + emgGap + h * 0.01f : 0f;
        float emgQrTop = hasEmergencyQr ? footerY - emgBlockH : footerY;
        float middleCeiling = hasEmergencyQr ? emgQrTop - h * 0.02f : footerY - h * 0.02f;

        float qrY = h * 0.03f;
        float qrBlockBottom = qrY;

        if (settings.ShowQr)
        {
            float verifySz = hasEmergencyQr ? Math.Min(w, availH) * 0.36f : Math.Min(w, availH) * 0.45f;
            float qrX = (w - verifySz) / 2f;
            DrawQr(canvas, dto.QrToken, SKRect.Create(qrX, qrY, verifySz, verifySz));
            qrBlockBottom = qrY + verifySz + h * 0.018f;
        }

        float nameFs  = h * 0.050f;
        float smallFs = h * 0.038f;
        float lineH   = h * 0.085f;
        float sLineH  = h * 0.068f;
        float ty      = qrBlockBottom + h * 0.02f + nameFs;

        bool Room(float nextLineH) => ty + nextLineH <= middleCeiling;

        AutoText(canvas, Trunc(dto.SchoolName, 50), hPad, ty, contentW, nameFs, textCol, bold: true, center: true);
        ty += lineH;
        if (Room(sLineH))
        {
            AutoText(canvas, "Escanea el código superior para verificar el carnet",
                hPad, ty, contentW, smallFs * 0.88f, textCol, center: true);
            ty += sLineH;
        }
        if (Room(sLineH))
        {
            AutoText(canvas, Trunc($"Carnet: {dto.CardNumber}", 30), hPad, ty, contentW, smallFs, textCol, center: true);
            ty += sLineH;
        }

        if (Room(sLineH) && !string.IsNullOrWhiteSpace(dto.IdCardPolicy))
        {
            var pol = dto.IdCardPolicy.Trim();
            if (pol.Length > 120) pol = pol[..119] + "…";
            AutoText(canvas, pol, hPad, ty, contentW, smallFs * 0.82f, textCol, center: true);
            ty += sLineH;
        }
        if (Room(sLineH) && settings.ShowSchoolPhone && !string.IsNullOrWhiteSpace(dto.SchoolPhone))
        {
            AutoText(canvas, $"Tel: {dto.SchoolPhone}", hPad, ty, contentW, smallFs, textCol, center: true);
            ty += sLineH;
        }
        if (Room(sLineH) && settings.ShowEmergencyContact && !string.IsNullOrWhiteSpace(dto.EmergencyContactName))
        {
            AutoText(canvas, $"Emergencia: {Trunc(dto.EmergencyContactName, 30)}",
                hPad, ty, contentW, smallFs, textCol, center: true);
            ty += sLineH;
            if (Room(sLineH) && !string.IsNullOrWhiteSpace(dto.EmergencyContactPhone))
            {
                AutoText(canvas, $"Tel: {dto.EmergencyContactPhone}", hPad, ty, contentW, smallFs, textCol, center: true);
                ty += sLineH;
            }
        }
        if (Room(sLineH) && settings.ShowAllergies && !string.IsNullOrWhiteSpace(dto.Allergies))
        {
            var allg = dto.Allergies.Length > 100 ? dto.Allergies[..99] + "…" : dto.Allergies;
            AutoText(canvas, $"Alergias: {allg}", hPad, ty, contentW, smallFs * 0.88f, textCol, center: true);
        }

        if (hasEmergencyQr)
        {
            float emgX = (w - emgQrSz) / 2f;
            DrawPlainUrlQr(canvas, dto.EmergencyInfoPageUrl!, SKRect.Create(emgX, emgQrTop, emgQrSz, emgQrSz));
            var cap = "Escaneame en caso de emergencia";
            using var tf = SKTypeface.FromFamilyName(null, SKFontStyle.Bold);
            using var cp = new SKPaint { Color = textCol, TextSize = emgCaptionFs, IsAntialias = true, Typeface = tf };
            while (cp.MeasureText(cap) > contentW && cp.TextSize > 8f)
                cp.TextSize -= 0.5f;
            float capY = emgQrTop + emgQrSz + emgCaptionFs + emgGap * 0.35f;
            float capX = hPad + (contentW - cp.MeasureText(cap)) / 2f;
            canvas.DrawText(cap, capX, capY, cp);
        }

        // Footer
        using (var p = Fill(primary)) canvas.DrawRect(0, footerY, w, footerH, p);
        AutoText(canvas, "Documento de identificación estudiantil",
            hPad, footerY + footerH * 0.65f, contentW, footerH * 0.38f, SKColors.White, center: true);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIMITIVOS
    // ══════════════════════════════════════════════════════════════════════════
    private void DrawQr(SKCanvas canvas, string? token, SKRect dest)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        var b = SafeQr(token);
        if (b == null) return;
        using var bmp = Decode(b);
        if (bmp != null) BmpDraw(canvas, bmp, dest);
    }

    /// <summary>QR con contenido literal (p. ej. URL https) sin firma adicional.</summary>
    private static void DrawPlainUrlQr(SKCanvas canvas, string url, SKRect dest)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            var b = QrHelper.GenerateQrPng(url, null);
            using var bmp = Decode(b);
            if (bmp != null) BmpDraw(canvas, bmp, dest);
        }
        catch
        {
            /* ignore */
        }
    }

    private static void DrawWatermark(SKCanvas canvas, byte[]? bytes, int w, int h, float pct)
    {
        if (bytes == null || bytes.Length == 0) return;
        using var bmp = Decode(bytes);
        if (bmp == null) return;
        float sz = Math.Min(w, h) * pct;
        BmpDraw(canvas, bmp, FitRect(bmp.Width, bmp.Height, sz, sz,
            (w - sz) / 2f, (h - sz) / 2f));
    }

    private static void DrawPhoto(SKCanvas canvas, byte[]? photoBytes,
        float x, float y, float w, float h, SKColor textCol)
    {
        if (photoBytes != null && photoBytes.Length > 0)
        {
            using var bmp = Decode(photoBytes);
            if (bmp != null) { BmpDraw(canvas, bmp, FitRect(bmp.Width, bmp.Height, w, h, x, y)); return; }
        }
        using var p = new SKPaint { Color = textCol, TextSize = Math.Max(16f, h * 0.12f), IsAntialias = true };
        float tw = p.MeasureText("FOTO");
        canvas.DrawText("FOTO", x + (w - tw) / 2f, y + (h + p.TextSize) / 2f, p);
    }

    private static void BmpDraw(SKCanvas canvas, SKBitmap bmp, SKRect dest)
    {
        using var p = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High };
        canvas.DrawBitmap(bmp, dest, p);
    }

    /// <summary>
    /// Auto-fit text: reduce font until it fits maxW; if still too wide, truncate with "...".
    /// y = baseline. center=true centers horizontally in [x, x+maxW].
    /// </summary>
    private static void AutoText(SKCanvas canvas, string text, float x, float y, float maxW,
        float fontSize, SKColor color, bool bold = false, bool center = false)
    {
        if (string.IsNullOrEmpty(text)) return;

        using var tf    = SKTypeface.FromFamilyName(null, bold ? SKFontStyle.Bold : SKFontStyle.Normal);
        using var paint = new SKPaint { Color = color, TextSize = fontSize, IsAntialias = true, Typeface = tf };

        while (paint.MeasureText(text) > maxW && paint.TextSize > 12f)
            paint.TextSize -= 1f;

        if (paint.MeasureText(text) > maxW)
        {
            while (text.Length > 1 && paint.MeasureText(text + "...") > maxW)
                text = text[..^1];
            text += "...";
        }

        float drawX = center ? x + (maxW - paint.MeasureText(text)) / 2f : x;
        canvas.DrawText(text, drawX, y, paint);
    }

    private static SKRect FitRect(int imgW, int imgH, float maxW, float maxH, float ox, float oy)
    {
        float scale = Math.Min(maxW / Math.Max(imgW, 1), maxH / Math.Max(imgH, 1));
        float fw = imgW * scale, fh = imgH * scale;
        return SKRect.Create(ox + (maxW - fw) / 2f, oy + (maxH - fh) / 2f, fw, fh);
    }

    private static SKPaint Fill(SKColor c)   => new() { Color = c, Style = SKPaintStyle.Fill };
    private static SKPaint Stroke(SKColor c, float w) =>
        new() { Color = c, Style = SKPaintStyle.Stroke, StrokeWidth = w };

    private static SKColor Col(string? hex, SKColor fallback = default)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try { return SKColor.Parse(hex.StartsWith('#') ? hex : '#' + hex); }
        catch { return fallback; }
    }

    private static string Trunc(string? t, int max) =>
        string.IsNullOrEmpty(t) ? "" : t.Length <= max ? t : t[..(max - 1)] + "…";

    private byte[]? SafeQr(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try   { return QrHelper.GenerateQrPng(token, _qrSig); }
        catch { try { return QrHelper.GenerateQrPng(token, null); } catch { return null; } }
    }

    private static SKBitmap? Decode(byte[]? b)
    {
        if (b == null || b.Length == 0) return null;
        try { return SKBitmap.Decode(b); } catch { return null; }
    }

    private static byte[] ToPng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data  = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
