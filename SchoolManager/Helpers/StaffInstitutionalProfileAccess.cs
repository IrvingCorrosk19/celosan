namespace SchoolManager.Helpers;

/// <summary>
/// Roles autorizados para el módulo de autogestión de perfil institucional (carnet de personal).
/// </summary>
public static class StaffInstitutionalProfileAccess
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "superadmin",
        "admin",
        "director",
        "teacher",
        "docente",
        "secretaria",
        "inspector",
        "contable",
        "contabilidad"
    };

    public static bool IsAllowedRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;
        if (StaffInstitutionalRoleFilter.IsStudentRole(role))
            return false;
        var r = role.Trim();
        if (r.Equals("parent", StringComparison.OrdinalIgnoreCase) ||
            r.Equals("acudiente", StringComparison.OrdinalIgnoreCase) ||
            r.Equals("clubparentsadmin", StringComparison.OrdinalIgnoreCase))
            return false;
        return AllowedRoles.Contains(r);
    }

    /// <summary>La UI de emisión de credencial sigue restringida a superadmin.</summary>
    public static bool CanOpenInstitutionalCredentialUi(string? role) =>
        string.Equals(role?.Trim(), "superadmin", StringComparison.OrdinalIgnoreCase);

    public const string AuthorizeRoles =
        "superadmin,admin,director,teacher,docente,secretaria,inspector,contable,contabilidad";

    /// <summary>Roles listados en SuperAdmin/StaffDirectory (excluye estudiantes, padres, club de padres).</summary>
    public static readonly string[] StaffDirectoryAllowlist =
    {
        "superadmin",
        "admin",
        "director",
        "teacher",
        "secretaria",
        "inspector",
        "contable",
        "contabilidad"
    };

    public static bool IsStaffDirectoryEligibleRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;
        return StaffDirectoryAllowlist.Contains(role.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
