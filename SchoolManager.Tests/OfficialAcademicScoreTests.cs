using SchoolManager.Helpers;
using Xunit;

namespace SchoolManager.Tests;

public class OfficialAcademicScoreTests
{
    [Theory]
    [InlineData(4.04, 4.0)]
    [InlineData(4.05, 4.1)]
    [InlineData(4.06, 4.1)]
    [InlineData(4.0, 4.0)]
    [InlineData(4.14, 4.1)]
    [InlineData(4.15, 4.2)]
    public void Rounds_one_decimal_away_from_zero(decimal value, decimal expected)
    {
        Assert.Equal(expected, OfficialAcademicScore.Round(value));
    }

    [Fact]
    public void Null_stays_null()
    {
        Assert.Null(OfficialAcademicScore.Round((decimal?)null));
    }
}
