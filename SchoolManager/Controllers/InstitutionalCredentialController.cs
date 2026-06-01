using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.Services.Security;
using SchoolManager.ViewModels;

namespace SchoolManager.Controllers;

[Authorize(Roles = "SuperAdmin,superadmin")]
[Route("InstitutionalCredential")]
public class InstitutionalCredentialController : Controller
{
    private readonly IInstitutionalCredentialService _service;
    private readonly IInstitutionalCredentialPdfService _pdfService;
    private readonly IInstitutionalCredentialHtmlCaptureService _htmlCapture;
    private readonly SchoolDbContext _context;
    private readonly IQrSignatureService _qrSignatureService;
    private readonly ILogger<InstitutionalCredentialController> _logger;

    public InstitutionalCredentialController(
        IInstitutionalCredentialService service,
        IInstitutionalCredentialPdfService pdfService,
        IInstitutionalCredentialHtmlCaptureService htmlCapture,
        SchoolDbContext context,
        IQrSignatureService qrSignatureService,
        ILogger<InstitutionalCredentialController> logger)
    {
        _service = service;
        _pdfService = pdfService;
        _htmlCapture = htmlCapture;
        _context = context;
        _qrSignatureService = qrSignatureService;
        _logger = logger;
    }

    /// <summary>Página pública al escanear el QR de la credencial (enlace firmado ?t=).</summary>
    [AllowAnonymous]
    [EnableRateLimiting("ScanApiPolicy")]
    [HttpGet("member")]
    public async Task<IActionResult> PublicMemberProfile([FromQuery] string? t)
    {
        if (!StaffMemberPublicLink.TryResolveRawTokenFromSignedQuery(t, _qrSignatureService, out var rawToken))
            return InvalidPublicMemberProfile();

        return await RenderPublicMemberProfileAsync(rawToken!);
    }

    /// <summary>Ruta alternativa: /member/{token} con token de staff_qr_tokens (validación en BD).</summary>
    [AllowAnonymous]
    [EnableRateLimiting("ScanApiPolicy")]
    [HttpGet("member/{token}")]
    public Task<IActionResult> PublicMemberProfileByPath(string token) =>
        RenderPublicMemberProfileAsync(token);

    [HttpGet("ui")]
    public IActionResult Index() => View();

