using Microsoft.EntityFrameworkCore;
using SchoolManager.Helpers;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;
using SchoolManager.ViewModels;
using StaffProfileEntity = SchoolManager.Models.StaffInstitutionalProfile;

namespace SchoolManager.Services.Implementations;

public class StaffInstitutionalProfileService : IStaffInstitutionalProfileService
{
    private readonly SchoolDbContext _context;
    private readonly ILogger<StaffInstitutionalProfileService> _logger;

    public StaffInstitutionalProfileService(
        SchoolDbContext context,
        ILogger<StaffInstitutionalProfileService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<StaffInstitutionalProfileViewModel?> GetProfileAsync(Guid userId)
    {
        var user = await StaffInstitutionalRoleFilter.WhereIsInstitutionalStaff(_context.Users.AsNoTracking())
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.LastName,
                u.Email,
                u.DocumentId,
                u.DateOfBirth,
                u.CellphonePrimary,
                u.CellphoneSecondary,
                u.Role,
                u.SchoolId,
                u.PhotoUrl,
                u.Status
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            _logger.LogWarning("[StaffProfile] Usuario no encontrado o no es personal: {UserId}", userId);
            return null;
        }

        if (!StaffInstitutionalProfileAccess.IsAllowedRole(user.Role))
        {
            _logger.LogWarning("[StaffProfile] Rol no permitido para perfil: {Role} UserId={UserId}", user.Role, userId);
            return null;
        }

        await EnsureStaffProfileRowAsync(userId);

        var staffRow = await _context.StaffInstitutionalProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId);

        string? schoolName = null;
        if (user.SchoolId.HasValue)
        {
            schoolName = await _context.Schools.AsNoTracking()
                .Where(s => s.Id == user.SchoolId.Value)
                .Select(s => s.Name)
                .FirstOrDefaultAsync();
        }

        return new StaffInstitutionalProfileViewModel
        {
            Id = user.Id,
            Name = user.Name,
            LastName = user.LastName,
            Email = user.Email,
            DocumentId = user.DocumentId,
            DateOfBirth = user.DateOfBirth,
            CellphonePrimary = user.CellphonePrimary,
            CellphoneSecondary = user.CellphoneSecondary,
            JobTitle = staffRow?.JobTitle,
            Department = staffRow?.Department,
            EmployeeCode = staffRow?.EmployeeCode,
            SchoolName = schoolName,
            RoleDisplay = StaffInstitutionalRoleFilter.FormatRoleDisplay(user.Role),
            Status = FormatStatusDisplay(user.Status),
            PhotoUrl = user.PhotoUrl,
            HasSchoolAssigned = user.SchoolId.HasValue,
            CanOpenInstitutionalCredentialUi = StaffInstitutionalProfileAccess.CanOpenInstitutionalCredentialUi(user.Role)
        };
    }

    public async Task<bool> UpdateProfileAsync(StaffInstitutionalProfileViewModel model, Guid actorId)
    {
        if (actorId != model.Id)
        {
            _logger.LogWarning("[StaffProfile] Intento de actualizar otro usuario Actor={Actor} Target={Target}", actorId, model.Id);
            return false;
        }

        var user = await StaffInstitutionalRoleFilter.WhereIsInstitutionalStaff(_context.Users)
            .FirstOrDefaultAsync(u => u.Id == model.Id);

        if (user == null || !StaffInstitutionalProfileAccess.IsAllowedRole(user.Role))
            return false;

        if (user.Email != model.Email)
        {
            var emailInUse = await _context.Users
                .AnyAsync(u => u.Email == model.Email && u.Id != model.Id);
            if (emailInUse)
                return false;
        }

        if (!string.IsNullOrEmpty(model.DocumentId) && user.DocumentId != model.DocumentId)
        {
            var documentInUse = await _context.Users
                .AnyAsync(u => u.DocumentId == model.DocumentId && u.Id != model.Id);
            if (documentInUse)
                return false;
        }

        user.Name = model.Name.Trim();
        user.LastName = model.LastName.Trim();
        user.Email = model.Email.Trim();
        user.DocumentId = string.IsNullOrWhiteSpace(model.DocumentId) ? null : model.DocumentId.Trim();
        user.DateOfBirth = model.DateOfBirth;
        user.CellphonePrimary = string.IsNullOrWhiteSpace(model.CellphonePrimary) ? null : model.CellphonePrimary.Trim();
        user.CellphoneSecondary = string.IsNullOrWhiteSpace(model.CellphoneSecondary) ? null : model.CellphoneSecondary.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = actorId;

        var profile = await _context.StaffInstitutionalProfiles.FirstOrDefaultAsync(p => p.UserId == model.Id);
        if (profile == null)
        {
            profile = new StaffProfileEntity { UserId = model.Id };
            _context.StaffInstitutionalProfiles.Add(profile);
        }

        profile.JobTitle = string.IsNullOrWhiteSpace(model.JobTitle) ? null : model.JobTitle.Trim();
        profile.Department = string.IsNullOrWhiteSpace(model.Department) ? null : model.Department.Trim();
        profile.EmployeeCode = string.IsNullOrWhiteSpace(model.EmployeeCode) ? null : model.EmployeeCode.Trim();

        await _context.SaveChangesAsync();
        _logger.LogInformation("[StaffProfile] Perfil actualizado UserId={UserId}", model.Id);
        return true;
    }

    public async Task<bool> IsEmailAvailableAsync(string email, Guid currentUserId)
    {
        try
        {
            var exists = await _context.Users.AnyAsync(u => u.Email == email && u.Id != currentUserId);
            return !exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StaffProfile] Error validando email");
            return false;
        }
    }

    public async Task<bool> IsDocumentIdAvailableAsync(string? documentId, Guid currentUserId)
    {
        if (string.IsNullOrEmpty(documentId))
            return true;

        try
        {
            var exists = await _context.Users.AnyAsync(u => u.DocumentId == documentId && u.Id != currentUserId);
            return !exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StaffProfile] Error validando documento");
            return false;
        }
    }

    public async Task EnsureStaffProfileRowAsync(Guid userId)
    {
        var exists = await _context.StaffInstitutionalProfiles.AnyAsync(p => p.UserId == userId);
        if (exists)
            return;

        _context.StaffInstitutionalProfiles.Add(new StaffProfileEntity { UserId = userId });
        await _context.SaveChangesAsync();
        _logger.LogInformation("[StaffProfile] Fila staff_institutional_profiles creada UserId={UserId}", userId);
    }

    private static string FormatStatusDisplay(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return "—";
        return status.Equals("active", StringComparison.OrdinalIgnoreCase) ? "Activo" : status;
    }
}
