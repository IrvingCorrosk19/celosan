namespace SchoolManager.Dtos;

/// <summary>
/// Identidad institucional para el PDF del boletín. Solo lo necesario para cabecera.
/// </summary>
public sealed class BulletinPdfIdentity
{
    public string SchoolName { get; set; } = "Institución educativa";
    public byte[]? LogoBytes { get; set; }
}
