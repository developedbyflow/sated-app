using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sated.Data;
using Sated.Parsing;

var survey = args.Length > 0
    ? args[0]
    : "../UsdaCoverageQuery/data/surveyDownload.json";

var connection = args.Length > 1
    ? args[1]
    : "Host=localhost;Port=5432;Database=sated;Username=sated;Password=sated";

if (!File.Exists(survey))
{
    Console.Error.WriteLine($"Survey file not found: {Path.GetFullPath(survey)}");
    return 1;
}

var options = new DbContextOptionsBuilder<SatedDbContext>().UseNpgsql(connection).Options;
await using var context = new SatedDbContext(options);

var already = await context.Set<Sated.Data.Entities.FoodServing>().IgnoreQueryFilters().CountAsync();

if (already > 0)
{
    Console.Error.WriteLine($"FoodServings already holds {already} rows, and this tool only fills an empty table.");
    Console.Error.WriteLine("To rebuild them, empty the table by hand first:");
    Console.Error.WriteLine("  docker exec sated-db psql -U sated -d sated -c 'DELETE FROM \"FoodServings\";'");
    return 1;
}

Console.WriteLine($"Reading {Path.GetFullPath(survey)}");

using var json = File.OpenRead(survey);
var file = JsonSerializer.Deserialize<SurveyFile>(json)!;

var bySurveyId = file.Foods.ToDictionary(food => food.FdcId);

var stored = await context.Foods
    .IgnoreQueryFilters()
    .Where(food => food.FdcId != null)
    .ToListAsync();

Console.WriteLine($"{stored.Count} catalogue foods carry an FdcId.");

var missing = 0;
var servings = 0;
var typical = 0;

foreach (var food in stored)
{
    if (!bySurveyId.TryGetValue(food.FdcId!.Value, out var surveyed))
    {
        missing++;
        continue;
    }

    var portions = SurveyPortions.Of(surveyed);

    food.Servings.AddRange(portions);
    food.TypicalGrams = SurveyPortions.TypicalGramsOf(surveyed);

    servings += portions.Length;
    typical += food.TypicalGrams is null ? 0 : 1;
}

await context.SaveChangesAsync();

Console.WriteLine($"{servings} servings written, across {stored.Count - missing} foods.");
Console.WriteLine($"{typical} foods carry a typical amount; {stored.Count - missing - typical} do not.");

if (missing > 0)
{
    Console.Error.WriteLine($"{missing} stored foods were not in the survey file at all.");
}

return 0;
