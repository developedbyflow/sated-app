using System.Net;
using System.Net.Http.Json;
using Sated.Api.Dtos;

namespace Sated.Api.Tests;

[Collection("Database")]
public class EditingAMealTests(AccountsDatabase database) : IClassFixture<AccountsDatabase>
{
    private const string Password = "abcdefghijkl";

    private static readonly DateOnly Today = new(2026, 8, 31);

    [Fact]
    public async Task Rewrite_ANewWeight_StoresIt()
    {
        using var browser = await SignedIn(onboard: true);
        var meal = await Logged(browser, grams: 100);

        var after = await Body(await Rewrite(browser, meal, new { grams = 400.0 }));

        Assert.Equal(400, Assert.Single(after.Entries).QuantityGrams);
    }

    [Fact]
    public async Task Rewrite_TheWeightOfOneOfTwoFoods_MovesTheGradeBecauseTheMixMoved()
    {
        using var browser = await SignedIn(onboard: true);
        var cheese = await database.AddFood("Cheese, mixed", ownerId: null);
        var water = await database.AddFood("Water, mixed", ownerId: null, calories: 0, protein: 0, fat: 0);
        var meal = await Logged(browser, grams: 100, foodId: cheese);
        var both = await Body(await browser.PostAsJsonAsync(
            $"/api/meals/{meal.Id}/entries", new { foodId = water, grams = 100.0 }));

        var response = await browser.PutAsJsonAsync(
            $"/api/meals/{meal.Id}/entries/{both.Entries[1].Id}", new { grams = 900.0 });

        var after = await Body(response);
        Assert.NotEqual(both.Grade!.Score, after.Grade!.Score);
    }

    [Fact]
    public async Task Rewrite_TheOnlyFoodInAMeal_LeavesTheGradeWhereItWas()
    {
        using var browser = await SignedIn(onboard: true);
        var meal = await Logged(browser, grams: 100);

        var after = await Body(await Rewrite(browser, meal, new { grams = 400.0 }));

        Assert.Equal(meal.Grade!.Score, after.Grade!.Score);
    }

    [Fact]
    public async Task Rewrite_IntoServings_ReplacesWhatWasTypedAsWellAsTheWeight()
    {
        using var browser = await SignedIn();
        var food = await database.AddFoodWithServing("Egg, rewritten", "1 egg", grams: 50);
        var meal = await Logged(browser, grams: 100, foodId: food);

        var after = await Body(await Rewrite(
            browser, meal, new { servingCount = 3.0, servingDescription = "1 egg" }));

        var entry = Assert.Single(after.Entries);
        Assert.Equal(150, entry.QuantityGrams);
        Assert.Equal(3, entry.DisplayAmount);
        Assert.Equal("1 egg", entry.DisplayUnit);
    }

