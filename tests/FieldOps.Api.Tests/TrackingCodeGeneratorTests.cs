using FieldOps.Api.Tracking;

namespace FieldOps.Api.Tests;

public class TrackingCodeGeneratorTests
{
    private const string AllowedAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    [Fact]
    public void Generate_ReturnsTwelveCharacters()
    {
        var generator = new TrackingCodeGenerator();

        var code = generator.Generate();

        Assert.Equal(12, code.Length);
    }

    [Fact]
    public void Generate_UsesOnlyAllowedCharacters()
    {
        var generator = new TrackingCodeGenerator();

        for (var i = 0; i < 1000; i++)
        {
            var code = generator.Generate();
            foreach (var c in code)
            {
                Assert.True(AllowedAlphabet.Contains(c), $"Unexpected character '{c}' in code '{code}'");
            }
        }
    }

    [Fact]
    public void Generate_Produces1000DistinctCodes()
    {
        var generator = new TrackingCodeGenerator();

        var codes = Enumerable.Range(0, 1000).Select(_ => generator.Generate()).ToList();

        Assert.Equal(1000, codes.Distinct().Count());
    }

    [Fact]
    public void Generate_NeverUsesConfusingLetters()
    {
        var generator = new TrackingCodeGenerator();
        var forbidden = new[] { 'I', 'L', 'O', 'U' };

        for (var i = 0; i < 1000; i++)
        {
            var code = generator.Generate();
            Assert.True(code.IndexOfAny(forbidden) < 0, $"Confusing letter in code '{code}'");
        }
    }
}