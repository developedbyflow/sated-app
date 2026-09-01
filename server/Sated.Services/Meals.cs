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
    UnknownEntry,
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
        Meal meal, int foodId, double? grams, double? servingCount, string? servingDescription,
        bool quantityEstimated = false)
    {
        var food = await WithServings(foodId);

        if (food is null)
        {
            return MealRejection.UnknownFood;
        }

        var quantity = Resolve(food, grams, servingCount, servingDescription);

        if (quantity.Rejection is not MealRejection.None)
        {
            return quantity.Rejection;
        }

        meal.Entries.Add(new MealEntry
        {
            FoodId = foodId,
            QuantityGrams = quantity.Grams,
            DisplayAmount = quantity.Amount,
            DisplayUnit = quantity.Unit,
            QuantityEstimated = quantityEstimated
        });

        await database.SaveChangesAsync();

        return MealRejection.None;
    }

    public async Task<MealRejection> Rewrite(
        Meal meal, int entryId, double? grams, double? servingCount, string? servingDescription)
    {
        var entry = meal.Entries.FirstOrDefault(logged => logged.Id == entryId);

        if (entry is null)
        {
            return MealRejection.UnknownEntry;
        }

        var quantity = Resolve(
            (await WithServings(entry.FoodId))!, grams, servingCount, servingDescription);

        if (quantity.Rejection is not MealRejection.None)
        {
            return quantity.Rejection;
        }

        entry.QuantityGrams = quantity.Grams;
        entry.DisplayAmount = quantity.Amount;
        entry.DisplayUnit = quantity.Unit;
        entry.QuantityEstimated = false;

        await database.SaveChangesAsync();

        return MealRejection.None;
    }

    public async Task<bool> RemoveEntry(Meal meal, int entryId)
    {
        var entry = meal.Entries.FirstOrDefault(logged => logged.Id == entryId);

        if (entry is null)
        {
            return false;
        }

        meal.Entries.Remove(entry);
        await database.SaveChangesAsync();

        return true;
    }

    public async Task<int> RemoveLoggedRecipe(Meal meal, int fromRecipeId)
    {
        var logged = meal.Entries.Where(entry => entry.FromRecipeId == fromRecipeId).ToArray();

        foreach (var entry in logged)
        {
            meal.Entries.Remove(entry);
        }

        await database.SaveChangesAsync();

        return logged.Length;
    }

    public async Task Remove(Meal meal)
    {
        database.Set<Meal>().Remove(meal);

        await database.SaveChangesAsync();
    }

    public async Task Rename(Meal meal, string name)
    {
        meal.Name = name;

        await database.SaveChangesAsync();
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

    private Task<Food?> WithServings(int foodId) =>
        database.Foods
            .Include(stored => stored.Servings)
            .FirstOrDefaultAsync(stored => stored.Id == foodId);

    private static Quantity Resolve(
        Food food, double? grams, double? servingCount, string? servingDescription)
    {
        var byGrams = grams is not null;
        var byServing = servingCount is not null && servingDescription is not null;

        if (byGrams == byServing)
        {
            return new Quantity(MealRejection.QuantityNotClear, 0, 0, "");
        }

        if (byGrams)
        {
            return new Quantity(MealRejection.None, grams!.Value, grams.Value, "g");
        }

        var serving = food.Servings.FirstOrDefault(offered => string.Equals(
            offered.Description, servingDescription, StringComparison.OrdinalIgnoreCase));

        return serving is null
            ? new Quantity(MealRejection.UnknownServing, 0, 0, "")
            : new Quantity(
                MealRejection.None,
                servingCount!.Value * serving.Grams,
                servingCount.Value,
                serving.Description);
    }

    private record Quantity(MealRejection Rejection, double Grams, double Amount, string Unit);

    private IQueryable<Meal> Full() =>
        database.Set<Meal>()
            .Include(meal => meal.Day)
            .Include(meal => meal.Entries)
            .ThenInclude(entry => entry.Food);
}
