using System.Text.RegularExpressions;

namespace Sated.Parsing;

public static partial class Slug
{
    public static string From(string description) =>
        NotALetterOrADigit().Replace(description.ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NotALetterOrADigit();
}
