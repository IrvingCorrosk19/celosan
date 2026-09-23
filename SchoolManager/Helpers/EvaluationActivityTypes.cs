namespace SchoolManager.Helpers;

public sealed class EvaluationActivityTypeOption
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public decimal? Weight { get; init; }
}

/// <summary>
/// Catálogo de tipos que el docente puede crear según el esquema declarado
/// (año + trimestre). El porcentaje vive aquí, no en Activity.Type.
/// </summary>
public static class EvaluationActivityTypes
{
    public const string NotasDeApreciacion = "Notas de apreciación";
    public const string EjerciciosDiarios = "Ejercicios diarios";
    public const string ExamenFinal = "Examen Final";
    public const string Recuperacion = "Recuperación";

    public const string Unidireccional = "Unidireccional";
    public const string Autoevaluacion = "Autoevaluación";
    public const string Coevaluacion = "Coevaluación";

    public static IReadOnlyList<EvaluationActivityTypeOption> LegacyTypes { get; } =
        new[]
        {
            new EvaluationActivityTypeOption { Value = NotasDeApreciacion, Label = "Notas de apreciación" },
            new EvaluationActivityTypeOption { Value = EjerciciosDiarios, Label = "Ejercicios diarios" },
            new EvaluationActivityTypeOption { Value = ExamenFinal, Label = "Examen Final" },
            new EvaluationActivityTypeOption { Value = Recuperacion, Label = "Recuperación" }
        };

    public static IReadOnlyList<EvaluationActivityTypeOption> WeightedTypes { get; } =
        new[]
        {
            new EvaluationActivityTypeOption { Value = Unidireccional, Label = "Unidireccional — 80 %", Weight = 80m },
            new EvaluationActivityTypeOption { Value = Autoevaluacion, Label = "Autoevaluación — 10 %", Weight = 10m },
            new EvaluationActivityTypeOption { Value = Coevaluacion, Label = "Coevaluación — 10 %", Weight = 10m }
        };

    public static IReadOnlyList<EvaluationActivityTypeOption> GetAvailable(EvaluationScheme scheme) =>
        scheme == EvaluationScheme.Weighted801010 ? WeightedTypes : LegacyTypes;

    public static string Normalize(string? type) =>
        (type ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsWeightedType(string? type)
    {
        var n = Normalize(StripDisplaySuffix(type));
        return n == Normalize(Unidireccional)
               || n == Normalize(Autoevaluacion)
               || n == Normalize(Coevaluacion);
    }

    public static bool TryCanonicalize(string? input, EvaluationScheme scheme, out string canonical)
    {
        canonical = string.Empty;
        var raw = StripDisplaySuffix(input);
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var n = Normalize(raw);
        foreach (var option in GetAvailable(scheme))
        {
            if (Normalize(option.Value) == n)
            {
                canonical = option.Value;
                return true;
            }
        }

        return false;
    }

    public static bool IsAllowedForCreate(string? type, EvaluationScheme scheme) =>
        TryCanonicalize(type, scheme, out _);

    /// <summary>Quita " — 80 %" si el cliente envió la etiqueta de UI.</summary>
    public static string StripDisplaySuffix(string? input)
    {
        var raw = (input ?? string.Empty).Trim();
        var sep = raw.IndexOf(" — ", StringComparison.Ordinal);
        if (sep > 0)
            raw = raw[..sep].Trim();
        return raw;
    }
}
