using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SchoolManager.Services.Interfaces;
using SkiaSharp;

namespace SchoolManager.Services.Implementations;

/// <summary>
/// Fotos de usuario: solo Cloudinary (cualquier ambiente). Lectura sigue aceptando rutas locales históricas en <see cref="GetUserPhotoBytesAsync"/>.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private const string CloudinaryUserPhotosFolder = "users/photos";

    /// <summary>Límite del archivo guardado (comprimido si hace falta).</summary>
    private const int MaxFileSizeBytes = 2 * 1024 * 1024; // 2 MB

    /// <summary>Máximo que se acepta en la subida antes de procesar (evita abusos).</summary>
    private const int MaxIncomingUploadBytes = 12 * 1024 * 1024; // 12 MB

    private const int CompressMaxInitialSide = 2048;
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png"
    };

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly ICloudinaryService _cloudinary;
    private readonly IHttpBytesDownloadCache _httpBytesDownloadCache;
    private readonly string _basePath;

    public LocalFileStorageService(
        IWebHostEnvironment env,
        ILogger<LocalFileStorageService> logger,
        ICloudinaryService cloudinary,
        IHttpBytesDownloadCache httpBytesDownloadCache)
    {
        _env = env;
        _logger = logger;
        _cloudinary = cloudinary;
        _httpBytesDownloadCache = httpBytesDownloadCache;
        _basePath = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "uploads", "users");
    }

    public async Task<string> SaveUserPhotoAsync(IFormFile file, Guid userId)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("[FileStorage] Intento de guardar archivo vacío para UserId={UserId}", userId);
            throw new ArgumentException("El archivo no puede estar vacío.", nameof(file));
        }

        ValidateMimeType(file.ContentType, file.FileName);
        if (file.Length > MaxIncomingUploadBytes)
        {
            throw new InvalidOperationException(
                $"La imagen supera el máximo de subida ({MaxIncomingUploadBytes / (1024 * 1024)} MB). " +
                "Redúzcala o use otra foto.");
        }

        byte[] rawBytes;
        await using (var incomingMs = new MemoryStream((int)Math.Min(file.Length, MaxIncomingUploadBytes)))
        {
            await file.CopyToAsync(incomingMs);
            rawBytes = incomingMs.ToArray();
        }

        string safeFileName;
        byte[] bytesToWrite;

        if (rawBytes.Length <= MaxFileSizeBytes)
        {
            bytesToWrite = rawBytes;
            safeFileName = $"{userId:N}_{Guid.NewGuid():N}{GetExtensionFromMime(file.ContentType)}";
        }
        else
        {
            bytesToWrite = CompressImageToMaxBytes(rawBytes, MaxFileSizeBytes);
            safeFileName = $"{userId:N}_{Guid.NewGuid():N}.jpg";
            _logger.LogInformation(
                "[FileStorage] Foto comprimida UserId={UserId} {OriginalKb:F0} KB → {FinalKb:F0} KB",
                userId, rawBytes.Length / 1024.0, bytesToWrite.Length / 1024.0);
        }

        if (bytesToWrite.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                "No se pudo reducir la imagen por debajo de 2 MB. Pruebe con otra foto o menor resolución.");
        }

        await using (var cloudStream = new MemoryStream(bytesToWrite))
        {
            var mimeForCloud = safeFileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? "image/png"
                : "image/jpeg";
            var formFileForCloud = new FormFile(cloudStream, 0, bytesToWrite.Length, "photo", safeFileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = mimeForCloud
            };

            var cloudUrl = await _cloudinary.UploadImageAsync(formFileForCloud, CloudinaryUserPhotosFolder);
            if (!string.IsNullOrEmpty(cloudUrl))
            {
                _logger.LogInformation("[FileStorage] Foto en Cloudinary UserId={UserId}", userId);
                return cloudUrl;
            }
        }

        if (!_cloudinary.IsConfigured)
        {
            throw new InvalidOperationException(
                "Las fotos de usuario solo se guardan en Cloudinary. Defina valores reales: " +
                "CLOUDINARY_CLOUD_NAME, CLOUDINARY_API_KEY y CLOUDINARY_API_SECRET " +
                "(o Cloudinary__CloudName, Cloudinary__ApiKey, Cloudinary__ApiSecret en user-secrets / variables de entorno).");
        }

        throw new InvalidOperationException(
            "No se pudo subir la foto a Cloudinary. No se guarda copia en disco. Intente de nuevo.");
    }

    /// <summary>
    /// Decodifica la imagen y aplica EXIF Orientation (p. ej. fotos verticales desde móvil)
    /// para que los píxeles queden “de pie” antes de redimensionar o comprimir.
    /// </summary>
    private static SKBitmap DecodeBitmapWithOrientationApplied(byte[] source)
    {
        using var data = SKData.CreateCopy(source);
        using var codec = SKCodec.Create(data);
        var origin = SKEncodedOrigin.TopLeft;
        SKBitmap? bitmap = null;
        if (codec != null)
        {
            origin = codec.EncodedOrigin;
            bitmap = SKBitmap.Decode(codec);
        }

        bitmap ??= SKBitmap.Decode(source);
        if (bitmap == null)
            throw new InvalidOperationException("No se pudo leer la imagen. Use un JPEG o PNG válido.");

        if (origin == SKEncodedOrigin.TopLeft)
            return bitmap;

        try
        {
            return TransformBitmapToUpright(bitmap, origin);
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    private static SKBitmap TransformBitmapToUpright(SKBitmap original, SKEncodedOrigin origin)
    {
        var useW = original.Width;
        var useH = original.Height;
        Action<SKCanvas> transform = _ => { };

        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                transform = c => c.Scale(-1, 1, useW / 2f, useH / 2f);
                break;
            case SKEncodedOrigin.BottomRight:
                transform = c => c.RotateDegrees(180, useW / 2f, useH / 2f);
                break;
            case SKEncodedOrigin.BottomLeft:
                transform = c => c.Scale(1, -1, useW / 2f, useH / 2f);
                break;
            case SKEncodedOrigin.LeftTop:
                useW = original.Height;
                useH = original.Width;
                transform = c =>
                {
                    c.RotateDegrees(90, useW / 2f, useH / 2f);
                    c.Scale(useH * 1f / useW, -useW * 1f / useH, useW / 2f, useH / 2f);
                };
                break;
            case SKEncodedOrigin.RightTop:
                useW = original.Height;
                useH = original.Width;
                transform = c =>
                {
                    c.RotateDegrees(90, useW / 2f, useH / 2f);
                    c.Scale(useH * 1f / useW, useW * 1f / useH, useW / 2f, useH / 2f);
                };
                break;
            case SKEncodedOrigin.RightBottom:
                useW = original.Height;
                useH = original.Width;
                transform = c =>
                {
                    c.RotateDegrees(90, useW / 2f, useH / 2f);
                    c.Scale(-useH * 1f / useW, useW * 1f / useH, useW / 2f, useH / 2f);
                };
                break;
            case SKEncodedOrigin.LeftBottom:
                useW = original.Height;
                useH = original.Width;
                transform = c =>
                {
                    c.RotateDegrees(90, useW / 2f, useH / 2f);
                    c.Scale(-useH * 1f / useW, -useW * 1f / useH, useW / 2f, useH / 2f);
                };
                break;
            default:
                throw new InvalidOperationException($"Origen de imagen no soportado: {origin}.");
        }

        var info = new SKImageInfo(useW, useH, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High };
        transform(canvas);
        canvas.DrawBitmap(original, new SKRect(0, 0, useW, useH), paint);
        canvas.Flush();

        using var snapshot = surface.Snapshot();
        var result = SKBitmap.FromImage(snapshot);
        if (result == null)
            throw new InvalidOperationException("No se pudo corregir la orientación de la imagen.");
        return result;
    }

    private static byte[] CompressImageToMaxBytes(byte[] source, int maxBytes)
    {
        var decoded = DecodeBitmapWithOrientationApplied(source);
        if (decoded == null || decoded.Width < 1 || decoded.Height < 1)
        {
            throw new InvalidOperationException(
                "No se pudo leer la imagen. Use un JPEG o PNG válido.");
        }

        var bmp = DownscaleToMaxSide(decoded, CompressMaxInitialSide);
        try
        {
            while (true)
            {
                for (var quality = 88; quality >= 28; quality -= 10)
                {
                    var jpeg = EncodeJpeg(bmp, quality);
                    if (jpeg.Length <= maxBytes)
                        return jpeg;
                }

                var nw = Math.Max(400, (int)(bmp.Width * 0.75f));
                var nh = Math.Max(400, (int)(bmp.Height * 0.75f));
                if (nw >= bmp.Width && nh >= bmp.Height)
                {
                    nw = Math.Max(200, bmp.Width * 9 / 10);
                    nh = Math.Max(200, bmp.Height * 9 / 10);
                }

                if (nw < 200 || nh < 200)
                {
                    throw new InvalidOperationException(
                        "No se pudo comprimir la imagen lo suficiente para cumplir 2 MB.");
                }

                var next = bmp.Resize(new SKImageInfo(nw, nh), SKFilterQuality.Medium);
                if (next == null)
                {
                    throw new InvalidOperationException(
                        "No se pudo redimensionar la imagen. Pruebe con otro archivo.");
                }

                bmp.Dispose();
                bmp = next;
            }
        }
        finally
        {
            bmp.Dispose();
        }
    }

    private static SKBitmap DownscaleToMaxSide(SKBitmap decoded, int maxSide)
    {
        if (decoded.Width <= maxSide && decoded.Height <= maxSide)
            return decoded;

        var scale = maxSide / (float)Math.Max(decoded.Width, decoded.Height);
        var w = Math.Max(1, (int)(decoded.Width * scale));
        var h = Math.Max(1, (int)(decoded.Height * scale));
        var resized = decoded.Resize(new SKImageInfo(w, h), SKFilterQuality.Medium);
        if (resized == null)
        {
            decoded.Dispose();
            throw new InvalidOperationException("No se pudo redimensionar la imagen.");
        }

        decoded.Dispose();
        return resized;
    }

    private static byte[] EncodeJpeg(SKBitmap bitmap, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        if (data == null)
            throw new InvalidOperationException("No se pudo codificar la imagen como JPEG.");
        return data.ToArray();
    }

    public async Task DeleteUserPhotoAsync(string? photoUrl)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
            return;

        var trimmed = photoUrl.Trim();

        if (IsCloudinaryHttpUrl(trimmed))
        {
            if (TryGetCloudinaryPublicId(trimmed, out var publicId))
                await _cloudinary.DeleteImageAsync(publicId);
            else
                _logger.LogWarning("[FileStorage] URL Cloudinary sin public_id reconocible: {Url}", photoUrl);
            return;
        }

        try
        {
            var fileName = Path.GetFileName(trimmed);
            if (string.IsNullOrEmpty(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                _logger.LogWarning("[FileStorage] Nombre de archivo inválido para eliminar: {Url}", photoUrl);
                return;
            }

            var fullPath = Path.GetFullPath(Path.Combine(_basePath, fileName));
            if (!fullPath.StartsWith(Path.GetFullPath(_basePath), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[FileStorage] Path traversal al eliminar: {Path}", fullPath);
                return;
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("[FileStorage] Foto eliminada: {Path}", fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FileStorage] Error eliminando foto: {Url}", photoUrl);
        }
    }

    public async Task<byte[]?> GetUserPhotoBytesAsync(string? photoUrl)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
            return null;

        var trimmed = photoUrl.Trim();

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return await _httpBytesDownloadCache.GetOrDownloadAsync(
                    trimmed,
                    6 * 1024 * 1024,
                    TimeSpan.FromSeconds(45),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FileStorage] Error descargando foto remota: {Url}", photoUrl);
                return null;
            }
        }

        try
        {
            var fileName = Path.GetFileName(trimmed.TrimStart('/').Replace("uploads/users/", "", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return null;

            var fullPath = Path.GetFullPath(Path.Combine(_basePath, fileName));
            if (!fullPath.StartsWith(Path.GetFullPath(_basePath), StringComparison.OrdinalIgnoreCase))
                return null;

            if (!File.Exists(fullPath))
                return null;

            return await File.ReadAllBytesAsync(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FileStorage] Error leyendo foto: {Url}", photoUrl);
            return null;
        }
    }

    private static bool IsCloudinaryHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Host.Equals("res.cloudinary.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Obtiene el public_id de Cloudinary desde la URL segura devuelta por la API de subida.
    /// </summary>
    private static bool TryGetCloudinaryPublicId(string url, out string publicId)
    {
        publicId = "";
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;
        if (!uri.Host.Equals("res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        var uploadIdx = parts.FindIndex(p => p.Equals("upload", StringComparison.OrdinalIgnoreCase));
        if (uploadIdx < 0 || uploadIdx + 1 >= parts.Count)
            return false;

        var tail = parts.Skip(uploadIdx + 1).ToList();
        if (tail.Count == 0)
            return false;

        if (tail[0].Length > 1 && tail[0][0] == 'v' && tail[0].Skip(1).All(char.IsDigit))
            tail.RemoveAt(0);

        if (tail.Count == 0)
            return false;

        publicId = string.Join("/", tail);
        publicId = Regex.Replace(publicId, @"\.(jpe?g|png)$", "", RegexOptions.IgnoreCase);
        return publicId.Length > 0;
    }

    private static void ValidateMimeType(string? contentType, string? fileName)
    {
        var mime = contentType?.Split(';').FirstOrDefault()?.Trim();
        if (string.IsNullOrEmpty(mime) || !AllowedMimeTypes.Contains(mime))
        {
            throw new InvalidOperationException(
                "Solo se permiten imágenes JPEG o PNG. Tipo recibido: " + (mime ?? "desconocido"));
        }

        var ext = Path.GetExtension(fileName ?? "");
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Extensión de archivo no permitida. Use .jpg o .png.");
        }
    }

    private static string GetExtensionFromMime(string contentType)
    {
        var mime = contentType?.Split(';').FirstOrDefault()?.Trim();
        return string.Equals(mime, "image/png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
    }
}
