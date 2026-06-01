namespace SchoolManager.Dtos;

public class StaffCardRenderDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = "";
    public string? DocumentId { get; set; }
    public string RoleDisplay { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string Department { get; set; } = "";
    public string? EmployeeCode { get; set; }
    public string CardNumber { get; set; } = "";
    public string QrToken { get; set; } = "";

    public string SchoolName { get; set; } = "";
    public string? SchoolPhone { get; set; }
    public string? IdCardPolicy { get; set; }

    public byte[]? LogoBytes { get; set; }
    public byte[]? SecondaryLogoBytes { get; set; }
    public byte[]? PhotoBytes { get; set; }
    public byte[]? WatermarkBytes { get; set; }
    public string? PhotoUrl { get; set; }
}
