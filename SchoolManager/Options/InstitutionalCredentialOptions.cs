namespace SchoolManager.Options;

/// <summary>
/// URL pública del sitio para QR de perfil institucional.
/// En Render: InstitutionalCredential__PublicBaseUrl=https://tu-app.onrender.com
/// </summary>
public class InstitutionalCredentialOptions
{
    public const string SectionName = "InstitutionalCredential";

    /// <summary>Ej. https://tu-app.onrender.com (sin barra final).</summary>
    public string? PublicBaseUrl { get; set; }
}
