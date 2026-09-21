namespace SchoolManager.Helpers;

/// <summary>
/// Calculador de solo lectura. Replica la fórmula de
/// <c>StudentActivityScoreService.GetPromediosFinalesAsync</c>:
/// media de (Notas de apreciación, Ejercicios diarios, Examen final) que existan,
/// y media anual de T1/T2/T3 con valor. No incluye recuperación.
/// </summary>
public static class OfficialTrimesterAverageCalculator
{
    public const string TypeApreciacion = "notas de apreciación";
    public const string TypeEjercicios = "ejercicios diarios";
    public const string TypeExamen = "examen final";

    public static readonly string[] OfficialTypes = { TypeApreciacion, TypeEjercicios, TypeExamen };

    public static string NormalizeType(string? type) =>
        (type ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsOfficialType(string? type)
    {
        var n = NormalizeType(type);
        return n == TypeApreciacion || n == TypeEjercicios || n == TypeExamen;
    }

    /// <summary>Promedio de un tipo. Null si no hay notas con valor en esa categoría.</summary>
    public static decimal? AverageByType(IEnumerable<(string? Type, decimal? Score)> scores, string officialType)
    {
        var values = scores
            .Where(s => s.Score.HasValue && NormalizeType(s.Type) == officialType)
            .Select(s => s.Score!.Value)
            .ToList();
        return values.Count == 0 ? null : values.Average();
    }

    /// <summary>Promedio trimestral = media de las categorías oficiales que tengan valor.</summary>
    public static decimal? ComputeTrimesterAverage(IEnumerable<(string? Type, decimal? Score)> scores)
    {
        var parts = new List<decimal>(3);
        var list = scores.ToList();
        foreach (var type in OfficialTypes)
        {
            var avg = AverageByType(list, type);
            if (avg.HasValue)
                parts.Add(avg.Value);
        }

        return parts.Count == 0 ? null : parts.Average();
    }

    /// <summary>Promedio final = media de T1, T2 y T3 que tengan valor. Vacío no se trata como 0.</summary>
    public static decimal? ComputeFinalAverage(decimal? t1, decimal? t2, decimal? t3)
    {
        var parts = new[] { t1, t2, t3 }.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return parts.Count == 0 ? null : parts.Average();
    }
}
