using System.Net.Http.Json;
using Sated.Api.Dtos;

namespace Sated.Api.Tests;

[Collection("Database")]
public class DayRingTests(AccountsDatabase database) : IClassFixture<AccountsDatabase>
{
    private const string Password = "abcdefghijkl";

    private static readonly DateOnly Today = new(2026, 8, 31);

    [Fact]
    public async Task Get_ADayWithTwoMeals_AddsUpTheProteinOfEveryEntry()
    {
        using var browser = await Onboarded();
        var food = await database.AddFood("Cheese, ringed", ownerId: null);

        await Log(browser, "Breakfast", food, grams: 150);
        await Log(browser, "Lunch", food, grams: 200);

        var day = await Day(browser);

        Assert.Equal(59.5, day.Protein.Grams, tolerance: 0.01);
    }

    [Fact]
    public async Task Get_AfterOnboarding_DerivesTheTargetFromTheWeightAndTheHeight()
    {
        using var browser = await Onboarded();

        var day = await Day(browser);

        Assert.Equal(118.3, day.Protein.TargetMinGrams!.Value, tolerance: 0.1);
        Assert.Equal(162.7, day.Protein.TargetMaxGrams!.Value, tolerance: 0.1);
    }

    [Fact]
    public async Task Get_TheSameWeightOnAShorterBody_LowersTheTarget()
    {
        using var browser = await Onboarded();
        var atOneEighty = (await Day(browser)).Protein.TargetMinGrams;

        await browser.PutAsJsonAsync(
            "/api/profile", new { weightKg = 82, heightCm = 165, activeLensId = "weight-loss" });

        var atOneSixtyFive = (await Day(browser)).Protein.TargetMinGrams;
        Assert.True(atOneSixtyFive < atOneEighty);
    }

    [Fact]
    public async Task Get_TheSameBodyUnderFitness_MovesTheTargetAndNotTheProtein()
    {
        using var browser = await Onboarded();
        var food = await database.AddFood("Cheese, lensed", ownerId: null);
        await Log(browser, "Lunch", food, grams: 200);
        var underWeightLoss = await Day(browser);

        await browser.PutAsJsonAsync(
            "/api/profile", new { weightKg = 82, heightCm = 180, activeLensId = "fitness" });

        var underFitness = await Day(browser);
        Assert.Equal(underWeightLoss.Protein.Grams, underFitness.Protein.Grams);
        Assert.True(underFitness.Protein.TargetMinGrams < underWeightLoss.Protein.TargetMinGrams);
    }

    [Fact]
    public async Task Get_BeforeOnboarding_StillCountsTheProteinButOffersNoTarget()
    {
        using var browser = await SignedIn();
        var food = await database.AddFood("Cheese, targetless", ownerId: null);
        await Log(browser, "Lunch", food, grams: 200);

        var day = await Day(browser);

        Assert.Equal(34, day.Protein.Grams, tolerance: 0.01);
        Assert.Null(day.Protein.TargetMinGrams);
        Assert.Null(day.Protein.TargetMaxGrams);
    }

    [Fact]
    public async Task Get_ADayWithNothingLogged_IsNoProteinAgainstARealTarget()
    {
        using var browser = await Onboarded();

        var day = await browser.GetFromJsonAsync<DayDto>("/api/days/2020-01-01", ApiJson.Options);

        Assert.Empty(day!.Meals);
        Assert.Equal(0, day.Protein.Grams);
        Assert.NotNull(day.Protein.TargetMinGrams);
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

    private async Task<HttpClient> Onboarded()
    {
        var browser = await SignedIn();

        var offered = await browser.GetFromJsonAsync<ConsentResponseDto>(
            "/api/consents/HealthData", ApiJson.Options);

        await browser.PostAsJsonAsync("/api/consents/HealthData", new { version = offered!.Version });
        await browser.PutAsJsonAsync(
            "/api/profile", new { weightKg = 82, heightCm = 180, activeLensId = "weight-loss" });

        return browser;
    }

    private static async Task Log(HttpClient browser, string name, int foodId, double grams)
    {
        var created = await browser.PostAsJsonAsync("/api/meals", new { date = Today, name });
        var meal = (await created.Content.ReadFromJsonAsync<MealDetailDto>(ApiJson.Options))!;

        await browser.PostAsJsonAsync($"/api/meals/{meal.Id}/entries", new { foodId, grams });
    }

    private static async Task<DayDto> Day(HttpClient browser) =>
        (await browser.GetFromJsonAsync<DayDto>($"/api/days/{Today:yyyy-MM-dd}", ApiJson.Options))!;
}
