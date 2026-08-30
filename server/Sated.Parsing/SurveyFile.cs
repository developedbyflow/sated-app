using System.Text.Json.Serialization;

namespace Sated.Parsing;

public record SurveyFile(
    [property: JsonPropertyName("SurveyFoods")] IReadOnlyList<SurveyFood> Foods
);

public record SurveyFood(
    [property: JsonPropertyName("fdcId")] int FdcId,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("foodNutrients")] IReadOnlyList<SurveyNutrient> Nutrients,
    [property: JsonPropertyName("wweiaFoodCategory")] SurveyCategory? Category
);

public record SurveyNutrient(
    [property: JsonPropertyName("nutrient")] NutrientIdentity Nutrient,
    [property: JsonPropertyName("amount")] double? Amount
);

public record NutrientIdentity(
    [property: JsonPropertyName("number")] string Number
);

public record SurveyCategory(
    [property: JsonPropertyName("wweiaFoodCategoryDescription")] string Description
);
