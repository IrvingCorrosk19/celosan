using SkiaSharp;
using SchoolManager.Helpers;
using SchoolManager.Dtos;
using SchoolManager.Services;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.Services.Security;

namespace SchoolManager.Services.Implementations;

public class InstitutionalCredentialImageService : IInstitutionalCredentialImageService
{
    private static readonly int PortW = IdCardPhysicalDimensions.PortraitWidthPx;
    private static readonly int PortH = IdCardPhysicalDimensions.PortraitHeightPx;

    private readonly IQrSignatureService _qrSig;

    public InstitutionalCredentialImageService(IQrSignatureService qrSig) => _qrSig = qrSig;

    public (float WidthMm, float HeightMm) GetPortraitCardMmDimensions() =>
        (IdCardPhysicalDimensions.ShortMm, IdCardPhysicalDimensions.LongMm);

    public byte[] GenerateFrontPng(StaffCardRenderDto dto, SchoolIdCardSetting settings)
    {
        using var bitmap = new SKBitmap(PortW, PortH);
        using var canvas = new SKCanvas(bitmap);
        DrawFrontPortrait(canvas, PortW, PortH, dto, settings);
        return ToPng(bitmap);
    }

    public byte[] GenerateBackPng(StaffCardRenderDto dto, SchoolIdCardSetting settings)
    {
        using var bitmap = new SKBitmap(PortW, PortH);
        using var canvas = new SKCanvas(bitmap);
        DrawBackPortrait(canvas, PortW, PortH, dto, settings);
        return ToPng(bitmap);
    }

    private void DrawFrontPortrait(SKCanvas canvas, int w, int h, StaffCardRenderDto dto, SchoolIdCardSetting settings)
    {
        var bg = Col(settings.BackgroundColor, SKColors.White);
        var primary = Col(settings.PrimaryColor, new SKColor(13, 110, 253));
        var textCol = Col(settings.TextColor, SKColors.Black);

        canvas.Clear(bg);

        float headerH = h * 0.20f;
        float footerH = h * 0.08f;
        float bodyH = h - headerH - footerH;
        float hPad = w * 0.06f;
        float textW = w - hPad * 2f;
        float borderW = Math.Max(2f, h * 0.002f);

        float photoZH = bodyH * 0.34f;
        float dataZH = bodyH * 0.34f;
        float bottomZH = bodyH * 0.32f;

        float photoZoneTop = headerH;
        float dataZoneTop = headerH + photoZH;
        float bottomZoneTop = headerH + photoZH + dataZH;

        DrawWatermark(canvas, dto.WatermarkBytes, w, h, 0.45f);

        using (var p = Fill(primary))
            canvas.DrawRect(0, 0, w, headerH, p);

        float logoSz = headerH * 0.5435f;
        float logoY = headerH * 0.06f;
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

        float photoSz = Math.Min(photoZH * 0.88f, w * 0.52f);
        float photoX = (w - photoSz) / 2f;
        float photoY = photoZoneTop + (photoZH - photoSz) / 2f;

        using (var bp = Stroke(primary, borderW))
            canvas.DrawRect(photoX, photoY, photoSz, photoSz, bp);
        if (settings.ShowPhoto)
            DrawPhoto(canvas, dto.PhotoBytes, photoX + borderW, photoY + borderW,
                photoSz - borderW * 2f, photoSz - borderW * 2f, textCol);

        float nameFs = h * 0.038f;
        float smallFs = h * 0.026f;
        float lineH = dataZH * 0.22f;
        float ty = dataZoneTop + nameFs * 1.05f;

        AutoText(canvas, dto.FullName, hPad, ty, textW, nameFs, textCol, bold: true, center: true);
        ty += lineH;
        if (settings.ShowDocumentId && !string.IsNullOrWhiteSpace(dto.DocumentId))
        {
            AutoText(canvas, $"Cédula: {dto.DocumentId}", hPad, ty, textW, smallFs, textCol, center: true);
            ty += lineH;
        }

        AutoText(canvas, $"Rol: {Trunc(dto.RoleDisplay, 36)}", hPad, ty, textW, smallFs, textCol, center: true);
        ty += lineH;
        AutoText(canvas, $"Cargo: {Trunc(dto.JobTitle, 36)}", hPad, ty, textW, smallFs, textCol, center: true);
        ty += lineH;
        AutoText(canvas, $"Área: {Trunc(dto.Department, 36)}", hPad, ty, textW, smallFs, textCol, center: true);
        if (!string.IsNullOrWhiteSpace(dto.EmployeeCode))
        {
            ty += lineH;
            AutoText(canvas, $"Código: {Trunc(dto.EmployeeCode, 24)}", hPad, ty, textW, smallFs, textCol, center: true);
        }

        using (var p = Fill(new SKColor(230, 238, 247)))
            canvas.DrawRect(0, bottomZoneTop, w, bottomZH, p);

        float qrInset = Math.Max(3f, w * 0.014f);
        float qrSz = Math.Min(bottomZH * 0.48f, w * 0.26f);
        float qrX = w - hPad - qrSz - qrInset;
        float qrY = bottomZoneTop + (bottomZH - qrSz) / 2f;
        if (settings.ShowQr)
            DrawQr(canvas, dto.QrToken, SKRect.Create(qrX, qrY, qrSz, qrSz));

        float leftW = qrX - hPad * 2f;
        float polIdFs = h * 0.027f;
        float lty = bottomZoneTop + bottomZH * 0.22f;
        AutoText(canvas, Trunc($"ID: {dto.CardNumber}", 26), hPad, lty, leftW, polIdFs, textCol, bold: true);

        float footerY = h - footerH;
        using (var fp = Fill(primary))
            canvas.DrawRect(0, footerY, w, footerH, fp);
        AutoText(canvas, "Credencial institucional",
            hPad, footerY + footerH * 0.65f, textW, footerH * 0.38f, SKColors.White, center: true);
    }

