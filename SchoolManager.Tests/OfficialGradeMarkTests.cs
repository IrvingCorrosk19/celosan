using SchoolManager.Helpers;
using SchoolManager.Services.Implementations;
using Xunit;

namespace SchoolManager.Tests;

public class OfficialGradeMarkTests
{
    [Theory]
    [InlineData("N/A", ImportedTrimesterGradeStatus.NoAsistio)]
    [InlineData("n/a", ImportedTrimesterGradeStatus.NoAsistio)]
    [InlineData("NA", ImportedTrimesterGradeStatus.NoAsistio)]
    [InlineData("SN", ImportedTrimesterGradeStatus.SinNota)]
    [InlineData("sn", ImportedTrimesterGradeStatus.SinNota)]
    public void Parses_special_tokens(string raw, string expectedStatus)
    {
        Assert.True(OfficialGradeMark.TryParseToken(raw, out var status, out var score, out var error));
        Assert.Equal(expectedStatus, status);
        Assert.Null(score);
        Assert.Null(error);
        Assert.Equal(
            expectedStatus == ImportedTrimesterGradeStatus.NoAsistio ? "N/A" : "SN",
            OfficialGradeMark.Display(null, status, true));
    }

    [Fact]
    public void Empty_token_is_skip()
    {
        Assert.True(OfficialGradeMark.TryParseToken("  ", out var status, out var score, out _));
        Assert.Null(status);
        Assert.Null(score);
    }

    [Fact]
    public void Numeric_token_is_not_special()
    {
        Assert.False(OfficialGradeMark.TryParseToken("4.2", out var status, out _, out _));
        Assert.Null(status);
        Assert.Equal("4.2", OfficialGradeMark.Display(4.2m));
        Assert.Equal("—", OfficialGradeMark.Display(null));
    }

    [Fact]
    public void Imported_NA_overrides_activities_and_is_not_zero()
    {
        var pairs = new (string? Type, decimal? Score)[]
        {
            ("notas de apreciación", 4.0m),
            ("ejercicios diarios", 3.5m),
            ("examen final", 5.0m)
        };

        var official = OfficialGradeService.SelectOfficial(
            null, Guid.NewGuid(), pairs, "2026", "3T",
            hasImported: true,
            importedStatus: ImportedTrimesterGradeStatus.NoAsistio);

        Assert.Null(official.Score);
        Assert.Equal("N/A", official.Display);
        Assert.True(official.IsImported);
        Assert.Equal(4.0m, OfficialTrimesterAverageCalculator.ComputeFinalAverage(null, 4.0m, 4.0m));
        Assert.Null(OfficialTrimesterAverageCalculator.ComputeFinalAverage(null, null, null));
        Assert.False(OfficialGradeMark.IsApproved(null));
    }
}
