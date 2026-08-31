using Microsoft.EntityFrameworkCore;
using Sated.Data;
using Sated.Data.Entities;
using Sated.Scoring;

namespace Sated.Services;

public enum MealRejection
{
    None,
    UnknownFood,
    UnknownServing,
    UnknownRecipe,
    RecipeNeedsGrams,
    QuantityNotClear
}

public class Meals(
    SatedDbContext database,
    Calibration calibration,
    FoodGrading grading,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public Task<Meal?> Find(int id) =>
        Full().FirstOrDefaultAsync(meal => meal.Id == id);

    public Task<Day?> On(DateOnly date) =>
        database.Days
            .Include(day => day.Meals)
            .ThenInclude(meal => meal.Entries)
            .ThenInclude(entry => entry.Food)
            .FirstOrDefaultAsync(day => day.Date == date);

    public Task<Meal[]> AllMine() =>
        Full().OrderBy(meal => meal.Day.Date).ThenBy(meal => meal.LoggedAt).ToArrayAsync();

    public async Task<Meal> Add(DateOnly date, string name)
    {
        var day = await database.Days.FirstOrDefaultAsync(stored => stored.Date == date);

        if (day is null)
        {
            day = new Day { OwnerId = currentUser.Id!, Date = date };
            database.Days.Add(day);
        }

        var meal = new Meal
        {
            Name = name,
            LoggedAt = clock.GetUtcNow(),
            EngineVersion = calibration.Version
        };

        day.Meals.Add(meal);
        await database.SaveChangesAsync();

        return meal;
    }

    public async Task<MealRejection> AddEntry(
        Meal meal, int foodId, double? grams, double? servingCount, string? servingDescription)
    {
        var food = await database.Foods
            .Include(stored => stored.Servings)
            .FirstOrDefaultAsync(stored => stored.Id == foodId);

        if (food is null)
        {
            return MealRejection.UnknownFood;
        }

        var byGrams = grams is not null;
        var byServing = servingCount is not null && servingDescription is not null;

        if (byGrams == byServing)
        {
            return MealRejection.QuantityNotClear;
        }

        var entry = byGrams
            ? new MealEntry
            {
                FoodId = foodId,
                QuantityGrams = grams!.Value,
                DisplayAmount = grams.Value,
                DisplayUnit = "g"
            }
            : Portioned(food, servingCount!.Value, servingDescription!);

        if (entry is null)
        {
            return MealRejection.UnknownServing;
        }

        meal.Entries.Add(entry);
        await database.SaveChangesAsync();

        return MealRejection.None;
    }

    public async Task<GradedFood?> GradeOf(Meal meal)
    {
        if (meal.Entries.Count == 0)
        {
            return null;
        }

        var user = await database.Users.FirstAsync(stored => stored.Id == currentUser.Id);
        var lens = grading.LensFor(user.ActiveLensId);

        return lens is null ? null : grading.Grade(Profile(meal), meal.Id, meal.Name, lens);
    }

    public async Task<MealRejection> AddRecipe(Meal meal, int recipeId, double? grams)
    {
        if (grams is null)
        {
            return MealRejection.RecipeNeedsGrams;
        }

        var recipe = await database.Recipes
            .Include(stored => stored.Ingredients)
            .FirstOrDefaultAsync(stored => stored.Id == recipeId);

        if (recipe is null || recipe.Ingredients.Count == 0)
        {
            return MealRejection.UnknownRecipe;
        }

        var share = grams.Value / recipe.Ingredients.Sum(ingredient => ingredient.Grams);

        foreach (var ingredient in recipe.Ingredients)
        {
            meal.Entries.Add(new MealEntry
            {
                FoodId = ingredient.FoodId,
                QuantityGrams = ingredient.Grams * share,
                DisplayAmount = ingredient.Grams * share,
                DisplayUnit = "g",
                FromRecipeId = recipe.Id,
                FromRecipeName = recipe.Name
            });
        }

        await database.SaveChangesAsync();

        return MealRejection.None;
    }

    public static FoodInput Profile(Meal meal) =>
        PortionAggregate.Aggregate(
            [.. meal.Entries.Select(entry =>
                new Portion(ScoringInput.From(entry.Food), entry.QuantityGrams))]);

    private static MealEntry? Portioned(Food food, double count, string description)
    {
        var serving = food.Servings.FirstOrDefault(offered =>
            string.Equals(offered.Description, description, StringComparison.OrdinalIgnoreCase));

        return serving is null
            ? null
            : new MealEntry
            {
                FoodId = food.Id,
                QuantityGrams = count * serving.Grams,
                DisplayAmount = count,
                DisplayUnit = serving.Description
            };
    }

    private IQueryable<Meal> Full() =>
        database.Set<Meal>()
            .Include(meal => meal.Day)
            .Include(meal => meal.Entries)
            .ThenInclude(entry => entry.Food);
}