    private void DrawBackPortrait(SKCanvas canvas, int w, int h, StaffCardRenderDto dto, SchoolIdCardSetting settings)
    {
        var bg = Col(settings.BackgroundColor, SKColors.White);
        var primary = Col(settings.PrimaryColor, new SKColor(13, 110, 253));
        var textCol = Col(settings.TextColor, SKColors.Black);

        canvas.Clear(bg);
        DrawWatermark(canvas, dto.WatermarkBytes, w, h, 0.45f);

        float hPad = w * 0.05f;
        float contentW = w - hPad * 2f;
        float footerH = h * 0.12f;
        float footerY = h - footerH;

        float qrY = h * 0.03f;
        float qrBlockBottom = qrY;
        if (settings.ShowQr)
        {
            float verifySz = Math.Min(w, h) * 0.38f;
            float qrX = (w - verifySz) / 2f;
            DrawQr(canvas, dto.QrToken, SKRect.Create(qrX, qrY, verifySz, verifySz));
            qrBlockBottom = qrY + verifySz + h * 0.018f;
        }

        float nameFs = h * 0.048f;
        float smallFs = h * 0.036f;
        float lineH = h * 0.078f;
        float sLineH = h * 0.062f;
        float ty = qrBlockBottom + h * 0.02f + nameFs;

        AutoText(canvas, Trunc(dto.SchoolName, 50), hPad, ty, contentW, nameFs, textCol, bold: true, center: true);
        ty += lineH;
        if (ty + sLineH <= footerY - h * 0.02f)
        {
            AutoText(canvas, "Escanea el código para verificar la credencial",
                hPad, ty, contentW, smallFs * 0.88f, textCol, center: true);
            ty += sLineH;
        }
        if (ty + sLineH <= footerY - h * 0.02f)
        {
            AutoText(canvas, Trunc($"Credencial: {dto.CardNumber}", 32), hPad, ty, contentW, smallFs, textCol, center: true);
            ty += sLineH;
        }
        if (ty + sLineH <= footerY - h * 0.02f && !string.IsNullOrWhiteSpace(dto.IdCardPolicy))
        {
            var pol = dto.IdCardPolicy!.Trim();
            if (pol.Length > 120) pol = pol[..119] + "…";
            AutoText(canvas, pol, hPad, ty, contentW, smallFs * 0.82f, textCol, center: true);
            ty += sLineH;
        }
        if (ty + sLineH <= footerY - h * 0.02f && settings.ShowSchoolPhone && !string.IsNullOrWhiteSpace(dto.SchoolPhone))
            AutoText(canvas, $"Tel: {dto.SchoolPhone}", hPad, ty, contentW, smallFs, textCol, center: true);

        using (var p = Fill(primary))
            canvas.DrawRect(0, footerY, w, footerH, p);
        AutoText(canvas, "Personal autorizado",
            hPad, footerY + footerH * 0.65f, contentW, footerH * 0.38f, SKColors.White, center: true);
    }

