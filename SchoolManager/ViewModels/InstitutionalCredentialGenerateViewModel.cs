using SchoolManager.Dtos;
using SchoolManager.Models;

namespace SchoolManager.ViewModels;

public class InstitutionalCredentialGenerateViewModel
{
    public Guid UserId { get; set; }

    public bool UserNotFound { get; set; }

    public bool NotEligible { get; set; }

    public string? NotEligibleReason { get; set; }

    public InstitutionalCredentialCardDto? Card { get; set; }

    public bool HasActiveCard => Card != null;

    public string SchoolName { get; set; } = "";

    public string? SchoolLogoUrl { get; set; }

    public string? SchoolPhone { get; set; }

    public string? IdCardPolicy { get; set; }

    public string Orientation { get; set; } = "Vertical";

    public bool ShowQr { get; set; } = true;

    public bool ShowPhoto { get; set; } = true;

    public bool ShowSchoolPhone { get; set; } = true;

    public bool ShowWatermark { get; set; } = true;

    public bool ShowDocumentId { get; set; }

    public string PrimaryColor { get; set; } = "#0D6EFD";

    public string BackgroundColor { get; set; } = "#FFFFFF";

    public string TextColor { get; set; } = "#111111";

    public string? DocumentId { get; set; }

    public static InstitutionalCredentialGenerateViewModel ForUser(
        Guid userId,
        School? school,
        SchoolIdCardSetting? settings)
    {
        var s = settings;
        return new InstitutionalCredentialGenerateViewModel
        {
            UserId = userId,
            SchoolName = school?.Name ?? "Sin escuela asignada",
            SchoolLogoUrl = school?.LogoUrl,
            SchoolPhone = school != null && !string.IsNullOrWhiteSpace(school.Phone) ? school.Phone.Trim() : null,
            IdCardPolicy = school?.IdCardPolicy,
            Orientation = s?.Orientation ?? "Vertical",
            ShowQr = s?.ShowQr ?? true,
            ShowPhoto = s?.ShowPhoto ?? true,
            ShowSchoolPhone = s?.ShowSchoolPhone ?? true,
            ShowWatermark = s?.ShowWatermark ?? true,
            ShowDocumentId = s?.ShowDocumentId ?? false,
            PrimaryColor = string.IsNullOrWhiteSpace(s?.PrimaryColor) ? "#0D6EFD" : s!.PrimaryColor,
            BackgroundColor = string.IsNullOrWhiteSpace(s?.BackgroundColor) ? "#FFFFFF" : s!.BackgroundColor,
            TextColor = string.IsNullOrWhiteSpace(s?.TextColor) ? "#111111" : s!.TextColor
        };
    }
}
