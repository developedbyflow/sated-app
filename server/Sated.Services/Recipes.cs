using Microsoft.EntityFrameworkCore;
using Sated.Data;
using Sated.Data.Entities;
using Sated.Scoring;

namespace Sated.Services;

public enum RecipeRejection
{
    None,
    NoIngredients,
    UnknownFood
}

public class Recipes(SatedDbContext database)
{
    public Task<Recipe[]> Mine() =>
        Full().OrderBy(recipe => recipe.Name).ToArrayAsync();

    public Task<Recipe?> Find(int id) =>
        Full().FirstOrDefaultAsync(recipe => recipe.Id == id);

    public async Task<RecipeRejection> Add(Recipe recipe)
    {
        var rejection = await Check(recipe);

        if (rejection is not RecipeRejection.None)
        {
            return rejection;
        }

        database.Recipes.Add(recipe);
        await database.SaveChangesAsync();

        return RecipeRejection.None;
    }

    public async Task<RecipeRejection> Replace(
        Recipe stored, string name, IReadOnlyList<RecipeIngredient> ingredients)
    {
        var replacement = new Recipe
        {
            Name = name,
            OwnerId = stored.OwnerId,
            Ingredients = [.. ingredients]
        };

        var rejection = await Check(replacement);

        if (rejection is not RecipeRejection.None)
        {
            return rejection;
        }

        stored.Name = name;
        stored.Ingredients.Clear();
        stored.Ingredients.AddRange(ingredients);

        await database.SaveChangesAsync();

        return RecipeRejection.None;
    }

    public async Task Remove(Recipe recipe)
    {
        database.Recipes.Remove(recipe);

        await database.SaveChangesAsync();
    }

    public static FoodInput Profile(Recipe recipe) =>
        PortionAggregate.Aggregate(
            [.. recipe.Ingredients.Select(ingredient =>
                new Portion(ScoringInput.From(ingredient.Food), ingredient.Grams))]);

    private async Task<RecipeRejection> Check(Recipe recipe)
    {
        if (recipe.Ingredients.Count == 0)
        {
            return RecipeRejection.NoIngredients;
        }

        var wanted = recipe.Ingredients.Select(ingredient => ingredient.FoodId).Distinct();

        var visible = await database.Foods
            .Where(food => wanted.Contains(food.Id))
            .CountAsync();

        return visible == wanted.Count() ? RecipeRejection.None : RecipeRejection.UnknownFood;
    }

    private IQueryable<Recipe> Full() =>
        database.Recipes
            .Include(recipe => recipe.Ingredients)
            .ThenInclude(ingredient => ingredient.Food);
}
