namespace FieldOps.Api.Reports;

public static class TrackingCodeNormalizer
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int CodeLength = 12;

    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var cleaned = input
            .Trim()
            .ToUpperInvariant()
            .Replace("-", "")
            .Replace(" ", "");

        if (cleaned.Length != CodeLength)
        {
            return null;
        }

        foreach (var character in cleaned)
        {
            if (!Alphabet.Contains(character))
            {
                return null;
            }
        }

        return cleaned;
    }
}