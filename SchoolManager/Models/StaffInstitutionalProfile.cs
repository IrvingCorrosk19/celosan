namespace SchoolManager.Models;

/// <summary>Datos laborales opcionales para credencial y directorio de personal.</summary>
public class StaffInstitutionalProfile
{
    public Guid UserId { get; set; }

    public string? JobTitle { get; set; }

    public string? Department { get; set; }

    public string? EmployeeCode { get; set; }

    public virtual User User { get; set; } = null!;
}
