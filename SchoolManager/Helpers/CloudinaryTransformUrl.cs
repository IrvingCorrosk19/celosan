using System.Text.RegularExpressions;

namespace SchoolManager.Helpers;

/// <summary>
/// Inserta o reemplaza la cadena de transformación inmediatamente después de <c>/image/upload/</c> en URLs de entrega Cloudinary.
/// No modifica el activo almacenado; solo altera la URL de entrega (on-the-fly). Compatible con cualquier foto ya guardada.
/// </summary>
public static partial class CloudinaryTransformUrl
{
    private const string UploadMarker = "/image/upload/";

    /// <summary>
    /// Aplica <paramref name="transformSegment"/> (ej. <c>w_128,h_128,c_fill,q_auto:eco,f_auto</c>) delante del public_id/version.
    /// Si no es Cloudinary o no hay marcador <c>/image/upload/</c>, devuelve <paramref name="originalUrl"/> sin cambios.
    /// </summary>
    public static string InsertAfterUpload(string? originalUrl, string transformSegment)
    {
        if (string.IsNullOrWhiteSpace(originalUrl) || string.IsNullOrWhiteSpace(transformSegment))
            return originalUrl?.Trim() ?? string.Empty;

        var url = originalUrl.Trim();
        if (!url.Contains("res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
            return url;

        var markerIdx = url.IndexOf(UploadMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIdx < 0)
            return url;

        var prefix = url[..(markerIdx + UploadMarker.Length)];
        var tail = url[(markerIdx + UploadMarker.Length)..];

        var qIdx = tail.IndexOf('?', StringComparison.Ordinal);
        string query = "";
        if (qIdx >= 0)
        {
            query = tail[qIdx..];
            tail = tail[..qIdx];
        }

        var segments = tail.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        var writeIdx = 0;
        for (var i = 0; i < segments.Count; i++)
        {
            if (LooksLikeCloudinaryTransformation(segments[i]))
                continue;
            writeIdx = i;
            break;
        }

        var retained = string.Join("/", segments.Skip(writeIdx));
        if (string.IsNullOrEmpty(retained))
            return url;

        var chain = transformSegment.Trim().Trim('/');
        if (string.IsNullOrEmpty(chain))
            return url;

        return prefix + chain + "/" + retained + query;
    }

    private static bool LooksLikeCloudinaryTransformation(string segment)
    {
        if (string.IsNullOrEmpty(segment))
            return false;
        if (VersionSegment().IsMatch(segment))
            return false;
        if (segment.Contains(',', StringComparison.Ordinal))
            return true;

        return segment.StartsWith("w_", StringComparison.OrdinalIgnoreCase)
               || segment.StartsWith("h_", StringComparison.OrdinalIgnoreCase)
               || segment.StartsWith("c_", StringComparison.OrdinalIgnoreCase)
               || segment.StartsWith("q_", StringComparison.OrdinalIgnoreCase)
               || segment.StartsWith("f_", StringComparison.OrdinalIgnoreCase)
               || segment.StartsWith("g_", StringComparison.OrdinalIgnoreCase)
               || segment.StartsWith("e_", StringComparison.OrdinalIgnoreCase)
               || segment.StartsWith("b_", StringComparison.OrdinalIgnoreCase)
               || segment.StartsWith("a_", StringComparison.OrdinalIgnoreCase)
               || segment.StartsWith("fl_", StringComparison.OrdinalIgnoreCase)
               || segment.StartsWith("dpr_", StringComparison.OrdinalIgnoreCase)
               || segment.StartsWith("t_", StringComparison.OrdinalIgnoreCase)
               || segment.StartsWith("x_", StringComparison.OrdinalIgnoreCase)
               || segment.StartsWith("y_", StringComparison.OrdinalIgnoreCase)
               || segment.StartsWith("r_", StringComparison.OrdinalIgnoreCase)
               || segment.StartsWith("o_", StringComparison.OrdinalIgnoreCase)
               || segment.StartsWith("l_", StringComparison.OrdinalIgnoreCase)
               || segment.StartsWith("ar_", StringComparison.OrdinalIgnoreCase)
               || segment.StartsWith("so_", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^v\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionSegment();
}
