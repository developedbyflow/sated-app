namespace Sated.Parsing;

public class NotConfiguredMealParser : IMealParser
{
    public Task<ParsedMeal?> Parse(
        string text, string catalogue, CancellationToken cancellation) =>
        Task.FromResult<ParsedMeal?>(null);
}
