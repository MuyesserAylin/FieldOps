using FieldOps.Api.Teams;
using Xunit;

namespace FieldOps.Api.Tests.Teams;

public class TeamLabelsTests
{
    [Theory]
    [InlineData("Idle", "Boşta")]
    [InlineData("OfferPending", "Teklif bekliyor")]
    [InlineData("OnRoute", "Yolda")]
    [InlineData("OnSite", "İşte")]
    [InlineData("Offline", "Çevrimdışı")]
    public void Status_known_values_return_turkish(string input, string expected)
        => Assert.Equal(expected, TeamStatusLabels.Get(input));

    [Theory]
    [InlineData("Gas", "Doğalgaz")]
    [InlineData("Plumbing", "Su tesisatı")]
    [InlineData("Electrical", "Elektrik")]
    [InlineData("RoadWork", "Yol")]
    public void Skill_known_values_return_turkish(string input, string expected)
        => Assert.Equal(expected, SkillLabels.Get(input));

    [Fact]
    public void Unknown_values_are_returned_as_is()
    {
        Assert.Equal("Bilinmeyen", TeamStatusLabels.Get("Bilinmeyen"));
        Assert.Equal("Bilinmeyen", SkillLabels.Get("Bilinmeyen"));
    }
}