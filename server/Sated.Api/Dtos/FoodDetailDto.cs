namespace Sated.Api.Dtos;

public record FoodDetailDto(
    int Id,
    int? FdcId,
    string Description,
    string Category,
    NutrientAmountsDto Nutrients
);
