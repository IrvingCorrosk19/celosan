using System.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.Services.Security;
using SchoolManager.Options;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace SchoolManager.Services.Implementations;

/// <summary>
/// Genera el PDF de carnets estudiantiles.
/// Toda la lógica de renderizado gráfico vive en <see cref="IStudentIdCardImageService"/>;
/// este servicio solo coordina la carga de datos/imágenes y envuelve los PNG en un PDF
/// de tamaño exacto CR80 — sin layouts dinámicos ni riesgo de "conflicting size constraints".
/// </summary>
public class StudentIdCardPdfService : IStudentIdCardPdfService
{
    private readonly SchoolDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly HttpClient _http;
    private readonly ILogger<StudentIdCardPdfService> _logger;
    private readonly IQrSignatureService _qrSignatureService;
    private readonly IWebHostEnvironment _environment;
    private readonly IStudentIdCardImageService _imageService;
    private readonly IOptions<StudentIdCardOptions> _studentIdCardOptions;

    private const int MaxImageDownloadBytes = 5 * 1024 * 1024;
    private static readonly TimeSpan ImageDownloadTimeout = TimeSpan.FromSeconds(10);
    private const int MaxAllergiesCharsOnCard = 100;

    public StudentIdCardPdfService(
        SchoolDbContext context,
        IFileStorageService fileStorage,
        IHttpClientFactory httpClientFactory,
        ILogger<StudentIdCardPdfService> logger,
        IQrSignatureService qrSignatureService,
        IWebHostEnvironment environment,
        IStudentIdCardImageService imageService,
        IOptions<StudentIdCardOptions> studentIdCardOptions)
    {
        _context      = context;
        _fileStorage  = fileStorage;
        _http         = httpClientFactory.CreateClient();
        _logger       = logger;
        _qrSignatureService = qrSignatureService;
        _environment  = environment;
        _imageService = imageService;
        _studentIdCardOptions = studentIdCardOptions;
    }

    public async Task<byte[]> GenerateCardPdfAsync(Guid studentId, Guid createdBy)
    {
        try
        {
            _logger.LogInformation(
                "[StudentIdCardPdf] GenerateCardPdfAsync inicio StudentId={StudentId} CreatedBy={CreatedBy}",
                studentId, createdBy);

            // 1. Escuela del estudiante
            var studentSchoolId = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == studentId)
                .Select(u => u.SchoolId)
                .FirstOrDefaultAsync();

            if (!studentSchoolId.HasValue)
                throw new Exception("El estudiante no tiene escuela asignada.");

            var school = await _context.Schools
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == studentSchoolId.Value)
                ?? throw new Exception("No se encontró la institución del estudiante.");

