using System.Text.Json.Serialization;

namespace Sated.Parsing;

public record SurveyFile(
    [property: JsonPropertyName("SurveyFoods")] IReadOnlyList<SurveyFood> Foods
);

public record SurveyFood(
    [property: JsonPropertyName("fdcId")] int FdcId,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("foodNutrients")] IReadOnlyList<SurveyNutrient> Nutrients,
    [property: JsonPropertyName("wweiaFoodCategory")] SurveyCategory? Category,
    [property: JsonPropertyName("foodPortions")] IReadOnlyList<SurveyPortion>? Portions = null
);

public record SurveyPortion(
    [property: JsonPropertyName("gramWeight")] double GramWeight,
    [property: JsonPropertyName("portionDescription")] string? Description,
    [property: JsonPropertyName("sequenceNumber")] int SequenceNumber
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
