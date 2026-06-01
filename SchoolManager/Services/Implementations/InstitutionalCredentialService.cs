using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Options;
using SchoolManager.Services.Interfaces;
using SchoolManager.Services.Security;
using SchoolManager.ViewModels;

namespace SchoolManager.Services.Implementations;

public class InstitutionalCredentialService : IInstitutionalCredentialService
{
    public const int QrTokenValidityMonths = 6;

    private readonly SchoolDbContext _context;
    private readonly ILogger<InstitutionalCredentialService> _logger;
    private readonly IQrSignatureService _qrSignatureService;
    private readonly IOptions<InstitutionalCredentialOptions> _credentialOptions;

    public InstitutionalCredentialService(
        SchoolDbContext context,
        ILogger<InstitutionalCredentialService> logger,
        IQrSignatureService qrSignatureService,
        IOptions<InstitutionalCredentialOptions> credentialOptions)
    {
        _context = context;
        _logger = logger;
        _qrSignatureService = qrSignatureService;
        _credentialOptions = credentialOptions;
    }

    private string? ResolveSiteBaseUrl(string? siteBaseUrlOverride)
    {
        if (!string.IsNullOrWhiteSpace(siteBaseUrlOverride))
            return siteBaseUrlOverride.TrimEnd('/');
        var o = _credentialOptions.Value.PublicBaseUrl;
        return string.IsNullOrWhiteSpace(o) ? null : o.TrimEnd('/');
    }

    private static string? BuildQrImageDataUrl(string? qrEncodeContent, IQrSignatureService qrSignatureService)
    {
        if (string.IsNullOrWhiteSpace(qrEncodeContent))
            return null;

        var usePlain = qrEncodeContent.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || qrEncodeContent.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        var pngBytes = QrHelper.GenerateQrPng(qrEncodeContent, usePlain ? null : qrSignatureService);
        return "data:image/png;base64," + Convert.ToBase64String(pngBytes);
    }

    private string? ResolveQrEncodeContent(string rawStaffToken, string? siteBaseUrl)
    {
        var publicUrl = StaffMemberPublicLink.BuildPublicUrl(siteBaseUrl, rawStaffToken, _qrSignatureService);
        return publicUrl ?? rawStaffToken;
    }

    public async Task<InstitutionalCredentialCardDto?> GetCurrentCardAsync(Guid userId, string? siteBaseUrl = null)
    {
        var row = await StaffInstitutionalRoleFilter.WhereIsInstitutionalStaff(_context.Users.AsNoTracking())
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Name,
                u.LastName,
                u.PhotoUrl,
                u.Role,
                u.SchoolId,
                JobTitle = _context.Set<StaffInstitutionalProfile>()
                    .Where(p => p.UserId == u.Id)
                    .Select(p => p.JobTitle)
                    .FirstOrDefault(),
                Department = _context.Set<StaffInstitutionalProfile>()
                    .Where(p => p.UserId == u.Id)
                    .Select(p => p.Department)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();

        if (row == null || !row.SchoolId.HasValue)
            return null;

        var card = await _context.Set<InstitutionalCredentialCard>()
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.Status == "active")
            .Select(c => new { c.CardNumber })
            .FirstOrDefaultAsync();

        if (card == null)
            return null;

        var token = await _context.Set<StaffQrToken>()
            .AsNoTracking()
            .Where(t => t.UserId == userId && !t.IsRevoked &&
                (t.ExpiresAt == null || t.ExpiresAt > DateTime.UtcNow))
            .Select(t => new { t.Token })
            .FirstOrDefaultAsync();

        if (token == null)
            return null;

        var baseUrl = ResolveSiteBaseUrl(siteBaseUrl);
        var qrContent = ResolveQrEncodeContent(token.Token, baseUrl);
        var qrImageDataUrl = BuildQrImageDataUrl(qrContent, _qrSignatureService);

