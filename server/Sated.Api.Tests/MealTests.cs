using System.Net;
using System.Net.Http.Json;
using Sated.Api.Dtos;

namespace Sated.Api.Tests;

[Collection("Database")]
public class MealTests(AccountsDatabase database) : IClassFixture<AccountsDatabase>
{
    private const string Password = "abcdefghijkl";

    private static readonly DateOnly Today = new(2026, 8, 31);

    [Fact]
    public async Task Post_WithoutASession_Answers401()
    {
        using var browser = database.NewBrowser();

        var response = await browser.PostAsJsonAsync(
            "/api/meals", new { date = Today, name = "Breakfast" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_ANewMeal_Creates201AndStampsTheEngineVersion()
    {
        using var browser = await SignedIn();

        var response = await NewMeal(browser, "Breakfast");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var meal = await Body(response);
        Assert.Equal(Today, meal.Date);
        Assert.False(string.IsNullOrWhiteSpace(meal.EngineVersion));
        Assert.Null(meal.Grade);
    }

    [Fact]
    public async Task AddEntry_InGrams_RecordsBothTheWeightAndWhatWasTyped()
    {
        using var browser = await SignedIn();
        var food = await database.AddFood("Cheese, logged", ownerId: null);
        var meal = await Body(await NewMeal(browser, "Lunch"));

        var response = await AddEntry(browser, meal.Id, new { foodId = food, grams = 150.0 });

        var stored = await Body(response);
        var entry = Assert.Single(stored.Entries);
        Assert.Equal(150, entry.QuantityGrams);
        Assert.Equal(150, entry.DisplayAmount);
        Assert.Equal("g", entry.DisplayUnit);
    }

    [Fact]
    public async Task AddEntry_InServings_FreezesTheGramsAndKeepsWhatWasChosen()
    {
        using var browser = await SignedIn();
        var food = await database.AddFoodWithServing("Egg, logged", "1 egg", grams: 50);
        var meal = await Body(await NewMeal(browser, "Breakfast"));

        var response = await AddEntry(
            browser, meal.Id, new { foodId = food, servingCount = 2.0, servingDescription = "1 egg" });

        var entry = Assert.Single((await Body(response)).Entries);
        Assert.Equal(100, entry.QuantityGrams);
        Assert.Equal(2, entry.DisplayAmount);
        Assert.Equal("1 egg", entry.DisplayUnit);
    }

    [Fact]
    public async Task AddEntry_AServingTheFoodDoesNotHave_IsRejected()
    {
        using var browser = await SignedIn();
        var food = await database.AddFoodWithServing("Egg, no slice", "1 egg", grams: 50);
        var meal = await Body(await NewMeal(browser, "Breakfast"));

        var response = await AddEntry(
            browser, meal.Id, new { foodId = food, servingCount = 1.0, servingDescription = "1 bucket" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddEntry_BothGramsAndAServing_IsRejected()
    {
        using var browser = await SignedIn();
        var food = await database.AddFoodWithServing("Egg, both", "1 egg", grams: 50);
        var meal = await Body(await NewMeal(browser, "Breakfast"));

        var response = await AddEntry(
            browser,
            meal.Id,
            new { foodId = food, grams = 60.0, servingCount = 1.0, servingDescription = "1 egg" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddEntry_NeitherGramsNorAServing_IsRejected()
    {
        using var browser = await SignedIn();
        var food = await database.AddFood("Cheese, unmeasured", ownerId: null);
        var meal = await Body(await NewMeal(browser, "Lunch"));

        var response = await AddEntry(browser, meal.Id, new { foodId = food });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddEntry_AFoodBelongingToSomebodyElse_IsRejected()
    {
        using var stranger = await SignedIn();
        var theirs = await database.AddFood("Their cheese", await IdOf(stranger));

        using var browser = await SignedIn();
        var meal = await Body(await NewMeal(browser, "Lunch"));

        var response = await AddEntry(browser, meal.Id, new { foodId = theirs, grams = 100.0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddEntry_WithAnActiveLens_ReturnsTheGradeWithoutAskingAgain()
    {
        using var browser = await SignedIn();
        await Onboard(browser);
        var food = await database.AddFood("Cheese, graded", ownerId: null);
        var meal = await Body(await NewMeal(browser, "Lunch"));

        var response = await AddEntry(browser, meal.Id, new { foodId = food, grams = 100.0 });

        Assert.NotNull((await Body(response)).Grade!.Grade);
    }

    [Fact]
    public async Task AddEntry_WithoutAnActiveLens_LogsAnywayAndLeavesTheGradeOut()
    {
        using var browser = await SignedIn();
        var food = await database.AddFood("Cheese, ungraded", ownerId: null);
        var meal = await Body(await NewMeal(browser, "Lunch"));

        var response = await AddEntry(browser, meal.Id, new { foodId = food, grams = 100.0 });

        var stored = await Body(response);
        Assert.Single(stored.Entries);
        Assert.Null(stored.Grade);
    }

    [Fact]
    public async Task AMealSomebodyElseLogged_IsNotFound()
    {
        using var owner = await SignedIn();
        var meal = await Body(await NewMeal(owner, "Theirs"));

        using var stranger = await SignedIn();

        Assert.Equal(
            HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/meals/{meal.Id}")).StatusCode);
    }

    [Fact]
    public async Task Day_WithTwoMeals_HoldsBothAndNobodyElses()
    {
        using var browser = await SignedIn();
        await NewMeal(browser, "Breakfast");
        await NewMeal(browser, "Lunch");

        using var stranger = await SignedIn();
        await NewMeal(stranger, "Their lunch");

        var mine = await browser.GetFromJsonAsync<DayDto>($"/api/days/{Today:yyyy-MM-dd}", ApiJson.Options);
        Assert.Equal(["Breakfast", "Lunch"], mine!.Meals.Select(meal => meal.Name).Order());
    }

    [Fact]
    public async Task Day_WithNothingLogged_IsEmptyRatherThanMissing()
    {
        using var browser = await SignedIn();

        var day = await browser.GetFromJsonAsync<DayDto>("/api/days/2020-01-01", ApiJson.Options);

        Assert.Empty(day!.Meals);
    }

    [Fact]
    public async Task Export_AfterLogging_CarriesTheMealWithItsFrozenGrams()
    {
        using var browser = await SignedIn();
        var food = await database.AddFoodWithServing("Egg, exported", "1 egg", grams: 50);
        var meal = await Body(await NewMeal(browser, "Breakfast"));
        await AddEntry(
            browser, meal.Id, new { foodId = food, servingCount = 2.0, servingDescription = "1 egg" });

        var response = await browser.PostAsJsonAsync(
            "/api/account/export", new { password = Password });

        var export = await response.Content.ReadFromJsonAsync<AccountExportDto>(ApiJson.Options);
        var logged = Assert.Single(export!.Meals);
        Assert.Equal(100, Assert.Single(logged.Entries).QuantityGrams);
        Assert.Equal("1 egg", logged.Entries[0].DisplayUnit);
    }

    [Fact]
    public async Task DeletingTheAccount_TakesTheDaysAndTheMealsWithIt()
    {
        using var browser = await SignedIn();
        var food = await database.AddFood("Cheese, doomed", ownerId: null);
        var meal = await Body(await NewMeal(browser, "Lunch"));
        await AddEntry(browser, meal.Id, new { foodId = food, grams = 100.0 });

        var response = await browser.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, "/api/account")
            {
                Content = JsonContent.Create(new { password = Password })
            });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await database.MealsWithId(meal.Id));
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

    private static async Task Onboard(HttpClient browser)
    {
        var offered = await browser.GetFromJsonAsync<ConsentResponseDto>(
            "/api/consents/HealthData", ApiJson.Options);

        await browser.PostAsJsonAsync("/api/consents/HealthData", new { version = offered!.Version });
        await browser.PutAsJsonAsync("/api/profile", new { weightKg = 82, activeLensId = "weight-loss" });
    }

    private static async Task<string> IdOf(HttpClient browser) =>
        (await browser.GetFromJsonAsync<CurrentUserDto>("/api/auth/me"))!.Id;

    private static Task<HttpResponseMessage> NewMeal(HttpClient browser, string name) =>
        browser.PostAsJsonAsync("/api/meals", new { date = Today, name });

    private static Task<HttpResponseMessage> AddEntry(HttpClient browser, int mealId, object body) =>
        browser.PostAsJsonAsync($"/api/meals/{mealId}/entries", body);

    private static async Task<MealDetailDto> Body(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<MealDetailDto>(ApiJson.Options))!;
}
