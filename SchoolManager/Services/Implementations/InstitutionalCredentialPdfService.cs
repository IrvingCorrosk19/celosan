using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Options;
using SchoolManager.Services.Interfaces;
using SchoolManager.Services.Security;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace SchoolManager.Services.Implementations;

public class InstitutionalCredentialPdfService : IInstitutionalCredentialPdfService
{
    private readonly SchoolDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IHttpBytesDownloadCache _httpBytesDownloadCache;
    private readonly ILogger<InstitutionalCredentialPdfService> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly IInstitutionalCredentialImageService _imageService;
    private readonly IQrSignatureService _qrSignatureService;
    private readonly IOptions<InstitutionalCredentialOptions> _credentialOptions;

    private const int MaxImageDownloadBytes = 5 * 1024 * 1024;
    private static readonly TimeSpan ImageDownloadTimeout = TimeSpan.FromSeconds(10);

    public InstitutionalCredentialPdfService(
        SchoolDbContext context,
        IFileStorageService fileStorage,
        IHttpBytesDownloadCache httpBytesDownloadCache,
        ILogger<InstitutionalCredentialPdfService> logger,
        IWebHostEnvironment environment,
        IInstitutionalCredentialImageService imageService,
        IQrSignatureService qrSignatureService,
        IOptions<InstitutionalCredentialOptions> credentialOptions)
    {
        _context = context;
        _fileStorage = fileStorage;
        _httpBytesDownloadCache = httpBytesDownloadCache;
        _logger = logger;
        _environment = environment;
        _imageService = imageService;
        _qrSignatureService = qrSignatureService;
        _credentialOptions = credentialOptions;
    }

    public async Task<byte[]> GenerateCardPdfAsync(Guid staffUserId, Guid createdBy)
    {
        _logger.LogInformation(
            "[InstitutionalCredentialPdf] GenerateCardPdfAsync UserId={UserId} CreatedBy={CreatedBy}",
            staffUserId, createdBy);

        var userSchoolId = await StaffInstitutionalRoleFilter.WhereIsInstitutionalStaff(_context.Users.AsNoTracking())
            .Where(u => u.Id == staffUserId)
            .Select(u => u.SchoolId)
            .FirstOrDefaultAsync();

        if (!userSchoolId.HasValue)
            throw new InvalidOperationException("El usuario no es personal institucional o no tiene escuela asignada.");

        var school = await _context.Schools.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == userSchoolId.Value)
            ?? throw new InvalidOperationException("No se encontró la institución.");

