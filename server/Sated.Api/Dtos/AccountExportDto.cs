namespace Sated.Api.Dtos;

public record AccountExportDto(
    DateTimeOffset ExportedAt,
    string Email,
    double? WeightKg,
    double? HeightCm,
    int? CalorieTargetKcal,
    string? ActiveLensId,
    ConsentExportDto[] Consents,
    FoodDetailDto[] Foods,
    RecipeDetailDto[] Recipes,
    MealExportDto[] Meals);