        return new InstitutionalCredentialCardDto
        {
            UserId = userId,
            CardNumber = card.CardNumber,
            FullName = $"{row.Name} {row.LastName}",
            RoleDisplay = StaffInstitutionalRoleFilter.FormatRoleDisplay(row.Role),
            JobTitle = string.IsNullOrWhiteSpace(row.JobTitle) ? "—" : row.JobTitle!,
            Department = string.IsNullOrWhiteSpace(row.Department) ? "—" : row.Department!,
            QrToken = token.Token,
            QrImageDataUrl = qrImageDataUrl,
            PhotoUrl = row.PhotoUrl
        };
    }

    public async Task<InstitutionalCredentialCardDto> GenerateAsync(Guid userId, Guid createdBy)
    {
        _logger.LogInformation(
            "[InstitutionalCredential] GenerateAsync UserId={UserId} CreatedBy={CreatedBy}",
            userId, createdBy);

        using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var user = await StaffInstitutionalRoleFilter.WhereIsInstitutionalStaff(_context.Users)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                _logger.LogWarning("[InstitutionalCredential] Usuario no elegible UserId={UserId}", userId);
                throw new InvalidOperationException("Usuario no encontrado o no es personal institucional.");
            }

            if (!user.SchoolId.HasValue)
            {
                _logger.LogWarning("[InstitutionalCredential] Sin escuela UserId={UserId}", userId);
                throw new InvalidOperationException("El usuario debe tener una escuela asignada.");
            }

            var profile = await _context.Set<StaffInstitutionalProfile>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            var existingCards = await _context.Set<InstitutionalCredentialCard>()
                .Where(c => c.UserId == userId && c.Status == "active")
                .ToListAsync();
            foreach (var ec in existingCards)
                ec.Status = "revoked";

            var existingTokens = await _context.Set<StaffQrToken>()
                .Where(t => t.UserId == userId && !t.IsRevoked)
                .ToListAsync();
            foreach (var et in existingTokens)
                et.IsRevoked = true;

            var cardNumber = InstitutionalCardNumberHelper.Generate(userId);
            var card = new InstitutionalCredentialCard
            {
                UserId = userId,
                CardNumber = cardNumber,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                Status = "active"
            };

            var newToken = new StaffQrToken
            {
                UserId = userId,
                Token = Guid.NewGuid().ToString("N"),
                ExpiresAt = DateTime.UtcNow.AddMonths(QrTokenValidityMonths),
                IsRevoked = false
            };

            _context.Set<InstitutionalCredentialCard>().Add(card);
            _context.Set<StaffQrToken>().Add(newToken);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var baseUrl = ResolveSiteBaseUrl(null);
            var qrContent = ResolveQrEncodeContent(newToken.Token, baseUrl);
            var qrImageDataUrl = BuildQrImageDataUrl(qrContent, _qrSignatureService);

            return new InstitutionalCredentialCardDto
            {
                UserId = userId,
                CardNumber = cardNumber,
                FullName = $"{user.Name} {user.LastName}",
                RoleDisplay = StaffInstitutionalRoleFilter.FormatRoleDisplay(user.Role),
                JobTitle = string.IsNullOrWhiteSpace(profile?.JobTitle) ? "—" : profile!.JobTitle!,
                Department = string.IsNullOrWhiteSpace(profile?.Department) ? "—" : profile!.Department!,
                QrToken = newToken.Token,
                QrImageDataUrl = qrImageDataUrl,
                PhotoUrl = user.PhotoUrl
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<StaffMemberPublicProfileVm?> ResolvePublicProfileByQrTokenAsync(string rawQrToken)
    {
        if (string.IsNullOrWhiteSpace(rawQrToken))
            return null;

        var tokenRow = await _context.Set<StaffQrToken>()
            .AsNoTracking()
            .Where(t => t.Token == rawQrToken.Trim())
            .Select(t => new { t.UserId, t.IsRevoked, t.ExpiresAt })
            .FirstOrDefaultAsync();

        if (tokenRow == null)
            return null;

        if (tokenRow.IsRevoked)
            return null;

        if (tokenRow.ExpiresAt.HasValue && tokenRow.ExpiresAt.Value <= DateTime.UtcNow)
            return null;

        var user = await StaffInstitutionalRoleFilter.WhereIsInstitutionalStaff(_context.Users.AsNoTracking())
            .Where(u => u.Id == tokenRow.UserId)
            .Select(u => new
            {
                u.Name,
                u.LastName,
                u.PhotoUrl,
                u.Role,
                u.Email,
                u.SchoolId,
                u.Status
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return null;

        string? schoolName = null;
        if (user.SchoolId.HasValue)
        {
            schoolName = await _context.Schools.AsNoTracking().IgnoreQueryFilters()
                .Where(s => s.Id == user.SchoolId.Value)
                .Select(s => s.Name)
                .FirstOrDefaultAsync();
        }

        var profile = await _context.Set<StaffInstitutionalProfile>()
            .AsNoTracking()
            .Where(p => p.UserId == tokenRow.UserId)
            .Select(p => new { p.JobTitle, p.Department, p.EmployeeCode })
            .FirstOrDefaultAsync();

        var activeCard = await _context.Set<InstitutionalCredentialCard>()
            .AsNoTracking()
            .Where(c => c.UserId == tokenRow.UserId && c.Status == "active")
            .Select(c => new { c.ExpiresAt })
            .FirstOrDefaultAsync();

        var credentialStatus = "No generada";
        if (activeCard != null)
        {
            if (activeCard.ExpiresAt.HasValue && activeCard.ExpiresAt.Value <= DateTime.UtcNow)
                credentialStatus = "Expirada";
            else
                credentialStatus = "Activa";
        }

        var statusRaw = user.Status?.Trim();
        var isActive = string.Equals(statusRaw, "active", StringComparison.OrdinalIgnoreCase);

        return new StaffMemberPublicProfileVm
        {
            FullName = $"{user.Name} {user.LastName}".Trim(),
            PhotoUrl = user.PhotoUrl,
            RoleDisplay = StaffInstitutionalRoleFilter.FormatRoleDisplay(user.Role),
            JobTitle = string.IsNullOrWhiteSpace(profile?.JobTitle) ? "—" : profile!.JobTitle!,
            Department = string.IsNullOrWhiteSpace(profile?.Department) ? "—" : profile!.Department!,
            SchoolName = schoolName,
            EmployeeCode = string.IsNullOrWhiteSpace(profile?.EmployeeCode) ? null : profile.EmployeeCode,
            Email = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email,
            CredentialStatusDisplay = credentialStatus,
            IsAccountActive = isActive
        };
    }
}