        var settings = await _context.Set<SchoolIdCardSetting>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.SchoolId == school.Id)
            ?? new SchoolIdCardSetting
            {
                SchoolId = school.Id,
                TemplateKey = "default_v1",
                BackgroundColor = "#FFFFFF",
                PrimaryColor = "#0D6EFD",
                TextColor = "#111111",
                ShowQr = true,
                ShowPhoto = true,
                ShowSchoolPhone = true,
                Orientation = "Vertical",
                ShowWatermark = true,
                UseModernLayout = true
            };

        settings.UseModernLayout = true;
        settings.Orientation = "Vertical";

        var renderDto = await BuildStaffCardDtoAsync(staffUserId, school.Name);
        renderDto.SchoolPhone = school.Phone;
        renderDto.IdCardPolicy = school.IdCardPolicy;

        if (!string.IsNullOrWhiteSpace(school.LogoUrl))
            renderDto.LogoBytes = await SafeDownloadBytesAsync(school.LogoUrl);

        if (settings.ShowWatermark && renderDto.LogoBytes != null)
            renderDto.WatermarkBytes = CreateWatermarkImage(renderDto.LogoBytes, 0.14f);

        if (settings.ShowSecondaryLogo && !string.IsNullOrWhiteSpace(settings.SecondaryLogoUrl))
            renderDto.SecondaryLogoBytes = await SafeDownloadBytesAsync(settings.SecondaryLogoUrl);

        if (settings.ShowPhoto && !string.IsNullOrWhiteSpace(renderDto.PhotoUrl))
            renderDto.PhotoBytes = await _fileStorage.GetUserPhotoBytesAsync(renderDto.PhotoUrl);

        var frontPng = _imageService.GenerateFrontPng(renderDto, settings);
        byte[]? backPng = settings.ShowQr ? _imageService.GenerateBackPng(renderDto, settings) : null;

        var (widthMm, heightMm) = _imageService.GetPortraitCardMmDimensions();
        float pageW = backPng != null ? widthMm * 2f + 2f : widthMm;
        float pageH = heightMm;

        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.EnableDebugging = false;

        return Document.Create(container =>
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
                    page.Content().Image(frontPng).FitArea();
            });
        }).GeneratePdf();
    }

    private async Task<StaffCardRenderDto> BuildStaffCardDtoAsync(Guid userId, string schoolName)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var card = await _context.Set<InstitutionalCredentialCard>()
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Status == "active");

            if (card == null)
            {
                card = new InstitutionalCredentialCard
                {
                    UserId = userId,
                    CardNumber = InstitutionalCardNumberHelper.Generate(userId),
                    IssuedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddYears(1),
                    Status = "active"
                };
                _context.Set<InstitutionalCredentialCard>().Add(card);
            }

            var token = await _context.Set<StaffQrToken>()
                .FirstOrDefaultAsync(t => t.UserId == userId && !t.IsRevoked &&
                    (t.ExpiresAt == null || t.ExpiresAt > DateTime.UtcNow));

            if (token == null)
            {
                token = new StaffQrToken
                {
                    UserId = userId,
                    Token = Guid.NewGuid().ToString("N"),
                    ExpiresAt = DateTime.UtcNow.AddMonths(InstitutionalCredentialService.QrTokenValidityMonths),
                    IsRevoked = false
                };
                _context.Set<StaffQrToken>().Add(token);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var user = await StaffInstitutionalRoleFilter.WhereIsInstitutionalStaff(_context.Users.AsNoTracking())
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    u.Name,
                    u.LastName,
                    u.DocumentId,
                    u.Role,
                    u.PhotoUrl,
                    JobTitle = _context.Set<StaffInstitutionalProfile>()
                        .Where(p => p.UserId == u.Id).Select(p => p.JobTitle).FirstOrDefault(),
                    Department = _context.Set<StaffInstitutionalProfile>()
                        .Where(p => p.UserId == u.Id).Select(p => p.Department).FirstOrDefault(),
                    EmployeeCode = _context.Set<StaffInstitutionalProfile>()
                        .Where(p => p.UserId == u.Id).Select(p => p.EmployeeCode).FirstOrDefault()
                })
                .FirstAsync();

            var baseUrl = _credentialOptions.Value.PublicBaseUrl?.TrimEnd('/');
            var qrContent = StaffMemberPublicLink.BuildPublicUrl(baseUrl, token.Token, _qrSignatureService)
                ?? token.Token;

            return new StaffCardRenderDto
            {
                UserId = userId,
                FullName = $"{user.Name} {user.LastName}",
                DocumentId = user.DocumentId,
                RoleDisplay = StaffInstitutionalRoleFilter.FormatRoleDisplay(user.Role),
                JobTitle = string.IsNullOrWhiteSpace(user.JobTitle) ? "—" : user.JobTitle!,
                Department = string.IsNullOrWhiteSpace(user.Department) ? "—" : user.Department!,
                EmployeeCode = user.EmployeeCode,
                CardNumber = card.CardNumber,
                QrToken = qrContent,
                SchoolName = schoolName,
                PhotoUrl = user.PhotoUrl
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<byte[]?> SafeDownloadBytesAsync(string url)
    {
        try
        {
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = await _httpBytesDownloadCache.GetOrDownloadAsync(
                    url, MaxImageDownloadBytes, ImageDownloadTimeout, CancellationToken.None);
                if (bytes == null || bytes.Length > MaxImageDownloadBytes)
                    return null;
                return bytes;
            }

            if (url.StartsWith('/'))
            {
                var fullPath = Path.Combine(
                    _environment.WebRootPath,
                    url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(fullPath)) return await File.ReadAllBytesAsync(fullPath);
            }

            if (!url.Contains('/') && !url.Contains('\\'))
            {
                var schoolsPath = Path.Combine(_environment.WebRootPath, "uploads", "schools",
                    url.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(schoolsPath)) return await File.ReadAllBytesAsync(schoolsPath);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[InstitutionalCredentialPdf] Error descargando imagen: {Url}", url);
            return null;
        }
    }

    private static byte[]? CreateWatermarkImage(byte[]? logoBytes, float opacity = 0.14f)
    {
        if (logoBytes == null || logoBytes.Length == 0 || opacity <= 0 || opacity >= 1) return null;
        try
        {
            using var data = SKData.CreateCopy(logoBytes);
            using var original = SKImage.FromEncodedData(data);
            if (original == null) return null;

            var info = new SKImageInfo(original.Width, original.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            if (surface == null) return null;

            using var wmCanvas = surface.Canvas;
            using var paint = new SKPaint
            {
                ColorFilter = SKColorFilter.CreateBlendMode(
                    SKColors.White.WithAlpha((byte)(opacity * 255)),
                    SKBlendMode.DstIn)
            };
            wmCanvas.DrawImage(original, 0, 0, paint);

            using var snapshot = surface.Snapshot();
            using var encoded = snapshot.Encode(SKEncodedImageFormat.Png, 100);
            if (encoded == null) return null;
            using var stream = new MemoryStream();
            encoded.SaveTo(stream);
            return stream.ToArray();
        }
        catch { return null; }
    }
}
