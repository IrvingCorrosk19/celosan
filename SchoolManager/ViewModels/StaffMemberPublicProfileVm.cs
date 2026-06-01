namespace SchoolManager.ViewModels;

/// <summary>Vista pública (solo lectura) al escanear el QR de credencial institucional.</summary>
public class StaffMemberPublicProfileVm
{
    public string FullName { get; set; } = "";
    public string? PhotoUrl { get; set; }
    public string RoleDisplay { get; set; } = "";
    public string JobTitle { get; set; } = "—";
    public string Department { get; set; } = "—";
    public string? SchoolName { get; set; }
    public string? EmployeeCode { get; set; }
    public string? Email { get; set; }
    public string CredentialStatusDisplay { get; set; } = "";
    public bool IsAccountActive { get; set; }
}

public class StaffMemberPublicInvalidVm
{
    public string Title { get; set; } = "No se pudo mostrar el perfil";
    public string Message { get; set; } = "El código QR está dañado, expiró o no es válido. Solicite una credencial actualizada en su institución.";
}
