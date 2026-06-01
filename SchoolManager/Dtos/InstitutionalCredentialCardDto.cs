namespace SchoolManager.Dtos;

public class InstitutionalCredentialCardDto
{
    public Guid UserId { get; set; }
    public string CardNumber { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string RoleDisplay { get; set; } = null!;
    public string JobTitle { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string QrToken { get; set; } = null!;
    public string? QrImageDataUrl { get; set; }
    public string? PhotoUrl { get; set; }
}
