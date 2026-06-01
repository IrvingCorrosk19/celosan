using SchoolManager.Models;

namespace SchoolManager.Helpers;

/// <summary>Usuarios no estudiantes con credencial institucional (docentes, dirección, etc.).</summary>
public static class StaffInstitutionalRoleFilter
{
    private static readonly HashSet<string> StudentRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "student", "Student", "estudiante", "Estudiante", "alumno", "Alumno"
    };

    public static bool IsStudentRole(string? role) =>
        role != null && StudentRoles.Contains(role.Trim());

    /// <summary>True si el rol puede gestionarse en directorio / credencial institucional (no es alumno).</summary>
    public static bool IsInstitutionalStaffRole(string? role) =>
        !string.IsNullOrWhiteSpace(role) && !IsStudentRole(role);

    public static IQueryable<User> WhereIsInstitutionalStaff(IQueryable<User> query) =>
        query.Where(u => u.Role != null &&
            u.Role != "student" && u.Role != "Student" &&
            u.Role != "estudiante" && u.Role != "Estudiante" &&
            u.Role != "alumno" && u.Role != "Alumno");

    public static string FormatRoleDisplay(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return "—";
        return role.Trim().ToLowerInvariant() switch
        {
            "admin" or "administrator" => "Administrador",
            "director" => "Director",
            "teacher" => "Docente",
            "coordinator" => "Coordinador",
            "secretary" => "Secretaría",
            "counselor" => "Consejería",
            "security" => "Seguridad",
            "staff" => "Personal",
            "superadmin" => "Super administrador",
            _ => role.Trim()
        };
    }
}
