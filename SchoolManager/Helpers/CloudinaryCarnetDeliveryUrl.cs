namespace SchoolManager.Helpers;

/// <summary>
/// URL de entrega Cloudinary optimizada para foto de carnet (alta densidad, recorte centrado).
/// </summary>
public static class CloudinaryCarnetDeliveryUrl
{
    /// <summary>Inserta transformación cuadrada de alta calidad tras <c>/image/upload/</c>.</summary>
    public static string WithCarnetFaceCrop(string? originalUrl, int edgePx)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
            return originalUrl ?? string.Empty;

        var url = originalUrl.Trim();
        if (!url.Contains("res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
            return url;

        edgePx = Math.Clamp(edgePx, 120, 800);
        var chain = $"w_{edgePx},h_{edgePx},c_fill,g_auto,q_auto:good,f_auto";
        return CloudinaryTransformUrl.InsertAfterUpload(url, chain);
    }
}
