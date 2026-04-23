namespace SchoolManager.Helpers;

/// <summary>
/// Cadenas de transformación Cloudinary solo en URL de entrega (on-the-fly). No crean ni borran activos.
/// </summary>
public static class UserPhotoDeliveryTransforms
{
    /// <summary>Miniatura cuadrada para listados (~44–52px en pantalla); reduce ancho de banda frente a la original.</summary>
    public const string ListThumbnail = "w_128,h_128,c_fill,g_auto,q_auto:eco,f_auto";
}
