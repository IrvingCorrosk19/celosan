using SchoolManager.Dtos;
using SchoolManager.Helpers;
using SchoolManager.Services.Implementations;
using Xunit;

namespace SchoolManager.Tests;

public class OfficialGradeSchemeIntegrationTests
{
    private static readonly (string? Type, decimal? Score)[] LegacyPairs =
    {
        ("notas de apreciación", 4.0m),
        ("ejercicios diarios", 3.5m),
        ("examen final", 5.0m)
    };

    private static readonly (string? Type, decimal? Score)[] WeightedPairs =
    {
        ("Unidireccional", 4.0m),
        ("Autoevaluación", 4.5m),
        ("Coevaluación", 4.0m)
    };

    [Fact]
    public void Legacy_calculator_formula_is_unchanged()
    {
        var expected = (4.0m + 3.5m + 5.0m) / 3m;
        Assert.Equal(expected, OfficialTrimesterAverageCalculator.ComputeTrimesterAverage(LegacyPairs));
        Assert.Null(OfficialTrimesterAverageCalculator.ComputeTrimesterAverage(Array.Empty<(string?, decimal?)>()));
        Assert.Equal(4.0m, OfficialTrimesterAverageCalculator.ComputeTrimesterAverage(new (string?, decimal?)[]
        {
            ("notas de apreciación", 4.0m)
        }));
    }

    [Fact]
    public void Year_2026_T1_and_T2_use_legacy_exactly()
    {
        var legacy = OfficialTrimesterAverageCalculator.ComputeTrimesterAverage(LegacyPairs);

        Assert.Equal(legacy, OfficialGradeService.FromActivities(LegacyPairs, "2026", "1T").Score);
        Assert.Equal(legacy, OfficialGradeService.FromActivities(LegacyPairs, "2026", "2T").Score);
        Assert.False(OfficialGradeService.FromActivities(LegacyPairs, "2026", "2T").IsImported);
    }

    [Fact]
    public void Year_2026_T3_without_new_categories_keeps_legacy()
    {
        var legacy = OfficialTrimesterAverageCalculator.ComputeTrimesterAverage(LegacyPairs);
        var t3 = OfficialGradeService.FromActivities(LegacyPairs, "2026", "3T");
        Assert.Equal(legacy, t3.Score);
        Assert.False(t3.IsImported);
    }

    [Fact]
    public void Year_2026_T3_uses_weighted_once_new_category_exists()
    {
        var mixed = LegacyPairs.Concat(WeightedPairs).ToArray();
        var t3 = OfficialGradeService.FromActivities(mixed, "2026", "3T");

        Assert.Equal(4.1m, t3.Score);
        Assert.Equal(OfficialTrimesterGradeOrigin.Activities, t3.Origin);
        Assert.Null(OfficialGradeService.FromActivities(LegacyPairs, "2026", "3T", new[] { "Unidireccional" }).Score);
    }

    [Fact]
    public void Year_2027_all_trimesters_use_weighted()
    {
        Assert.Equal(4.1m, OfficialGradeService.FromActivities(WeightedPairs, "2027", "1T").Score);
        Assert.Equal(4.1m, OfficialGradeService.FromActivities(WeightedPairs, "2027", "2T").Score);
        Assert.Equal(4.1m, OfficialGradeService.FromActivities(WeightedPairs, "2027", "3T").Score);
        Assert.Null(OfficialGradeService.FromActivities(LegacyPairs, "2027", "1T").Score);
    }

    [Fact]
    public void Imported_overrides_weighted_activities()
    {
        var weighted = OfficialGradeService.FromActivities(WeightedPairs, "2026", "3T");
        Assert.Equal(4.1m, weighted.Score);

        var official = OfficialGradeService.SelectOfficial(4.5m, Guid.NewGuid(), WeightedPairs, "2026", "3T", hasImported: true);
        Assert.Equal(4.5m, official.Score);
        Assert.True(official.IsImported);
        Assert.Equal(OfficialTrimesterGradeOrigin.Imported, official.Origin);

        var afterDelete = OfficialGradeService.SelectOfficial(null, null, WeightedPairs, "2026", "3T");
        Assert.Equal(4.1m, afterDelete.Score);
        Assert.False(afterDelete.IsImported);
    }
}
