using Microsoft.EntityFrameworkCore;
using Sated.Data;
using Sated.Data.Entities;
using Sated.Scoring;

namespace Sated.Services;

public enum FoodRejection
{
    None,
    UnknownCategory,
    EnergyTooHighForAnyFood,
    EnergyDisagreesWithTheMacronutrients
}

public class FoodCatalogue(SatedDbContext database)
{
    public Task<string[]> Categories() =>
        database.Foods
            .Where(food => food.OwnerId == null)
            .Select(food => food.Category)
            .Distinct()
            .OrderBy(category => category)
            .ToArrayAsync();

    public async Task<FoodRejection> Add(Food food, double carbohydrate)
    {
        var catalogued = await database.Foods.AnyAsync(known =>
            known.OwnerId == null && known.Category == food.Category);

        if (!catalogued)
        {
            return FoodRejection.UnknownCategory;
        }

        var check = NutrientPlausibility.Check(
            food.Nutrients.Calories, food.Nutrients.Protein, food.Nutrients.Fat, carbohydrate);

        if (check is NutrientCheck.EnergyTooHighForAnyFood)
        {
            return FoodRejection.EnergyTooHighForAnyFood;
        }

        if (check is NutrientCheck.EnergyDisagreesWithTheMacronutrients)
        {
            return FoodRejection.EnergyDisagreesWithTheMacronutrients;
        }

        database.Foods.Add(food);
        await database.SaveChangesAsync();

        return FoodRejection.None;
    }
}
