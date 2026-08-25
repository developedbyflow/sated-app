using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sated.Scoring;

// How fast the engine grades, because nobody had ever measured it. A grade is pure arithmetic over
// one record — no I/O, no database, no allocation beyond the score objects — so the number matters
// less for a single food than for the cases that come later: re-grading a user's whole history when
// they switch lens (FR-4 says the history recalculates), or grading a catalogue at import.

const string DataPath = "../UsdaCoverageQuery/data/surveyDownload.json";
const int Rounds = 20;

var shipped = Calibration.Load();

var combiner = new ScoreCombiner(
    new GeneralStrategies(
        shipped.SatietyScale, shipped.DensityScales, shipped.ReferenceMealGrams),
    shipped.Rules);

var lenses = shipped.Lenses;

var foods = JsonSerializer.Deserialize<SurveyFoodsFile>(File.ReadAllText(DataPath))!.Foods;
var catalogue = new List<FoodInput>();

foreach (var food in foods)
{
    var amounts = food.FoodNutrients
        .Where(entry => entry.Amount is not null)
        .ToDictionary(entry => entry.Nutrient.Number, entry => entry.Amount!.Value);

    if (!Codes.Required.All(amounts.ContainsKey))
    {
        continue;
    }

    catalogue.Add(new FoodInput(
        Category: food.WweiaFoodCategory?.Description ?? "Not included in a food category",
        Calories: amounts["208"], Protein: amounts["203"], Fat: amounts["204"],
        Fiber: amounts["291"], VitaminA: amounts["320"], VitaminC: amounts["401"],
        VitaminE: amounts["323"], Calcium: amounts["301"], Iron: amounts["303"],
        Magnesium: amounts["304"], Potassium: amounts["306"], SaturatedFat: amounts["606"],
        Sodium: amounts["307"], VitaminD: amounts["328"], Thiamine: amounts["404"]));
}

Console.WriteLine($"{catalogue.Count} alimente · {lenses.Length} lentile · " +
    $"{RuntimeInformation.OSArchitecture}, .NET {Environment.Version}");

// The first pass pays for JIT compilation. What is wanted is the steady state, not the start-up.
GradeEverything();

var watch = Stopwatch.StartNew();

for (var round = 0; round < Rounds; round++)
{
    GradeEverything();
}

watch.Stop();

var total = (long)Rounds * catalogue.Count * lenses.Length;
var perSecond = total / watch.Elapsed.TotalSeconds;

Console.WriteLine();
Console.WriteLine($"{total:N0} note în {watch.Elapsed.TotalSeconds:F2} s");
Console.WriteLine($"  {perSecond:N0} note pe secundă");
Console.WriteLine($"  {watch.Elapsed.TotalMilliseconds * 1000 / total:F2} microsecunde per notă");
Console.WriteLine();
Console.WriteLine($"tot catalogul, o lentilă:      {catalogue.Count / perSecond * 1000:F1} ms");
Console.WriteLine($"un an de mese (3/zi · 365):    {3 * 365 / perSecond * 1000:F2} ms");

void GradeEverything()
{
    foreach (var food in catalogue)
    {
        foreach (var lens in lenses)
        {
            shipped.GradeFor(combiner.Combine(food, lens), lens);
        }
    }
}

public static class Codes
{
    public static readonly string[] Required =
        ["208", "203", "204", "291", "320", "401", "323", "301", "303", "304", "306", "606", "307",
         "328", "404"];
}

public record Nutrient([property: JsonPropertyName("number")] string Number);

public record FoodNutrient(
    [property: JsonPropertyName("nutrient")] Nutrient Nutrient,
    [property: JsonPropertyName("amount")] double? Amount
);

public record WweiaCategory(
    [property: JsonPropertyName("wweiaFoodCategoryDescription")] string Description
);

public record FoodItem(
    [property: JsonPropertyName("foodNutrients")] List<FoodNutrient> FoodNutrients,
    [property: JsonPropertyName("wweiaFoodCategory")] WweiaCategory? WweiaFoodCategory
);

public record SurveyFoodsFile([property: JsonPropertyName("SurveyFoods")] List<FoodItem> Foods);
