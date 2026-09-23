using System.Globalization;

namespace SchoolManager.Helpers;

public static class OfficialGradeMark
{
    public const string NoAsistio = "N/A";
    public const string SinNota = "SN";
    public const string Empty = "—";

    public static string Display(decimal? score, string? status = null, bool isImported = false)
    {
        var normalized = ImportedTrimesterGradeStatus.Normalize(status);
        if (isImported || ImportedTrimesterGradeStatus.IsSpecial(normalized))
        {
            if (normalized == ImportedTrimesterGradeStatus.NoAsistio)
                return NoAsistio;
            if (normalized == ImportedTrimesterGradeStatus.SinNota)
                return SinNota;
        }

        return score.HasValue
            ? score.Value.ToString("0.0", CultureInfo.InvariantCulture)
            : Empty;
    }

    public static bool TryParseToken(string? raw, out string? status, out decimal? score, out string? error)
    {
        status = null;
        score = null;
        error = null;
        var text = (raw ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return true;

        var compact = text.Replace(" ", string.Empty).ToUpperInvariant();
        if (compact is "N/A" or "NA" or "N\\A")
        {
            status = ImportedTrimesterGradeStatus.NoAsistio;
            return true;
        }

        if (compact == "SN")
        {
            status = ImportedTrimesterGradeStatus.SinNota;
            return true;
        }

        error = "El valor no es un token especial.";
        return false;
    }

    public static bool IsApproved(decimal? finalAverage) =>
        finalAverage.HasValue && finalAverage.Value >= 3.0m;
}