    [Fact]
    public async Task Rewrite_WithNeitherGramsNorAServing_IsRejected()
    {
        using var browser = await SignedIn();
        var meal = await Logged(browser, grams: 100);

        var response = await Rewrite(browser, meal, new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rewrite_AnEntryThatIsNotInThisMeal_IsNotFound()
    {
        using var browser = await SignedIn();
        var meal = await Logged(browser, grams: 100);

        var response = await browser.PutAsJsonAsync(
            $"/api/meals/{meal.Id}/entries/999999", new { grams = 50.0 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveEntry_TheLastOne_LeavesAMealWithNoGradeRatherThanAnE()
    {
        using var browser = await SignedIn(onboard: true);
        var meal = await Logged(browser, grams: 100);

        var after = await Body(await browser.DeleteAsync(
            $"/api/meals/{meal.Id}/entries/{meal.Entries[0].Id}"));

        Assert.Empty(after.Entries);
        Assert.Null(after.Grade);
    }

    [Fact]
    public async Task RemoveEntry_OneOfTwo_RecalculatesTheGradeOnWhatIsLeft()
    {
        using var browser = await SignedIn(onboard: true);
        var meal = await Logged(browser, grams: 100);
        var second = await database.AddFood("Water, second", ownerId: null, calories: 0, protein: 0, fat: 0);
        var both = await Body(await browser.PostAsJsonAsync(
            $"/api/meals/{meal.Id}/entries", new { foodId = second, grams = 400.0 }));

        var after = await Body(await browser.DeleteAsync(
            $"/api/meals/{meal.Id}/entries/{both.Entries[1].Id}"));

        Assert.Single(after.Entries);
        Assert.NotEqual(both.Grade!.Score, after.Grade!.Score);
    }

    [Fact]
    public async Task RemoveLoggedRecipe_TakesEveryEntryItUnpackedAndNothingElse()
    {
        using var browser = await SignedIn();
        var cheese = await database.AddFood("Cheese, grouped", ownerId: null);
        var water = await database.AddFood("Water, grouped", ownerId: null, calories: 0, protein: 0, fat: 0);
        var soup = await Compose(browser, (cheese, 200), (water, 400));
        var meal = await NewMeal(browser);
        await browser.PostAsJsonAsync($"/api/meals/{meal.Id}/entries", new { foodId = cheese, grams = 50.0 });
        await browser.PostAsJsonAsync($"/api/meals/{meal.Id}/entries", new { recipeId = soup, grams = 600.0 });

        var after = await Body(await browser.DeleteAsync($"/api/meals/{meal.Id}/recipes/{soup}"));

        var left = Assert.Single(after.Entries);
        Assert.Equal(50, left.QuantityGrams);
        Assert.Null(left.FromRecipeName);
    }

    [Fact]
    public async Task Delete_AWholeMeal_LeavesTheRecipeItWasLoggedFromUntouched()
    {
        using var browser = await SignedIn();
        var cheese = await database.AddFood("Cheese, meal deleted", ownerId: null);
        var soup = await Compose(browser, (cheese, 200));
        var meal = await NewMeal(browser);
        await browser.PostAsJsonAsync($"/api/meals/{meal.Id}/entries", new { recipeId = soup, grams = 200.0 });

        await browser.DeleteAsync($"/api/meals/{meal.Id}");

        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/api/recipes/{soup}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/meals/{meal.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_AWholeMeal_TakesItsEntriesOutOfTheDay()
    {
        using var browser = await SignedIn();
        var meal = await Logged(browser, grams: 100);

        await browser.DeleteAsync($"/api/meals/{meal.Id}");

        var day = await browser.GetFromJsonAsync<DayDto>(
            $"/api/days/{Today:yyyy-MM-dd}", ApiJson.Options);
        Assert.Empty(day!.Meals);
    }

    [Fact]
    public async Task Rename_AMeal_KeepsItsEntries()
    {
        using var browser = await SignedIn();
        var meal = await Logged(browser, grams: 100);

        var after = await Body(await browser.PutAsJsonAsync(
            $"/api/meals/{meal.Id}", new { name = "Second breakfast" }));

        Assert.Equal("Second breakfast", after.Name);
        Assert.Single(after.Entries);
    }

    [Fact]
    public async Task EditingSomebodyElsesMeal_IsNotFound()
    {
        using var owner = await SignedIn();
        var meal = await Logged(owner, grams: 100);

        using var stranger = await SignedIn();
        var response = await stranger.PutAsJsonAsync(
            $"/api/meals/{meal.Id}", new { name = "Mine now" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<HttpClient> SignedIn(bool onboard = false)
    {
        var browser = database.NewBrowser();

        await browser.PostAsJsonAsync("/api/auth/register", new
        {
            email = AccountsDatabase.UnusedEmail(),
            password = Password
        });

        if (onboard)
        {
            var offered = await browser.GetFromJsonAsync<ConsentResponseDto>(
                "/api/consents/HealthData", ApiJson.Options);

            await browser.PostAsJsonAsync(
                "/api/consents/HealthData", new { version = offered!.Version });
            await browser.PutAsJsonAsync(
                "/api/profile", new { weightKg = 82, heightCm = 180, activeLensId = "weight-loss" });
        }

        return browser;
    }

    private async Task<MealDetailDto> Logged(HttpClient browser, double grams, int? foodId = null)
    {
        var food = foodId ?? await database.AddFood($"Cheese {Guid.NewGuid():N}", ownerId: null);
        var meal = await NewMeal(browser);

        return await Body(await browser.PostAsJsonAsync(
            $"/api/meals/{meal.Id}/entries", new { foodId = food, grams }));
    }

    private static async Task<MealDetailDto> NewMeal(HttpClient browser) =>
        (await (await browser.PostAsJsonAsync("/api/meals", new { date = Today, name = "Breakfast" }))
            .Content.ReadFromJsonAsync<MealDetailDto>(ApiJson.Options))!;

    private static async Task<int> Compose(
        HttpClient browser, params (int FoodId, double Grams)[] ingredients)
    {
        var response = await browser.PostAsJsonAsync("/api/recipes", new
        {
            name = "Soup",
            ingredients = ingredients
                .Select(ingredient => new { foodId = ingredient.FoodId, grams = ingredient.Grams })
                .ToArray()
        });

        return (await response.Content.ReadFromJsonAsync<RecipeDetailDto>(ApiJson.Options))!.Id;
    }

    private static Task<HttpResponseMessage> Rewrite(
        HttpClient browser, MealDetailDto meal, object body) =>
        browser.PutAsJsonAsync($"/api/meals/{meal.Id}/entries/{meal.Entries[0].Id}", body);

    private static async Task<MealDetailDto> Body(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<MealDetailDto>(ApiJson.Options))!;
}
