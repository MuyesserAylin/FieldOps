using FieldOps.Api.Teams;

namespace FieldOps.Api.Tests.Teams;

public class LocationAgeTests
{
    [Theory]
    [InlineData(0, "0 sn")]
    [InlineData(59, "59 sn")]
    [InlineData(60, "1 dk")]
    [InlineData(61, "1 dk")]
    [InlineData(3599, "59 dk")]
    [InlineData(3600, "1 sa")]
    [InlineData(3660, "1 sa 1 dk")]
    [InlineData(86399, "23 sa 59 dk")]
    [InlineData(86400, "1 gün")]
    [InlineData(1_133_400, "13 gün")]
    [InlineData(-30, "0 sn")]
    public void Format_gives_a_short_turkish_text(int seconds, string expected)
    {
        Assert.Equal(expected, LocationAge.Format(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(59, false)]
    [InlineData(60, false)]
    [InlineData(61, true)]
    [InlineData(1_000_000, true)]
    [InlineData(-30, false)]
    public void IsStale_follows_the_elimination_rule(int seconds, bool expected)
    {
        Assert.Equal(expected, LocationAge.IsStale(TimeSpan.FromSeconds(seconds), 60));
    }
}