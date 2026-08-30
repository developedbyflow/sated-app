namespace Sated.Api.Dtos;

public record NutrientAmountsDto(
    double Calories,
    double Protein,
    double Fat,
    double Fiber,
    double SaturatedFat,
    double Sodium,
    double? VitaminA,
    double? VitaminC,
    double? VitaminD,
    double? VitaminE,
    double? Thiamine,
    double? Calcium,
    double? Iron,
    double? Magnesium,
    double? Potassium,
    double? Leucine
);
