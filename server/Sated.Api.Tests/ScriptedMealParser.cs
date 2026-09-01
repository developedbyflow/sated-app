using Sated.Parsing;

namespace Sated.Api.Tests;

public class ScriptedMealParser : IMealParser
{
    public ParsedMeal? Answer { get; set; }

    public string Catalogue { get; private set; } = string.Empty;

    public Task<ParsedMeal?> Parse(string text, string catalogue, CancellationToken cancellation)
    {
        Catalogue = catalogue;

        return Task.FromResult(Answer);
    }
}
