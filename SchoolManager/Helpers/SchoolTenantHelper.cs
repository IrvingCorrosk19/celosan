using Microsoft.EntityFrameworkCore;
using SchoolManager.Models;
using SchoolManager.Services.Interfaces;

namespace SchoolManager.Helpers;

public static class SchoolTenantHelper
{
    public static bool IsSuperAdminRole(string? role) =>
        string.Equals(role, "superadmin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

    public static bool CanAccessSchool(Guid? resourceSchoolId, Guid? callerSchoolId, bool bypassTenant)
    {
        if (bypassTenant)
            return true;
        if (!callerSchoolId.HasValue || callerSchoolId.Value == Guid.Empty)
            return false;
        if (!resourceSchoolId.HasValue)
            return false;
        return resourceSchoolId.Value == callerSchoolId.Value;
    }

    public static bool CanAccessSchool(Guid? resourceSchoolId, ITenantContext tenant) =>
        CanAccessSchool(resourceSchoolId, tenant.SchoolId, tenant.BypassTenantFilter);

    public static async Task<User?> GetUserInTenantAsync(
        SchoolDbContext context,
        ITenantContext tenant,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
            return null;
        return CanAccessSchool(user.SchoolId, tenant) ? user : null;
    }

    public static async Task<bool> StudentBelongsToTenantAsync(
        SchoolDbContext context,
        ITenantContext tenant,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        if (tenant.BypassTenantFilter)
            return true;

        if (!tenant.SchoolId.HasValue)
            return false;

        return await context.Users.AsNoTracking()
            .AnyAsync(u =>
                u.Id == studentId &&
                u.SchoolId == tenant.SchoolId &&
                (u.Role.ToLower() == "student" || u.Role.ToLower() == "estudiante"),
                cancellationToken);
    }
}
