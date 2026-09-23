using SchoolManager.Helpers;
using Xunit;

namespace SchoolManager.Tests;

public class EvaluationActivityTypesTests
{
    [Fact]
    public void Weighted_catalog_exposes_percentages_but_persists_short_values()
    {
        var types = EvaluationActivityTypes.GetAvailable(EvaluationScheme.Weighted801010);
        Assert.Equal(3, types.Count);
        Assert.Contains(types, t => t.Value == "Unidireccional" && t.Label.Contains("80"));
        Assert.Contains(types, t => t.Value == "Autoevaluación" && t.Label.Contains("10"));
        Assert.Contains(types, t => t.Value == "Coevaluación" && t.Label.Contains("10"));
        Assert.DoesNotContain(types, t => t.Value.Contains("Recuperación", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Legacy_catalog_keeps_current_teacher_types()
    {
        var types = EvaluationActivityTypes.GetAvailable(EvaluationScheme.Legacy);
        Assert.Contains(types, t => t.Value == "Notas de apreciación");
        Assert.Contains(types, t => t.Value == "Ejercicios diarios");
        Assert.Contains(types, t => t.Value == "Examen Final");
        Assert.Contains(types, t => t.Value == "Recuperación");
    }

    [Theory]
    [InlineData("Unidireccional", EvaluationScheme.Weighted801010, true, "Unidireccional")]
    [InlineData("Unidireccional — 80 %", EvaluationScheme.Weighted801010, true, "Unidireccional")]
    [InlineData("autoevaluación", EvaluationScheme.Weighted801010, true, "Autoevaluación")]
    [InlineData("Examen Final", EvaluationScheme.Weighted801010, false, "")]
    [InlineData("Notas de apreciación", EvaluationScheme.Weighted801010, false, "")]
    [InlineData("Examen Final", EvaluationScheme.Legacy, true, "Examen Final")]
    [InlineData("Unidireccional", EvaluationScheme.Legacy, false, "")]
    public void Canonicalize_rejects_types_outside_declared_scheme(
        string input, EvaluationScheme scheme, bool allowed, string expected)
    {
        var ok = EvaluationActivityTypes.TryCanonicalize(input, scheme, out var canonical);
        Assert.Equal(allowed, ok);
        Assert.Equal(expected, canonical);
    }
}
