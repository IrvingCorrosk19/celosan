namespace SchoolManager.Helpers;

public static class ImportedTrimesterGradeStatus
{
    public const string Graded = "Graded";
    public const string NoAsistio = "NoAsistio";
    public const string SinNota = "SinNota";

    public static bool IsSpecial(string? status) =>
        Normalize(status) is NoAsistio or SinNota;

    public static bool IsGraded(string? status) =>
        Normalize(status) == Graded;

    public static string Normalize(string? status)
    {
        var value = (status ?? string.Empty).Trim();
        if (value.Equals(NoAsistio, StringComparison.OrdinalIgnoreCase))
            return NoAsistio;
        if (value.Equals(SinNota, StringComparison.OrdinalIgnoreCase))
            return SinNota;
        return Graded;
    }
}
