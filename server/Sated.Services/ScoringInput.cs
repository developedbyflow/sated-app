using Sated.Data.Entities;
using Sated.Scoring;

namespace Sated.Services;

public static class ScoringInput
{
    public static FoodInput From(Food food) => new(
        Category: food.Category,
        Calories: food.Nutrients.Calories,
        Protein: food.Nutrients.Protein,
        Fat: food.Nutrients.Fat,
        Fiber: food.Nutrients.Fiber,
        VitaminA: food.Nutrients.VitaminA,
        VitaminC: food.Nutrients.VitaminC,
        VitaminE: food.Nutrients.VitaminE,
        Calcium: food.Nutrients.Calcium,
        Iron: food.Nutrients.Iron,
        Magnesium: food.Nutrients.Magnesium,
        Potassium: food.Nutrients.Potassium,
        SaturatedFat: food.Nutrients.SaturatedFat,
        Sodium: food.Nutrients.Sodium,
        VitaminD: food.Nutrients.VitaminD,
        Thiamine: food.Nutrients.Thiamine,
        LeucinePer100g: food.Nutrients.Leucine,
        LeucineIsEstimated: false);
}
