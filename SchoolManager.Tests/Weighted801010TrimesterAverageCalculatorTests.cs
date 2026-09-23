using SchoolManager.Helpers;
using Xunit;

namespace SchoolManager.Tests;

public class Weighted801010TrimesterAverageCalculatorTests
{
    [Fact]
    public void Complete_U_A_C_rounds_4_05_to_4_1()
    {
        var scores = new (string? Type, decimal? Score)[]
        {
            ("Unidireccional", 4.0m),
            ("Autoevaluación", 4.5m),
            ("Coevaluación", 4.0m)
        };

        var raw = (4.0m * 0.80m) + (4.5m * 0.10m) + (4.0m * 0.10m);
        Assert.Equal(4.05m, raw);
        Assert.Equal(4.1m, Weighted801010TrimesterAverageCalculator.ComputeTrimesterAverage(scores));
    }

    [Fact]
    public void Missing_C_reweights_80_and_10()
    {
        var scores = new (string? Type, decimal? Score)[]
        {
            ("Unidireccional", 4.0m),
            ("Autoevaluación", 5.0m)
        };

        var raw = ((4.0m * 80m) + (5.0m * 10m)) / 90m;
        Assert.True(raw > 4.11m && raw < 4.12m);
        Assert.Equal(4.1m, OfficialAcademicScore.Round(raw));
        Assert.Equal(4.1m, Weighted801010TrimesterAverageCalculator.ComputeTrimesterAverage(scores));
    }

    [Fact]
    public void Only_U_returns_U()
    {
        var scores = new (string? Type, decimal? Score)[]
        {
            ("Unidireccional", 4.0m)
        };

        Assert.Equal(4.0m, Weighted801010TrimesterAverageCalculator.ComputeTrimesterAverage(scores));
    }

    [Fact]
    public void No_categories_returns_null()
    {
        var scores = new (string? Type, decimal? Score)[]
        {
            ("notas de apreciación", 4.0m),
            ("examen final", 5.0m),
            ("recuperación", 3.0m)
        };

        Assert.Null(Weighted801010TrimesterAverageCalculator.ComputeTrimesterAverage(scores));
    }

    [Fact]
    public void Multiple_unidirectional_activities_are_simple_average()
    {
        var scores = new (string? Type, decimal? Score)[]
        {
            ("Unidireccional", 5.0m),
            ("unidireccional", 3.0m),
            (" UNIDIRECCIONAL ", 4.0m),
            ("Autoevaluación", 4.5m),
            ("Coevaluación", 4.0m)
        };

        // U = 4.0 → same as complete case → 4.1
        Assert.Equal(4.1m, Weighted801010TrimesterAverageCalculator.ComputeTrimesterAverage(scores));
    }

    [Fact]
    public void Legacy_types_and_unknown_types_are_ignored()
    {
        var scores = new (string? Type, decimal? Score)[]
        {
            ("notas de apreciación", 1.0m),
            ("ejercicios diarios", 1.0m),
            ("examen final", 1.0m),
            ("Unidireccional", 4.0m),
            ("autoevaluacion", 5.0m)
        };

        Assert.Equal(4.0m, Weighted801010TrimesterAverageCalculator.ComputeTrimesterAverage(scores));
    }

    [Fact]
    public void Null_scores_do_not_count_as_zero()
    {
        var scores = new (string? Type, decimal? Score)[]
        {
            ("Unidireccional", 4.0m),
            ("Autoevaluación", null),
            ("Coevaluación", null)
        };

        Assert.Equal(4.0m, Weighted801010TrimesterAverageCalculator.ComputeTrimesterAverage(scores));
    }
}
