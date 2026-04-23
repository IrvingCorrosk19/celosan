namespace SchoolManager.Helpers;

/// <summary>
/// URL del endpoint que sirve la foto de usuario (Cloudinary, disco o imagen por defecto como el logo de escuela).
/// </summary>
public static class UserPhotoLinks
{
    public static string Href(string? photoUrlStored) =>
        "/File/GetUserPhoto?photoUrl=" + Uri.EscapeDataString(photoUrlStored ?? string.Empty);

    /// <summary>
    /// Foto para vista previa del carnet (HTML/Puppeteer): query <c>carnetEdge</c> para variante Cloudinary de mayor borde, mismo marco CSS.
    /// </summary>
    public static string HrefForCarnetPreview(string? photoUrlStored, int edgePx = 360) =>
        "/File/GetUserPhoto?photoUrl=" + Uri.EscapeDataString(photoUrlStored ?? string.Empty)
        + "&carnetEdge=" + edgePx.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Miniatura listados: <c>variant=thumb</c> → redirección Cloudinary liviana.</summary>
    public static string HrefListThumbnail(string? photoUrlStored) =>
        "/File/GetUserPhoto?photoUrl=" + Uri.EscapeDataString(photoUrlStored ?? string.Empty) + "&variant=thumb";
}
