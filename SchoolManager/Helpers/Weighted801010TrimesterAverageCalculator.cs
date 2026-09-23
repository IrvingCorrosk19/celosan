namespace SchoolManager.Helpers;

/// <summary>
/// Calculador 80/10/10. Solo reconoce Unidireccional, Autoevaluación y Coevaluación
/// (Trim + case-insensitive, sin fuzzy matching). Tipos legacy y Recuperación se ignoran.
/// Componentes faltantes se reponderan; no se tratan como 0.
/// </summary>
public static class Weighted801010TrimesterAverageCalculator
{
    public const string TypeUnidireccional = "unidireccional";
    public const string TypeAutoevaluacion = "autoevaluación";
    public const string TypeCoevaluacion = "coevaluación";

    public const decimal WeightUnidireccional = 80m;
    public const decimal WeightAutoevaluacion = 10m;
    public const decimal WeightCoevaluacion = 10m;

    public static readonly string[] OfficialTypes =
    {
        TypeUnidireccional,
        TypeAutoevaluacion,
        TypeCoevaluacion
    };

    public static string NormalizeType(string? type) =>
        (type ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsOfficialType(string? type)
    {
        var n = NormalizeType(type);
        return n == TypeUnidireccional || n == TypeAutoevaluacion || n == TypeCoevaluacion;
    }

    /// <summary>Promedio simple de una categoría. Null si no hay scores con valor.</summary>
    public static decimal? AverageByType(IEnumerable<(string? Type, decimal? Score)> scores, string category)
    {
        var normalized = NormalizeType(category);
        var values = scores
            .Where(s => s.Score.HasValue && NormalizeType(s.Type) == normalized)
            .Select(s => s.Score!.Value)
            .ToList();
        return values.Count == 0 ? null : values.Average();
    }

    /// <summary>
    /// (U×80 + A×10 + C×10) / pesos presentes. Si no hay ninguna categoría, null.
    /// El resultado oficial se redondea a 1 decimal (AwayFromZero).
    /// </summary>
    public static decimal? ComputeTrimesterAverage(IEnumerable<(string? Type, decimal? Score)> scores)
    {
        var list = scores.ToList();
        var u = AverageByType(list, TypeUnidireccional);
        var a = AverageByType(list, TypeAutoevaluacion);
        var c = AverageByType(list, TypeCoevaluacion);

        decimal weightedSum = 0m;
        decimal weightSum = 0m;

        if (u.HasValue)
        {
            weightedSum += u.Value * WeightUnidireccional;
            weightSum += WeightUnidireccional;
        }

        if (a.HasValue)
        {
            weightedSum += a.Value * WeightAutoevaluacion;
            weightSum += WeightAutoevaluacion;
        }

        if (c.HasValue)
        {
            weightedSum += c.Value * WeightCoevaluacion;
            weightSum += WeightCoevaluacion;
        }

        if (weightSum == 0m)
            return null;

        return OfficialAcademicScore.Round(weightedSum / weightSum);
    }
}
