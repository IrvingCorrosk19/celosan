namespace SchoolManager.Helpers;

/// <summary>
/// Despacha el cálculo de activities al calculator del esquema resuelto.
/// Los consumidores no eligen Legacy/Weighted; usan IOfficialGradeService.
/// </summary>
public static class OfficialSchemeAverageCalculator
{
    public static decimal? ComputeTrimesterAverage(
        EvaluationScheme scheme,
        IEnumerable<(string? Type, decimal? Score)> scores)
    {
        return scheme == EvaluationScheme.Weighted801010
            ? Weighted801010TrimesterAverageCalculator.ComputeTrimesterAverage(scores)
            : OfficialTrimesterAverageCalculator.ComputeTrimesterAverage(scores);
    }
}
