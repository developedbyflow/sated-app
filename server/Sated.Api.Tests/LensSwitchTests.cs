using System.Net.Http.Json;
using Sated.Api.Dtos;
using Sated.Scoring;

namespace Sated.Api.Tests;

[Collection("Database")]
public class LensSwitchTests(AccountsDatabase database) : IClassFixture<AccountsDatabase>
{
    private const string Password = "abcdefghijkl";

    private static readonly DateOnly LongAgo = new(2026, 3, 14);

    [Fact]
    public async Task Get_AfterSwitchingTheLens_ADayLoggedLongAgoCarriesANewLetter()
    {
        using var browser = await Onboarded();
        var cheese = await database.AddFood("Cheese, regraded", ownerId: null);
        await Log(browser, LongAgo, "Lunch", (cheese, 200));
        var underWeightLoss = await Day(browser, LongAgo);

        await SwitchTo(browser, "fitness");

        Assert.Equal(Grade.C, underWeightLoss.Grade!.Grade);
        Assert.Equal(Grade.A, (await Day(browser, LongAgo)).Grade!.Grade);
    }

    [Fact]
    public async Task Get_AfterSwitchingTheLens_TheMealInThatDayIsRegradedToo()
    {
        using var browser = await Onboarded();
        var cheese = await database.AddFood("Cheese, regraded inside its meal", ownerId: null);
        await Log(browser, LongAgo, "Lunch", (cheese, 200));
        var underWeightLoss = await Day(browser, LongAgo);

        await SwitchTo(browser, "fitness");

        Assert.Equal(Grade.C, underWeightLoss.Meals.Single().Grade!.Grade);
        Assert.Equal(Grade.A, (await Day(browser, LongAgo)).Meals.Single().Grade!.Grade);
    }

    [Fact]
    public async Task Get_AfterSwitchingTheLens_EveryLoggedEntryIsStillTheSameRow()
    {
        using var browser = await Onboarded();
        var cheese = await database.AddFood("Cheese, kept through the switch", ownerId: null);
        var cod = await database.AddFood(
            "Cod, kept through the switch", ownerId: null, calories: 90, protein: 20, fat: 1);
        await Log(browser, LongAgo, "Lunch", (cheese, 200), (cod, 150));
        var before = await Day(browser, LongAgo);
        Assert.Equal(2, Logged(before).Count());

        await SwitchTo(browser, "fitness");

        Assert.Equal(Logged(before), Logged(await Day(browser, LongAgo)));
    }

    [Fact]
    public async Task Get_AfterSwitchingTheLens_TheProteinTargetMovesAndTheProteinEatenDoesNot()
    {
        using var browser = await Onboarded();
        var cheese = await database.AddFood("Cheese, counted twice", ownerId: null);
        await Log(browser, LongAgo, "Lunch", (cheese, 200));
        var underWeightLoss = await Day(browser, LongAgo);

        await SwitchTo(browser, "glp-1");

        var underGlp1 = await Day(browser, LongAgo);
        Assert.Equal(underWeightLoss.Protein.Grams, underGlp1.Protein.Grams);
        Assert.NotEqual(underWeightLoss.Protein.TargetMinGrams, underGlp1.Protein.TargetMinGrams);
    }

    [Fact]
    public async Task Get_SwitchingBackToTheFirstLens_ReturnsTheFirstLetter()
    {
        using var browser = await Onboarded();
        var cheese = await database.AddFood("Cheese, switched back", ownerId: null);
        await Log(browser, LongAgo, "Lunch", (cheese, 200));
        var atTheStart = await Day(browser, LongAgo);
        await SwitchTo(browser, "fitness");

        await SwitchTo(browser, "weight-loss");

        Assert.Equal(atTheStart.Grade!.Grade, (await Day(browser, LongAgo)).Grade!.Grade);
    }

    private static IEnumerable<(int Meal, int Entry, int Food, double Grams)> Logged(DayDto day) =>
        day.Meals.SelectMany(meal => meal.Entries.Select(entry =>
            (meal.Id, entry.Id, entry.FoodId, entry.QuantityGrams)));

    private static Task SwitchTo(HttpClient browser, string lensId) =>
        browser.PutAsJsonAsync(
            "/api/profile", new { weightKg = 82, heightCm = 180, activeLensId = lensId });

    private async Task<HttpClient> Onboarded()
    {
        var browser = database.NewBrowser();

        await browser.PostAsJsonAsync("/api/auth/register", new
        {
            email = AccountsDatabase.UnusedEmail(),
            password = Password
        });

        var offered = await browser.GetFromJsonAsync<ConsentResponseDto>(
            "/api/consents/HealthData", ApiJson.Options);

        await browser.PostAsJsonAsync("/api/consents/HealthData", new { version = offered!.Version });
        await SwitchTo(browser, "weight-loss");

        return browser;
    }

    private static async Task Log(
        HttpClient browser, DateOnly date, string name, params (int Food, double Grams)[] entries)
    {
        var created = await browser.PostAsJsonAsync("/api/meals", new { date, name });
        var meal = (await created.Content.ReadFromJsonAsync<MealDetailDto>(ApiJson.Options))!;

        foreach (var (foodId, grams) in entries)
        {
            await browser.PostAsJsonAsync($"/api/meals/{meal.Id}/entries", new { foodId, grams });
        }
    }

    private static async Task<DayDto> Day(HttpClient browser, DateOnly date) =>
        (await browser.GetFromJsonAsync<DayDto>($"/api/days/{date:yyyy-MM-dd}", ApiJson.Options))!;
}
