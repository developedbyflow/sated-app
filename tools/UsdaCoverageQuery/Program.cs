using System.Text.Json;
using System.Text.Json.Serialization;

var json = File.ReadAllText("data/FoodData_Central_foundation_food_json_2026-04-30.json");
var data = JsonSerializer.Deserialize<FoundationFoodsFile>(json); 

var foundationFoods = data!.Foods.OfType<FoodItem>().ToList();
Console.WriteLine($"Foundation Foods: {data.Foods.Count - foundationFoods.Count} intrări null ignorate din {data.Foods.Count}.");

var srLegacyJson = File.ReadAllText("data/FoodData_Central_sr_legacy_food_json_2018-04.json");
var srLegacyData = JsonSerializer.Deserialize<SrLegacyFoodsFile>(srLegacyJson);

var fnddsJson = File.ReadAllText("data/surveyDownload.json");
var fnddsData = JsonSerializer.Deserialize<SurveyFoodsFile>(fnddsJson);

var satietyNutrients = new HashSet<string> { "208", "203", "291", "204" };
var densityNutrients = new HashSet<string> { "203", "291", "320", "401", "323", "301", "303", "304", "306", "606", "539", "307" };

var leucineNutrient = new HashSet<string> { "504" };
var vitaminDNutrient = new HashSet<string> { "328" };
var thiamineNutrient = new HashSet<string> { "404" };

var groups = new (string Name, HashSet<string> Codes)[]
{
    ("Sațietate (4)", satietyNutrients),
    ("Densitate (12)", densityNutrients),
    ("Leucină", leucineNutrient),
    ("Vitamina D", vitaminDNutrient),
    ("Tiamină", thiamineNutrient),
};

var sources = new (string Name, List<FoodItem> Foods)[]
{
    ("Foundation Foods", foundationFoods),
    ("SR Legacy", srLegacyData!.Foods),
    ("FNDDS", fnddsData!.Foods),
};

foreach (var source in sources)
{
    Console.WriteLine();
    Console.WriteLine($"--- {source.Name} ({source.Foods.Count} alimente) ---");

    foreach (var group in groups)
    {
        Console.WriteLine($"{group.Name,-16} {PercentageWithNutrients(source.Foods, group.Codes):F1}%");
    }
}

static bool HasAllNutrients(FoodItem food, HashSet<string> requiredCodes)
{
    if (food.FoodNutrients is null)
    {
        return false;
    }

    var availableCodes = food.FoodNutrients
        .Where(fn => fn.Amount is not null)
        .Select(fn => fn.Nutrient.Number)
        .ToHashSet();

    return requiredCodes.All(availableCodes.Contains);
}

static double PercentageWithNutrients(List<FoodItem> foods, HashSet<string> requiredCodes)
{
    double count = foods.Count(food => HasAllNutrients(food, requiredCodes));
    return count / foods.Count * 100;
}


public record Nutrient(
    [property: JsonPropertyName("number")] string Number,
    [property: JsonPropertyName("name")] string Name
); 

public record FoodNutrient(
    [property: JsonPropertyName("nutrient")] Nutrient Nutrient,
    [property: JsonPropertyName("amount")] double? Amount
);

public record FoodPortion(
    [property: JsonPropertyName("gramWeight")] double GramWeight
);

public record FoodItem(
    [property: JsonPropertyName("fdcId")] int FdcId,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("foodNutrients")] List<FoodNutrient>? FoodNutrients,
    [property: JsonPropertyName("foodPortions")] List<FoodPortion>? FoodPortions
);

public record FoundationFoodsFile(
    [property: JsonPropertyName("FoundationFoods")] List<FoodItem?> Foods
);

public record SrLegacyFoodsFile(
    [property: JsonPropertyName("SRLegacyFoods")] List<FoodItem> Foods
);

public record SurveyFoodsFile(
    [property: JsonPropertyName("SurveyFoods")] List<FoodItem> Foods
);