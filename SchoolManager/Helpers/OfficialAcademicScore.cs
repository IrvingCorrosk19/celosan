namespace SchoolManager.Helpers;

/// <summary>
/// Redondeo oficial de notas académicas. Única regla: 1 decimal, AwayFromZero.
/// 4.04 → 4.0; 4.05 → 4.1; 4.06 → 4.1.
/// </summary>
public static class OfficialAcademicScore
{
    public const int DecimalPlaces = 1;
    public const decimal Min = 1.0m;
    public const decimal Max = 5.0m;

    public static decimal Round(decimal value) =>
        Math.Round(value, DecimalPlaces, MidpointRounding.AwayFromZero);

    public static decimal? Round(decimal? value) =>
        value.HasValue ? Round(value.Value) : null;

    /// <summary>Null (celda vacía) es válido. Si hay valor, debe estar en 1.0–5.0.</summary>
    public static bool IsValidScore(decimal? score) =>
        !score.HasValue || (score.Value >= Min && score.Value <= Max);
}
