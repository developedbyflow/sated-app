using Microsoft.EntityFrameworkCore;
using Sated.Data;
using Sated.Parsing;

namespace Sated.Services;

public record MealParseItem(
    int FoodId, string Description, string RawText, double QuantityGrams, bool QuantityEstimated);

public record MealParse(MealParseItem[] Items, string[] Unrecognised);

public class MealParsing(SatedDbContext database, IMealParser parser)
{
    public async Task<MealParse?> Of(string text, CancellationToken cancellation)
    {
        var reachable = await database.Foods
            .Select(food => new CatalogueRow(food.Id, food.Description, food.OwnerId != null))
            .ToListAsync(cancellation);

        var parsed = await parser.Parse(text, Prompt(reachable), cancellation);

        if (parsed is null)
        {
            return null;
        }

        var byId = reachable.ToDictionary(row => row.Id);
        var items = new List<MealParseItem>();
        var unrecognised = new List<string>(parsed.Unrecognised);

        foreach (var item in parsed.Items)
        {
            if (item.FoodId is int id
                && byId.TryGetValue(id, out var row)
                && item.QuantityGrams > 0)
            {
                items.Add(new MealParseItem(
                    row.Id, row.Description, item.RawText, item.QuantityGrams,
                    item.QuantityEstimated));
            }
            else
            {
                unrecognised.Add(item.RawText);
            }
        }

        return new MealParse([.. items], [.. unrecognised]);
    }

    private static string Prompt(List<CatalogueRow> reachable) =>
        CataloguePrompt.Of(
            Entries(reachable.Where(row => !row.IsMine)),
            Entries(reachable.Where(row => row.IsMine)));

    private static IEnumerable<CatalogueEntry> Entries(IEnumerable<CatalogueRow> rows) =>
        rows.Select(row => new CatalogueEntry(row.Id, row.Description));

    private record CatalogueRow(int Id, string Description, bool IsMine);
}
