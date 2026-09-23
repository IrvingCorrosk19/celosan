using SchoolManager.Helpers;
using SchoolManager.Services.Implementations;
using Xunit;

namespace SchoolManager.Tests;

public class EvaluationSchemeResolverTests
{
    [Theory]
    [InlineData("2026", "1T", EvaluationScheme.Legacy)]
    [InlineData("2026", "T1", EvaluationScheme.Legacy)]
    [InlineData("2026", "2T", EvaluationScheme.Legacy)]
    [InlineData("2026", "T2", EvaluationScheme.Legacy)]
    [InlineData("2026", "3T", EvaluationScheme.Weighted801010)]
    [InlineData("2026", "T3", EvaluationScheme.Weighted801010)]
    [InlineData("2026-2027", "3T", EvaluationScheme.Weighted801010)]
    [InlineData("2026-2027", "1T", EvaluationScheme.Legacy)]
    [InlineData("2027", "1T", EvaluationScheme.Weighted801010)]
    [InlineData("2027", "2T", EvaluationScheme.Weighted801010)]
    [InlineData("2027", "3T", EvaluationScheme.Weighted801010)]
    [InlineData("2028", "1T", EvaluationScheme.Weighted801010)]
    [InlineData("2025", "3T", EvaluationScheme.Legacy)]
    [InlineData(null, "3T", EvaluationScheme.Legacy)]
    [InlineData("", "3T", EvaluationScheme.Legacy)]
    [InlineData("2026", null, EvaluationScheme.Legacy)]
    public void Resolve_uses_year_and_trimester_not_server_date(
        string? year, string? trimester, EvaluationScheme expected)
    {
        Assert.Equal(expected, EvaluationSchemeResolver.Resolve(year, trimester));
    }

    [Fact]
    public void Effective_2026_3T_stays_legacy_until_new_category_exists()
    {
        Assert.Equal(EvaluationScheme.Legacy, EvaluationSchemeResolver.ResolveEffective("2026", "3T", new[] { "notas de apreciación", "examen final" }));
        Assert.Equal(EvaluationScheme.Weighted801010, EvaluationSchemeResolver.ResolveEffective("2026", "3T", new[] { "notas de apreciación", "Unidireccional" }));
    }

    [Fact]
    public void Effective_2027_never_falls_back_to_legacy()
    {
        Assert.Equal(EvaluationScheme.Weighted801010, EvaluationSchemeResolver.ResolveEffective("2027", "1T", new[] { "notas de apreciación" }));
        Assert.Equal(EvaluationScheme.Weighted801010, EvaluationSchemeResolver.ResolveEffective("2027", "2T", Array.Empty<string>()));
        Assert.Equal(EvaluationScheme.Weighted801010, EvaluationSchemeResolver.ResolveEffective("2027", "3T", new[] { "examen final" }));
    }
}
