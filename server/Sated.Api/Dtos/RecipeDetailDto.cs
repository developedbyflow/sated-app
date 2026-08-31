using Sated.Data.Entities;
using Sated.Services;

namespace Sated.Api.Dtos;

public record RecipeIngredientDto(int FoodId, string Description, FoodSource Source, double Grams);

public record RecipeListItemDto(int Id, string Name, int Ingredients, double TotalGrams);

public record RecipeDetailDto(
    int Id,
    string Name,
    double TotalGrams,
    RecipeIngredientDto[] Ingredients,
    NutrientAmountsDto Nutrients,
    bool LeucineIsEstimated)
{
    public static RecipeListItemDto Listed(Recipe recipe) => new(
        recipe.Id,
        recipe.Name,
        recipe.Ingredients.Count,
        recipe.Ingredients.Sum(ingredient => ingredient.Grams));

    public static RecipeDetailDto From(Recipe recipe)
    {
        var profile = Services.Recipes.Profile(recipe);

        return new RecipeDetailDto(
            recipe.Id,
            recipe.Name,
            recipe.Ingredients.Sum(ingredient => ingredient.Grams),
            [.. recipe.Ingredients.Select(ingredient => new RecipeIngredientDto(
                ingredient.FoodId,
                ingredient.Food.Description,
                ingredient.Food.Source,
                ingredient.Grams))],
            new NutrientAmountsDto(
                profile.Calories,
                profile.Protein,
                profile.Fat,
                profile.Fiber,
                profile.SaturatedFat,
                profile.Sodium,
                profile.VitaminA,
                profile.VitaminC,
                profile.VitaminD,
                profile.VitaminE,
                profile.Thiamine,
                profile.Calcium,
                profile.Iron,
                profile.Magnesium,
                profile.Potassium,
                profile.LeucinePer100g),
            profile.LeucineIsEstimated);
    }
}
