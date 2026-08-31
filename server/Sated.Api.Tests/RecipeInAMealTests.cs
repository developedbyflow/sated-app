using System.Net;
using System.Net.Http.Json;
using Sated.Api.Dtos;

namespace Sated.Api.Tests;

[Collection("Database")]
public class RecipeInAMealTests(AccountsDatabase database) : IClassFixture<AccountsDatabase>
{
    private const string Password = "abcdefghijkl";

    private static readonly DateOnly Today = new(2026, 8, 31);

    [Fact]
    public async Task AddingARecipe_IsOneCallThatBecomesOneEntryPerIngredient()
    {
        using var browser = await SignedIn();
        var soup = await Composed(browser, ("Cheese, soup", 200), ("Water, soup", 400));
        var meal = await NewMeal(browser);

        var response = await AddRecipe(browser, meal.Id, soup, grams: 600);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var logged = await Body(response);
        Assert.Equal(2, logged.Entries.Length);
        Assert.Equal(600, logged.TotalGrams, tolerance: 0.001);
    }

    [Fact]
    public async Task AddingHalfARecipe_ScalesEveryIngredientByTheSameShare()
    {
        using var browser = await SignedIn();
        var soup = await Composed(browser, ("Cheese, half", 200), ("Water, half", 400));
        var meal = await NewMeal(browser);

        var logged = await Body(await AddRecipe(browser, meal.Id, soup, grams: 300));

        Assert.Equal(
            [100, 200],
            logged.Entries.Select(entry => entry.QuantityGrams).Order());
    }

    [Fact]
    public async Task AnEntryFromARecipe_RemembersWhichRecipeAndItsNameAtThatMoment()
    {
        using var browser = await SignedIn();
        var soup = await Composed(browser, ("Cheese, named", 200));
        var meal = await NewMeal(browser);

        var logged = await Body(await AddRecipe(browser, meal.Id, soup, grams: 200));

        var entry = Assert.Single(logged.Entries);
        Assert.Equal(soup, entry.FromRecipeId);
        Assert.Equal("Soup", entry.FromRecipeName);
    }

    [Fact]
    public async Task EditingTheRecipeAfterwards_LeavesTheLoggedMealExactlyAsItWas()
    {
        using var browser = await SignedIn();
        var cheese = await database.AddFood("Cheese, frozen", ownerId: null);
        var water = await database.AddFood("Water, frozen", ownerId: null, calories: 0, protein: 0, fat: 0);
        var soup = await Compose(browser, "Soup", (cheese, 200), (water, 400));
        var meal = await NewMeal(browser);
        var before = await Body(await AddRecipe(browser, meal.Id, soup, grams: 600));

        await browser.PutAsJsonAsync($"/api/recipes/{soup}", new
        {
            name = "Something else entirely",
            ingredients = new[] { new { foodId = water, grams = 1000.0 } }
        });

        var after = await Body(await browser.GetAsync($"/api/meals/{meal.Id}"));
        Assert.Equal(
            before.Entries.Select(entry => entry.QuantityGrams).Order(),
            after.Entries.Select(entry => entry.QuantityGrams).Order());
        Assert.Equal("Soup", after.Entries[0].FromRecipeName);
    }

    [Fact]
    public async Task DeletingTheRecipeAfterwards_LeavesTheLoggedMealStanding()
    {
        using var browser = await SignedIn();
        var soup = await Composed(browser, ("Cheese, deleted recipe", 200));
        var meal = await NewMeal(browser);
        await AddRecipe(browser, meal.Id, soup, grams: 200);

        await browser.DeleteAsync($"/api/recipes/{soup}");

        var after = await Body(await browser.GetAsync($"/api/meals/{meal.Id}"));
        var entry = Assert.Single(after.Entries);
        Assert.Equal(200, entry.QuantityGrams);
        Assert.Equal("Soup", entry.FromRecipeName);
    }

    [Fact]
    public async Task AddingARecipeWithoutGrams_IsRejected()
    {
        using var browser = await SignedIn();
        var soup = await Composed(browser, ("Cheese, no grams", 200));
        var meal = await NewMeal(browser);

        var response = await browser.PostAsJsonAsync(
            $"/api/meals/{meal.Id}/entries", new { recipeId = soup });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddingSomebodyElsesRecipe_IsRejected()
    {
        using var stranger = await SignedIn();
        var theirs = await Composed(stranger, ("Cheese, theirs", 200));

        using var browser = await SignedIn();
        var meal = await NewMeal(browser);

        var response = await AddRecipe(browser, meal.Id, theirs, grams: 200);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddingBothAFoodAndARecipe_IsRejected()
    {
        using var browser = await SignedIn();
        var food = await database.AddFood("Cheese, both", ownerId: null);
        var soup = await Composed(browser, ("Cheese, both two", 200));
        var meal = await NewMeal(browser);

        var response = await browser.PostAsJsonAsync(
            $"/api/meals/{meal.Id}/entries",
            new { foodId = food, recipeId = soup, grams = 100.0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpClient> SignedIn()
    {
        var browser = database.NewBrowser();

        await browser.PostAsJsonAsync("/api/auth/register", new
        {
            email = AccountsDatabase.UnusedEmail(),
            password = Password
        });

        return browser;
    }

    private async Task<int> Composed(HttpClient browser, params (string Food, double Grams)[] parts)
    {
        var ingredients = new List<(int FoodId, double Grams)>();

        foreach (var part in parts)
        {
            ingredients.Add((await database.AddFood(part.Food, ownerId: null), part.Grams));
        }

        return await Compose(browser, "Soup", [.. ingredients]);
    }

    private static async Task<int> Compose(
        HttpClient browser, string name, params (int FoodId, double Grams)[] ingredients)
    {
        var response = await browser.PostAsJsonAsync("/api/recipes", new
        {
            name,
            ingredients = ingredients
                .Select(ingredient => new { foodId = ingredient.FoodId, grams = ingredient.Grams })
                .ToArray()
        });

        return (await response.Content.ReadFromJsonAsync<RecipeDetailDto>(ApiJson.Options))!.Id;
    }

    private static async Task<MealDetailDto> NewMeal(HttpClient browser) =>
        (await (await browser.PostAsJsonAsync("/api/meals", new { date = Today, name = "Dinner" }))
            .Content.ReadFromJsonAsync<MealDetailDto>(ApiJson.Options))!;

    private static Task<HttpResponseMessage> AddRecipe(
        HttpClient browser, int mealId, int recipeId, double grams) =>
        browser.PostAsJsonAsync($"/api/meals/{mealId}/entries", new { recipeId, grams });

    private static async Task<MealDetailDto> Body(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<MealDetailDto>(ApiJson.Options))!;
}