    private void DrawQr(SKCanvas canvas, string? token, SKRect dest)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        var b = SafeQr(token);
        if (b == null) return;
        using var bmp = Decode(b);
        if (bmp != null) BmpDraw(canvas, bmp, dest);
    }

    private byte[]? SafeQr(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var signature = content.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || content.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? null
            : _qrSig;

        try { return QrHelper.GenerateQrPng(content, signature); }
        catch
        {
            try { return QrHelper.GenerateQrPng(content, null); }
            catch { return null; }
        }
    }

    private static void DrawWatermark(SKCanvas canvas, byte[]? bytes, int w, int h, float pct)
    {
        if (bytes == null || bytes.Length == 0) return;
        using var bmp = Decode(bytes);
        if (bmp == null) return;
        float sz = Math.Min(w, h) * pct;
        BmpDraw(canvas, bmp, FitRect(bmp.Width, bmp.Height, sz, sz, (w - sz) / 2f, (h - sz) / 2f));
    }

    private static void DrawPhoto(SKCanvas canvas, byte[]? photoBytes,
        float x, float y, float w, float h, SKColor textCol)
    {
        if (photoBytes != null && photoBytes.Length > 0)
        {
            using var bmp = Decode(photoBytes);
            if (bmp != null)
            {
                BmpDraw(canvas, bmp, FitRect(bmp.Width, bmp.Height, w, h, x, y));
                return;
            }
        }
        using var p = new SKPaint { Color = textCol, TextSize = Math.Max(16f, h * 0.12f), IsAntialias = true };
        float tw = p.MeasureText("FOTO");
        canvas.DrawText("FOTO", x + (w - tw) / 2f, y + (h + p.TextSize) / 2f, p);
    }

    private static void BmpDraw(SKCanvas canvas, SKBitmap bmp, SKRect dest)
    {
        using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High };
        canvas.DrawBitmap(bmp, dest, paint);
    }

    private static void AutoText(SKCanvas canvas, string text, float x, float y, float maxW,
        float fontSize, SKColor color, bool bold = false, bool center = false)
    {
        if (string.IsNullOrEmpty(text)) return;

        using var tf = SKTypeface.FromFamilyName(null, bold ? SKFontStyle.Bold : SKFontStyle.Normal);
        using var paint = new SKPaint { Color = color, TextSize = fontSize, IsAntialias = true, Typeface = tf };

        while (paint.MeasureText(text) > maxW && paint.TextSize > 10f)
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

    private static SKPaint Fill(SKColor c) => new() { Color = c, Style = SKPaintStyle.Fill };

    private static SKPaint Stroke(SKColor c, float width) =>
        new() { Color = c, Style = SKPaintStyle.Stroke, StrokeWidth = width };

    private static SKColor Col(string? hex, SKColor fallback = default)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try { return SKColor.Parse(hex.StartsWith('#') ? hex : '#' + hex); }
        catch { return fallback; }
    }

    private static string Trunc(string? t, int max) =>
        string.IsNullOrEmpty(t) ? "" : t.Length <= max ? t : t[..(max - 1)] + "…";

    private static SKBitmap? Decode(byte[]? b)
    {
        if (b == null || b.Length == 0) return null;
        try { return SKBitmap.Decode(b); } catch { return null; }
    }

    private static byte[] ToPng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
