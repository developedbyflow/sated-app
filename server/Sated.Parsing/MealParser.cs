namespace Sated.Parsing;

public record CatalogueEntry(int Id, string Description);

public record ParsedItem(int? FoodId, string RawText, double QuantityGrams, bool QuantityEstimated);

public record ParsedMeal(ParsedItem[] Items, string[] Unrecognised);

public interface IMealParser
{
    Task<ParsedMeal?> Parse(
        string text, string catalogue, CancellationToken cancellation);
}