    [HttpGet("ui/generate/{userId}")]
    public async Task<IActionResult> GenerateView(Guid userId)
    {
        var row = await StaffInstitutionalRoleFilter.WhereIsInstitutionalStaff(_context.Users.AsNoTracking())
            .Where(u => u.Id == userId)
            .Select(u => new { u.SchoolId, u.DocumentId })
            .FirstOrDefaultAsync();

        if (row == null)
        {
            return View("Generate", new InstitutionalCredentialGenerateViewModel
            {
                UserId = userId,
                UserNotFound = true,
                SchoolName = "—"
            });
        }

        if (!row.SchoolId.HasValue)
        {
            return View("Generate", new InstitutionalCredentialGenerateViewModel
            {
                UserId = userId,
                NotEligible = true,
                NotEligibleReason = "El usuario no tiene escuela asignada.",
                SchoolName = "—"
            });
        }

        var schoolId = row.SchoolId.Value;
        var bundle = await _context.Schools.AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.Id == schoolId)
            .Select(s => new
            {
                School = s,
                CardSettings = _context.Set<SchoolIdCardSetting>().AsNoTracking().IgnoreQueryFilters()
                    .Where(x => x.SchoolId == s.Id)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();

        var vm = InstitutionalCredentialGenerateViewModel.ForUser(
            userId,
            bundle?.School,
            bundle?.CardSettings);
        vm.DocumentId = row.DocumentId;
        var siteBase = $"{Request.Scheme}://{Request.Host}";
        vm.Card = await _service.GetCurrentCardAsync(userId, siteBase);
        return View("Generate", vm);
    }

    [HttpGet("ui/print/{userId}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Print(Guid userId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var currentUserId))
            return Unauthorized("Usuario no autenticado.");

        var eligible = await StaffInstitutionalRoleFilter.WhereIsInstitutionalStaff(_context.Users.AsNoTracking())
            .AnyAsync(u => u.Id == userId && u.SchoolId != null);
        if (!eligible)
        {
            return new ContentResult
            {
                Content = "Usuario no elegible para credencial institucional.",
                ContentType = "text/plain; charset=utf-8",
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        try
        {
            var url = $"{Request.Scheme}://{Request.Host}/InstitutionalCredential/ui/generate/{userId}";
            try
            {
                var pdf = await _htmlCapture.GenerateFromUrl(url);
                await MarkCardPrintedAsync(userId);
                return File(pdf, "application/pdf", $"credencial-institucional-{userId:N}.pdf");
            }
            catch (Exception htmlEx)
            {
                _logger.LogWarning(htmlEx,
                    "[InstitutionalCredential] PDF HTML falló; nativo UserId={UserId}", userId);
                var pdf = await _pdfService.GenerateCardPdfAsync(userId, currentUserId);
                await MarkCardPrintedAsync(userId);
                return File(pdf, "application/pdf", $"credencial-institucional-{userId:N}.pdf");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[InstitutionalCredential] Print error UserId={UserId}", userId);
            return new ContentResult
            {
                Content = "No se pudo generar el PDF: " + ex.Message,
                ContentType = "text/plain; charset=utf-8",
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }

    [HttpPost("api/generate/{userId}")]
    public async Task<IActionResult> GenerateApi(Guid userId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var actorId))
            return Unauthorized("Usuario no autenticado");

        try
        {
            var result = await _service.GenerateAsync(userId, actorId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("api/list-json")]
    public async Task<IActionResult> ListJson(
        string? schoolId = null,
        string? role = null,
        string? search = null,
        string? documentId = null,
        string? credentialStatus = null,
        string? printed = null)
    {
        var query = ApplyInstitutionalCredentialListFilters(
            StaffInstitutionalRoleFilter.WhereIsInstitutionalStaff(_context.Users.AsNoTracking())
                .Where(u => u.SchoolId != null),
            schoolId,
            role,
            search,
            documentId,
            credentialStatus,
            printed);

        var rawRows = await query
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.Name)
            .Select(u => new
            {
                id = u.Id,
                fullName = u.Name + " " + u.LastName,
                documentId = u.DocumentId ?? "",
                email = u.Email ?? "",
                photoUrl = u.PhotoUrl,
                role = u.Role,
                roleDisplay = StaffInstitutionalRoleFilter.FormatRoleDisplay(u.Role),
                jobTitle = _context.Set<StaffInstitutionalProfile>()
                    .Where(p => p.UserId == u.Id)
                    .Select(p => p.JobTitle)
                    .FirstOrDefault() ?? "",
                department = _context.Set<StaffInstitutionalProfile>()
                    .Where(p => p.UserId == u.Id)
                    .Select(p => p.Department)
                    .FirstOrDefault() ?? "",
                employeeCode = _context.Set<StaffInstitutionalProfile>()
                    .Where(p => p.UserId == u.Id)
                    .Select(p => p.EmployeeCode)
                    .FirstOrDefault() ?? "",
                activeCard = _context.Set<InstitutionalCredentialCard>()
                    .Where(c => c.UserId == u.Id && c.Status == "active")
                    .Select(c => new
                    {
                        c.CardNumber,
                        c.IssuedAt,
                        c.ExpiresAt,
                        c.Status,
                        c.IsPrinted,
                        c.PrintedAt
                    })
                    .FirstOrDefault(),
                hasRevokedCard = _context.Set<InstitutionalCredentialCard>()
                    .Any(c => c.UserId == u.Id && c.Status == "revoked")
            })
            .ToListAsync();

        var rows = rawRows.Select(u =>
        {
            var displayStatus = ResolveCredentialDisplayStatus(
                u.activeCard?.Status,
                u.activeCard?.ExpiresAt,
                u.hasRevokedCard);
            return new
            {
                u.id,
                u.fullName,
                u.documentId,
                u.email,
                u.photoUrl,
                u.role,
                u.roleDisplay,
                u.jobTitle,
                u.department,
                employeeCode = u.employeeCode,
                cardNumber = u.activeCard?.CardNumber ?? "",
                issuedAt = u.activeCard?.IssuedAt,
                expiresAt = u.activeCard?.ExpiresAt,
                cardStatus = u.activeCard?.Status ?? "",
                credentialStatus = displayStatus,
                isPrinted = u.activeCard != null && u.activeCard.IsPrinted,
                printedAt = u.activeCard?.PrintedAt,
                hasActiveCard = u.activeCard != null && displayStatus == "active"
            };
        }).ToList();

        return Json(new { data = rows });
    }

    [HttpGet("api/list-filters")]
    public async Task<IActionResult> ListFilters()
    {
        var staffQuery = StaffInstitutionalRoleFilter.WhereIsInstitutionalStaff(_context.Users.AsNoTracking())
            .Where(u => u.SchoolId != null);

        var schools = await _context.Schools.AsNoTracking().IgnoreQueryFilters()
            .OrderBy(s => s.Name)
            .Select(s => new { id = s.Id.ToString(), name = s.Name })
            .ToListAsync();

        var roles = await staffQuery
            .Where(u => u.Role != null && u.Role != "")
            .Select(u => u.Role!)
            .Distinct()
            .OrderBy(r => r)
            .ToListAsync();

        return Json(new
        {
            schools,
            roles = roles.Select(r => new { value = r, label = StaffInstitutionalRoleFilter.FormatRoleDisplay(r) })
        });
    }

    [HttpGet("api/qr-preview/{userId:guid}")]
    public async Task<IActionResult> QrPreview(Guid userId)
    {
        var card = await _service.GetCurrentCardAsync(userId);
        if (card == null)
            return Json(new { success = false, message = "No hay credencial activa con QR vigente." });

        var siteBase = $"{Request.Scheme}://{Request.Host}";
        var publicUrl = StaffMemberPublicLink.BuildPublicUrl(siteBase, card.QrToken, _qrSignatureService);

        return Json(new
        {
            success = true,
            cardNumber = card.CardNumber,
            qrImageDataUrl = card.QrImageDataUrl,
            publicMemberUrl = publicUrl
        });
    }

    private IActionResult InvalidPublicMemberProfile() =>
        View("PublicMemberInvalid", new StaffMemberPublicInvalidVm());

    private async Task<IActionResult> RenderPublicMemberProfileAsync(string rawToken)
    {
        var vm = await _service.ResolvePublicProfileByQrTokenAsync(rawToken);
        if (vm == null)
        {
            return View("PublicMemberInvalid", new StaffMemberPublicInvalidVm
            {
                Title = "Enlace no válido o expirado",
                Message = "La credencial fue revocada, expiró o el código no es válido. Solicite una credencial actualizada en su institución."
            });
        }

        return View("PublicMemberProfile", vm);
    }

    private static string ResolveCredentialDisplayStatus(
        string? activeCardStatus,
        DateTime? expiresAt,
        bool hasRevokedCard)
    {
        if (string.IsNullOrWhiteSpace(activeCardStatus) ||
            !string.Equals(activeCardStatus, "active", StringComparison.OrdinalIgnoreCase))
            return hasRevokedCard ? "revoked" : "none";

        if (expiresAt.HasValue && expiresAt.Value <= DateTime.UtcNow)
            return "expired";

        return "active";
    }

    private IQueryable<User> ApplyInstitutionalCredentialListFilters(
        IQueryable<User> query,
        string? schoolId,
        string? role,
        string? search,
        string? documentId,
        string? credentialStatus,
        string? printed)
    {
        if (Guid.TryParse(schoolId, out var sid))
            query = query.Where(u => u.SchoolId == sid);

        if (!string.IsNullOrWhiteSpace(role))
        {
            var r = role.Trim();
            query = query.Where(u => u.Role == r);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var p = "%" + search.Trim() + "%";
            query = query.Where(u =>
                EF.Functions.ILike(u.Name, p) ||
                EF.Functions.ILike(u.LastName, p) ||
                (u.Email != null && EF.Functions.ILike(u.Email, p)) ||
                (u.DocumentId != null && EF.Functions.ILike(u.DocumentId, p)));
        }

        if (!string.IsNullOrWhiteSpace(documentId))
        {
            var docPattern = "%" + documentId.Trim() + "%";
            query = query.Where(u =>
                u.DocumentId != null && EF.Functions.ILike(u.DocumentId, docPattern));
        }

        var statusFilter = string.IsNullOrWhiteSpace(credentialStatus)
            ? null
            : credentialStatus.Trim().ToLowerInvariant();

        if (statusFilter is "none")
        {
            query = query.Where(u => !_context.Set<InstitutionalCredentialCard>()
                .Any(c => c.UserId == u.Id && c.Status == "active"));
        }
        else if (statusFilter is "active")
        {
            query = query.Where(u => _context.Set<InstitutionalCredentialCard>()
                .Any(c => c.UserId == u.Id && c.Status == "active" &&
                    (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow)));
        }
        else if (statusFilter is "expired")
        {
            query = query.Where(u => _context.Set<InstitutionalCredentialCard>()
                .Any(c => c.UserId == u.Id && c.Status == "active" &&
                    c.ExpiresAt != null && c.ExpiresAt <= DateTime.UtcNow));
        }
        else if (statusFilter is "revoked")
        {
            query = query.Where(u =>
                !_context.Set<InstitutionalCredentialCard>()
                    .Any(c => c.UserId == u.Id && c.Status == "active") &&
                _context.Set<InstitutionalCredentialCard>()
                    .Any(c => c.UserId == u.Id && c.Status == "revoked"));
        }

        var printedFilter = string.IsNullOrWhiteSpace(printed)
            ? null
            : printed.Trim().ToLowerInvariant();

        if (printedFilter is "printed")
        {
            query = query.Where(u => _context.Set<InstitutionalCredentialCard>()
                .Any(c => c.UserId == u.Id && c.Status == "active" && c.IsPrinted));
        }
        else if (printedFilter is "not_printed")
        {
            query = query.Where(u => !_context.Set<InstitutionalCredentialCard>()
                .Any(c => c.UserId == u.Id && c.Status == "active" && c.IsPrinted));
        }

        return query;
    }

    private async Task MarkCardPrintedAsync(Guid userId)
    {
        var cards = await _context.Set<InstitutionalCredentialCard>()
            .Where(c => c.UserId == userId && c.Status == "active")
            .ToListAsync();
        if (cards.Count == 0)
            return;
        var now = DateTime.UtcNow;
        foreach (var c in cards)
        {
            c.IsPrinted = true;
            c.PrintedAt = now;
        }
        await _context.SaveChangesAsync();
    }
}
