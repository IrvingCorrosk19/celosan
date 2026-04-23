using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SchoolManager.Helpers;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Controllers;

[Authorize]
public class FileController : Controller
{
    private readonly ISuperAdminService _superAdminService;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<FileController> _logger;

    public FileController(
        ISuperAdminService superAdminService,
        IFileStorageService fileStorage,
        ILogger<FileController> logger)
    {
        _superAdminService = superAdminService;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetSchoolLogo(string logoUrl)
    {
        if (string.IsNullOrEmpty(logoUrl))
        {
            // Retornar logo por defecto si no hay logoUrl
            var defaultLogoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logoIPT.jpg");
            if (System.IO.File.Exists(defaultLogoPath))
            {
                var defaultBytes = await System.IO.File.ReadAllBytesAsync(defaultLogoPath);
                return File(defaultBytes, "image/jpeg");
            }
            return NotFound();
        }

        var trimmed = logoUrl.Trim();
        // GetLogoAsync devuelve null para https (pensando en src directo), pero las vistas usan este endpoint.
        // Sin redirección, siempre caía al logo por defecto y el carnet / impresión masiva no mostraban el logo real.
        if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var abs)
                && abs.Host.Equals("res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(trimmed);
            }
        }

        try
        {
            var bytes = await _superAdminService.GetLogoAsync(logoUrl);
            if (bytes == null)
            {
                // Fallback a logo por defecto
                var defaultLogoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logoIPT.jpg");
                if (System.IO.File.Exists(defaultLogoPath))
                {
                    var defaultBytes = await System.IO.File.ReadAllBytesAsync(defaultLogoPath);
                    return File(defaultBytes, "image/jpeg");
                }
                return NotFound();
            }

            return File(bytes, "image/jpeg");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo logo de escuela: {logoUrl}", logoUrl);
            
            // Fallback a logo por defecto en caso de error
            try
            {
                var defaultLogoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logoIPT.jpg");
                if (System.IO.File.Exists(defaultLogoPath))
                {
                    var defaultBytes = await System.IO.File.ReadAllBytesAsync(defaultLogoPath);
                    return File(defaultBytes, "image/jpeg");
                }
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Error obteniendo logo por defecto");
            }
            
            return NotFound();
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetUserAvatar(string avatarUrl)
    {
        if (string.IsNullOrEmpty(avatarUrl))
        {
            return NotFound();
        }

        try
        {
            var bytes = await _superAdminService.GetAvatarAsync(avatarUrl);
            if (bytes == null)
            {
                return NotFound();
            }

            return File(bytes, "image/jpeg");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo avatar de usuario: {avatarUrl}", avatarUrl);
            return NotFound();
        }
    }

    /// <summary>
    /// Foto de perfil/carnet: misma idea que GetSchoolLogo — siempre devuelve una imagen (por defecto si falta el archivo).
    /// URL de Cloudinary → redirección al CDN. Rutas locales → bytes desde disco vía IFileStorageService.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetUserPhoto(string? photoUrl, int? carnetEdge = null, string? variant = null)
    {
        var placeholderSvg = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "user-photo-placeholder.svg");
        var fallbackJpeg = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logoIPT.jpg");

        async Task<IActionResult> PlaceholderAsync()
        {
            if (System.IO.File.Exists(placeholderSvg))
                return PhysicalFile(placeholderSvg, "image/svg+xml");
            if (System.IO.File.Exists(fallbackJpeg))
                return File(await System.IO.File.ReadAllBytesAsync(fallbackJpeg), "image/jpeg");
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(photoUrl))
            return await PlaceholderAsync();

        var trimmed = photoUrl.Trim();

        if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
                return await PlaceholderAsync();
            if (!uri.Host.Equals("res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("GetUserPhoto: URL externa no permitida (solo Cloudinary o rutas locales): {Host}", uri.Host);
                return await PlaceholderAsync();
            }

            if (carnetEdge is >= 120 and <= 800)
            {
                var hiRes = CloudinaryCarnetDeliveryUrl.WithCarnetFaceCrop(trimmed, carnetEdge.Value);
                return Redirect(hiRes);
            }

            if (string.Equals(variant, "thumb", StringComparison.OrdinalIgnoreCase))
            {
                var thumbUrl = CloudinaryTransformUrl.InsertAfterUpload(trimmed, UserPhotoDeliveryTransforms.ListThumbnail);
                return Redirect(thumbUrl);
            }

            return Redirect(trimmed);
        }

        try
        {
            var bytes = await _fileStorage.GetUserPhotoBytesAsync(trimmed);
            if (bytes == null || bytes.Length == 0)
                return await PlaceholderAsync();

            var contentType = trimmed.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
            Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
            {
                Private = true,
                MaxAge    = TimeSpan.FromMinutes(15)
            };
            return File(bytes, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo foto de usuario: {PhotoUrl}", photoUrl);
            return await PlaceholderAsync();
        }
    }

    [HttpGet]
    public IActionResult DownloadTemplate(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return NotFound();
        }

        try
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "descargables", fileName);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            
            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error descargando plantilla: {fileName}", fileName);
            return NotFound();
        }
    }
} 