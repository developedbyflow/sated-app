using System.Text.Json;
using System.Text.Json.Serialization;

var json = File.ReadAllText("data/FoodData_Central_foundation_food_json_2026-04-30.json");
var data = JsonSerializer.Deserialize<FoundationFoodsFile>(json); 

Console.WriteLine($"Foundation Foods: {data!.Foods.Count} alimente.");

var srLegacyJson = File.ReadAllText("data/FoodData_Central_sr_legacy_food_json_2018-04.json");
var srLegacyData = JsonSerializer.Deserialize<SrLegacyFoodsFile>(srLegacyJson);
Console.WriteLine($"SR Legacy: {srLegacyData!.Foods.Count} alimente.");

var fnddsJson = File.ReadAllText("data/surveyDownload.json");
var fnddsData = JsonSerializer.Deserialize<SurveyFoodsFile>(fnddsJson);
Console.WriteLine($"FNDDS: {fnddsData!.Foods.Count} alimente.");

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
    [property: JsonPropertyName("FoundationFoods")] List<FoodItem> Foods
);

public record SrLegacyFoodsFile(
    [property: JsonPropertyName("SRLegacyFoods")] List<FoodItem> Foods
);

public record SurveyFoodsFile(
    [property: JsonPropertyName("SurveyFoods")] List<FoodItem> Foods
);