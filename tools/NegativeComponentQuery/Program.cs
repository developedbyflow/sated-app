using System.Text.Json;
using System.Text.Json.Serialization;
using Sated.Scoring;

// FsCheck found that FatQuality.UnsaturatedShare can return a negative number, which ComponentStrategy
// documents as impossible: "a component score between 0 and 100". This counts how far that reaches
// into the real catalogue before deciding what to do about it.

var shipped = Calibration.Load();
var combiner = new ScoreCombiner(
    new GeneralStrategies(shipped.SatietyScale, shipped.DensityScales, shipped.ReferenceMealGrams),
    shipped.Rules);

var foods = JsonSerializer.Deserialize<SurveyFoodsFile>(
    File.ReadAllText("../UsdaCoverageQuery/data/surveyDownload.json"))!.Foods;

var negative = new List<(string Food, string Lens, string Component, double Score)>();
var counted = 0;

foreach (var food in foods)
{
    var amounts = food.FoodNutrients.Where(e => e.Amount is not null)
        .ToDictionary(e => e.Nutrient.Number, e => e.Amount!.Value);

    if (!Codes.Required.All(amounts.ContainsKey)) continue;
    counted++;

    var input = new FoodInput(
        Category: food.WweiaFoodCategory?.Description ?? "Not included in a food category",
        Calories: amounts["208"], Protein: amounts["203"], Fat: amounts["204"],
        Fiber: amounts["291"], VitaminA: amounts["320"], VitaminC: amounts["401"],
        VitaminE: amounts["323"], Calcium: amounts["301"], Iron: amounts["303"],
        Magnesium: amounts["304"], Potassium: amounts["306"], SaturatedFat: amounts["606"],
        Sodium: amounts["307"], VitaminD: amounts["328"], Thiamine: amounts["404"]);

    foreach (var lens in shipped.Lenses)
    {
        var score = combiner.Combine(input, lens);

        foreach (var (name, value) in new (string, ComponentValue?)[]
            { ("satiety", score.Satiety), ("density", score.Density),
              ("protein", score.ProteinQuality) })
        {
            if (value is not null && (value.Score < 0 || value.Score > 100))
            {
                negative.Add((food.Description, lens.Name, name, value.Score));
            }
        }

        if (score.Value < 0 || score.Value > 100)
        {
            negative.Add((food.Description, lens.Name, "COMBINAT", score.Value));
        }
    }
}

Console.WriteLine($"Catalog: {counted} alimente · {shipped.Lenses.Length} lentile");
Console.WriteLine($"componente în afara intervalului 0-100: {negative.Count}\n");

foreach (var group in negative.GroupBy(n => (n.Lens, n.Component)))
{
    Console.WriteLine($"  {group.Key.Lens,-13} {group.Key.Component,-9} {group.Count(),5} · " +
        $"minim {group.Min(n => n.Score):F2}");
}

foreach (var n in negative.OrderBy(n => n.Score).Take(8))
{
    Console.WriteLine($"    {n.Score,8:F2}  {n.Component,-9} {n.Lens,-13} {n.Food[..Math.Min(44, n.Food.Length)]}");
}

public static class Codes
{
    public static readonly string[] Required =
        ["208","203","204","291","320","401","323","301","303","304","306","606","307","328","404"];
}
public record Nutrient([property: JsonPropertyName("number")] string Number);
public record FoodNutrient([property: JsonPropertyName("nutrient")] Nutrient Nutrient,
    [property: JsonPropertyName("amount")] double? Amount);
public record WweiaCategory([property: JsonPropertyName("wweiaFoodCategoryDescription")] string Description);
public record FoodItem([property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("foodNutrients")] List<FoodNutrient> FoodNutrients,
    [property: JsonPropertyName("wweiaFoodCategory")] WweiaCategory? WweiaFoodCategory);
public record SurveyFoodsFile([property: JsonPropertyName("SurveyFoods")] List<FoodItem> Foods);