            // 2. Settings de carnet
            var settings = await _context.Set<SchoolIdCardSetting>()
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.SchoolId == school.Id)
                ?? new SchoolIdCardSetting
                {
                    SchoolId         = school.Id,
                    TemplateKey      = "default_v1",
                    BackgroundColor  = "#FFFFFF",
                    PrimaryColor     = "#0D6EFD",
                    TextColor        = "#111111",
                    ShowQr           = true,
                    ShowPhoto        = true,
                    ShowSchoolPhone  = true,
                    Orientation      = "Vertical",
                    ShowWatermark    = true
                };

            // 3. Campos de plantilla personalizada
            var fields = await _context.Set<IdCardTemplateField>()
                .AsNoTracking()
                .Where(x => x.SchoolId == school.Id && x.IsEnabled)
                .ToListAsync();

            // 4. Carnet + token (transacción serializable — CONC-2 fix)
            var renderDto = await BuildStudentCardDtoAsync(studentId, createdBy, school.Name);

            renderDto.SchoolName   = school.Name;
            renderDto.SchoolPhone  = school.Phone;
            renderDto.IdCardPolicy = school.IdCardPolicy;
            renderDto.PolicyNumber = string.IsNullOrWhiteSpace(school.PolicyNumber)
                ? "POLIZA-PENDIENTE-CONFIGURACION"
                : school.PolicyNumber.Trim();
            if (string.IsNullOrWhiteSpace(renderDto.AcademicYear))
                renderDto.AcademicYear = "NO DEFINIDO";
            if (!string.IsNullOrWhiteSpace(renderDto.Allergies) &&
                renderDto.Allergies.Length > MaxAllergiesCharsOnCard)
                renderDto.Allergies = renderDto.Allergies[..(MaxAllergiesCharsOnCard - 1)] + "…";

            // 5. Cargar imágenes
            if (!string.IsNullOrWhiteSpace(school.LogoUrl))
                renderDto.LogoBytes = await SafeDownloadBytesAsync(school.LogoUrl);

            if (settings.ShowWatermark && renderDto.LogoBytes != null)
                renderDto.WatermarkBytes = CreateWatermarkImage(renderDto.LogoBytes, 0.14f);

            if (settings.ShowSecondaryLogo && !string.IsNullOrWhiteSpace(settings.SecondaryLogoUrl))
                renderDto.SecondaryLogoBytes = await SafeDownloadBytesAsync(settings.SecondaryLogoUrl);

            if (settings.ShowPhoto && !string.IsNullOrWhiteSpace(renderDto.PhotoUrl))
                renderDto.PhotoBytes = await _fileStorage.GetUserPhotoBytesAsync(renderDto.PhotoUrl);

            // 6. Renderizar PNG(s) via SkiaSharp (sin layouts dinámicos, sin overflow)
            IReadOnlyList<IdCardTemplateField>? customFields = fields.Count > 0 ? fields : null;

            var frontPng = _imageService.GenerateCardImage(renderDto, settings, customFields);
            byte[]? backPng = (settings.ShowQr && customFields == null)
                ? _imageService.GenerateCardBackImage(renderDto, settings)
                : null;

            // 7. PDF wrapper — solo embebe imágenes PNG en página de tamaño CR80 exacto
            var (widthMm, heightMm) = _imageService.GetCardMmDimensions(settings);
            float pageW = backPng != null ? widthMm * 2f + 2f : widthMm;
            float pageH = heightMm;

            QuestPDF.Settings.License       = LicenseType.Community;
            QuestPDF.Settings.EnableDebugging = false;

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(pageW, pageH, Unit.Millimetre);
                    page.Margin(0);

                    if (backPng != null)
                    {
                        page.Content().Row(row =>
                        {
                            row.Spacing(2f, Unit.Millimetre);
                            row.ConstantItem(widthMm, Unit.Millimetre).Image(frontPng).FitArea();
                            row.ConstantItem(widthMm, Unit.Millimetre).Image(backPng).FitArea();
                        });
                    }
                    else
                    {
                        page.Content().Image(frontPng).FitArea();
                    }
                });
            }).GeneratePdf();

            _logger.LogInformation(
                "[StudentIdCardPdf] GenerateCardPdfAsync OK StudentId={StudentId} SchoolId={SchoolId}",
                studentId, school.Id);
            return pdf;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[StudentIdCardPdf] GenerateCardPdfAsync error StudentId={StudentId}: {Message}",
                studentId, ex.Message);
            throw;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DB: construye el DTO de renderizado asegurando carnet + token activos
    // CONC-2: transacción serializable — previene duplicados concurrentes
    // ══════════════════════════════════════════════════════════════════════════
    private async Task<StudentCardRenderDto> BuildStudentCardDtoAsync(
        Guid studentId, Guid createdBy, string schoolName)
    {
        _logger.LogInformation("[StudentIdCardPdf] BuildStudentCardDtoAsync StudentId={StudentId}", studentId);

        var student = await _context.Users
            .Include(u => u.StudentAssignments).ThenInclude(a => a.Grade)
            .Include(u => u.StudentAssignments).ThenInclude(a => a.Group)
            .Include(u => u.StudentAssignments).ThenInclude(a => a.Shift)
            .Include(u => u.StudentAssignments).ThenInclude(a => a.AcademicYear)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == studentId)
            ?? throw new Exception("Estudiante no encontrado.");

        var assignment = ActiveStudentAssignmentHelper.PickForDisplay(student.StudentAssignments)
            ?? throw new Exception("El estudiante no tiene asignación activa.");

        // PAY-GATE (última línea de defensa)
        var payment = await _context.StudentPaymentAccesses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.StudentId == studentId);

        if (payment == null || payment.CarnetStatus != "Pagado")
            throw new Exception("El estudiante no ha pagado el carnet.");

        using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var card = await _context.StudentIdCards
                .FirstOrDefaultAsync(c => c.StudentId == studentId && c.Status == "active");

            if (card == null)
            {
                card = new StudentIdCard
                {
                    StudentId  = studentId,
                    CardNumber = CardNumberHelper.Generate(studentId),
                    IssuedAt   = DateTime.UtcNow,
                    ExpiresAt  = DateTime.UtcNow.AddYears(1),
                    Status     = "active"
                };
                _context.StudentIdCards.Add(card);
            }

            var token = await _context.StudentQrTokens
                .FirstOrDefaultAsync(t => t.StudentId == studentId && !t.IsRevoked &&
                    (t.ExpiresAt == null || t.ExpiresAt > DateTime.UtcNow));

            if (token == null)
            {
                token = new StudentQrToken
                {
                    StudentId = studentId,
                    Token     = Guid.NewGuid().ToString("N"),
                    ExpiresAt = DateTime.UtcNow.AddMonths(StudentIdCardService.QrTokenValidityMonths),
                    IsRevoked = false
                };
                _context.StudentQrTokens.Add(token);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var publicBase = _studentIdCardOptions.Value.PublicBaseUrl;
            var emergencyUrl = CarnetEmergencyInfoLink.BuildPublicUrl(publicBase, studentId, _qrSignatureService);

            return new StudentCardRenderDto
            {
                StudentId              = studentId,
                FullName               = $"{student.Name} {student.LastName}",
                DocumentId             = student.DocumentId,
                Grade                  = assignment.Grade?.Name ?? "",
                Group                  = assignment.Group?.Name ?? "",
                Shift                  = assignment.Shift?.Name ?? "",
                CardNumber             = card.CardNumber,
                QrToken                = token.Token,
                EmergencyInfoPageUrl   = emergencyUrl,
                PhotoUrl               = student.PhotoUrl,
                Allergies              = student.Allergies,
                EmergencyContactName   = student.EmergencyContactName,
                EmergencyContactPhone  = student.EmergencyContactPhone,
                EmergencyRelationship  = student.EmergencyRelationship,
                AcademicYear           = assignment.AcademicYear?.Name
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UTILIDADES
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Descarga segura de imagen con timeout (10s) y límite de tamaño (5MB).
    /// BUG-4: soporta rutas locales /uploads/... vía WebRootPath.
    /// </summary>
    private async Task<byte[]?> SafeDownloadBytesAsync(string url)
    {
        try
        {
            if (url.StartsWith("http://") || url.StartsWith("https://"))
            {
                using var cts   = new CancellationTokenSource(ImageDownloadTimeout);
                var bytes = await _http.GetByteArrayAsync(url, cts.Token);
                if (bytes.Length > MaxImageDownloadBytes)
                {
                    _logger.LogWarning(
                        "[StudentIdCardPdf] Imagen {Url} supera {Max} bytes, ignorada.", url, MaxImageDownloadBytes);
                    return null;
                }
                return bytes;
            }

            if (url.StartsWith("/"))
            {
                var fullPath = Path.Combine(
                    _environment.WebRootPath,
                    url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(fullPath)) return await File.ReadAllBytesAsync(fullPath);
                _logger.LogWarning("[StudentIdCardPdf] Archivo local no encontrado: {Path}", fullPath);
                return null;
            }

            if (!url.Contains('/') && !url.Contains('\\'))
            {
                var schoolsPath = Path.Combine(_environment.WebRootPath, "uploads", "schools",
                    url.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(schoolsPath)) return await File.ReadAllBytesAsync(schoolsPath);

                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads",
                    url.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(uploadsPath)) return await File.ReadAllBytesAsync(uploadsPath);

                _logger.LogWarning("[StudentIdCardPdf] Logo bare-filename no encontrado: {File}", url);
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[StudentIdCardPdf] Timeout descargando imagen: {Url}", url);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[StudentIdCardPdf] Error descargando imagen: {Url}", url);
            return null;
        }
    }

    /// <summary>Genera logo semitransparente para usar como marca de agua.</summary>
    private static byte[]? CreateWatermarkImage(byte[]? logoBytes, float opacity = 0.14f)
    {
        if (logoBytes == null || logoBytes.Length == 0 || opacity <= 0 || opacity >= 1) return null;
        try
        {
            using var data     = SKData.CreateCopy(logoBytes);
            using var original = SKImage.FromEncodedData(data);
            if (original == null) return null;

            var info = new SKImageInfo(original.Width, original.Height,
                SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            if (surface == null) return null;

            using var wmCanvas = surface.Canvas;
            using var paint    = new SKPaint
            {
                ColorFilter = SKColorFilter.CreateBlendMode(
                    SKColors.White.WithAlpha((byte)(opacity * 255)),
                    SKBlendMode.DstIn)
            };
            wmCanvas.DrawImage(original, 0, 0, paint);

            using var snapshot = surface.Snapshot();
            using var encoded  = snapshot.Encode(SKEncodedImageFormat.Png, 100);
            if (encoded == null) return null;
            using var stream   = new MemoryStream();
            encoded.SaveTo(stream);
            return stream.ToArray();
        }
        catch { return null; }
    }
}
