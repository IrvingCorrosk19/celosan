using SchoolManager.Models;

namespace SchoolManager.Helpers;

public static class CurriculumLoadAccess
{
    public static bool CanView(User? user)
    {
        var role = NormalizeRole(user?.Role);
        return role is "teacher" or "admin" or "superadmin" or "secretaria";
    }

    public static bool CanEdit(User? user)
    {
        var role = NormalizeRole(user?.Role);
        if (role is "admin" or "superadmin")
            return true;
        if (role == "secretaria" && user?.CanEditCurriculumLoad == true)
            return true;
        return false;
    }

    private static string NormalizeRole(string? role) =>
        string.IsNullOrWhiteSpace(role) ? string.Empty : role.Trim().ToLowerInvariant();
}
