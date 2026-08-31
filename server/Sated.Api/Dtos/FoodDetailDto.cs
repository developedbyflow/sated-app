using Sated.Data.Entities;

namespace Sated.Api.Dtos;

public record FoodDetailDto(
    int Id,
    int? FdcId,
    string Description,
    string Category,
    NutrientAmountsDto Nutrients
)
{
    public static FoodDetailDto From(Food food) => new(
        food.Id,
        food.FdcId,
        food.Description,
        food.Category,
        new NutrientAmountsDto(
            food.Nutrients.Calories,
            food.Nutrients.Protein,
            food.Nutrients.Fat,
            food.Nutrients.Fiber,
            food.Nutrients.SaturatedFat,
            food.Nutrients.Sodium,
            food.Nutrients.VitaminA,
            food.Nutrients.VitaminC,
            food.Nutrients.VitaminD,
            food.Nutrients.VitaminE,
            food.Nutrients.Thiamine,
            food.Nutrients.Calcium,
            food.Nutrients.Iron,
            food.Nutrients.Magnesium,
            food.Nutrients.Potassium,
            food.Nutrients.Leucine));
}
