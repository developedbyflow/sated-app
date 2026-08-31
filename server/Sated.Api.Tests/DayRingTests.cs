using System.Net.Http.Json;
using Sated.Api.Dtos;

namespace Sated.Api.Tests;

[Collection("Database")]
public class DayRingTests(AccountsDatabase database) : IClassFixture<AccountsDatabase>
{
    private const string Password = "abcdefghijkl";

    private static readonly DateOnly Today = new(2026, 8, 31);

    private static readonly DateOnly Yesterday = new(2026, 8, 30);

    [Fact]
    public async Task Get_TheSameFoodSplitAcrossMeals_GradesTheDayIdentically()
    {
        using var browser = await Onboarded();
        var lean = await database.AddFood("Cod, plated", null, calories: 90, protein: 20, fat: 1);
        var fatty = await database.AddFood("Butter, plated", null, calories: 717, protein: 1, fat: 81);

        await Log(browser, Today, "All of it", (lean, 100), (fatty, 200));
        await Log(browser, Yesterday, "First", (lean, 100));
        await Log(browser, Yesterday, "Second", (fatty, 200));

        var oneMeal = await Day(browser, Today);
        var twoMeals = await Day(browser, Yesterday);

        var theMealItself = oneMeal.Meals.Single().Grade!;
        Assert.Equal(theMealItself.Score, oneMeal.Grade!.Score, tolerance: 0.000001);
        Assert.Equal(theMealItself.Score, twoMeals.Grade!.Score, tolerance: 0.000001);
        Assert.Equal(theMealItself.Grade, twoMeals.Grade.Grade);
    }

    [Fact]
    public async Task Get_ADayWithButterOnTop_GradesLowerThanTheSameDayWithout()
    {
        using var browser = await Onboarded();
        var lean = await database.AddFood("Cod, alone", null, calories: 90, protein: 20, fat: 1);
        var fatty = await database.AddFood("Butter, on top", null, calories: 717, protein: 1, fat: 81);

        await Log(browser, Yesterday, "Lunch", (lean, 100));
        await Log(browser, Today, "Lunch", (lean, 100), (fatty, 200));

        var withoutButter = await Day(browser, Yesterday);
        var withButter = await Day(browser, Today);

        Assert.True(withButter.Grade!.Score < withoutButter.Grade!.Score);
    }

    [Fact]
    public async Task Get_ADayWithMeals_CarriesTheGradeWithItsComponents()
    {
        using var browser = await Onboarded();
        var food = await database.AddFood("Cheese, graded", ownerId: null);

        await Log(browser, Today, "Lunch", (food, 200));

        var grade = (await Day(browser, Today)).Grade;

        Assert.NotNull(grade);
        Assert.NotNull(grade.Grade);
        Assert.True(grade.Satiety.Score > 0);
        Assert.NotNull(grade.Density);
    }

    [Fact]
    public async Task Get_ADayWithNothingLogged_HasNoGradeRatherThanAnE()
    {
        using var browser = await Onboarded();

        var day = await Day(browser, Today);

        Assert.Null(day.Grade);
    }

    [Fact]
    public async Task Get_ADayWhoseMealsAreAllEmpty_StillHasNoGrade()
    {
        using var browser = await Onboarded();

        await browser.PostAsJsonAsync("/api/meals", new { date = Today, name = "Nothing yet" });

        var day = await Day(browser, Today);

        Assert.Single(day.Meals);
        Assert.Null(day.Grade);
    }

    [Fact]
    public async Task Get_WithoutAnActiveLens_HasNoGradeAndNoTarget()
    {
        using var browser = await SignedIn();
        var food = await database.AddFood("Cheese, lensless", ownerId: null);
        await Log(browser, Today, "Lunch", (food, 200));

        var day = await Day(browser, Today);

        Assert.Null(day.Grade);
        Assert.Null(day.Protein.TargetMinGrams);
        Assert.Equal(34, day.Protein.Grams, tolerance: 0.01);
    }

    [Fact]
    public async Task Get_ADayWithTwoMeals_AddsUpTheProteinOfEveryEntry()
    {
        using var browser = await Onboarded();
        var food = await database.AddFood("Cheese, ringed", ownerId: null);

        await Log(browser, Today, "Breakfast", (food, 150));
        await Log(browser, Today, "Lunch", (food, 200));

        var day = await Day(browser, Today);

        Assert.Equal(59.5, day.Protein.Grams, tolerance: 0.01);
    }

    [Fact]
    public async Task Get_AfterOnboarding_DerivesTheTargetFromTheWeightAndTheHeight()
    {
        using var browser = await Onboarded();

        var day = await Day(browser, Today);

        Assert.Equal(118.3, day.Protein.TargetMinGrams!.Value, tolerance: 0.1);
        Assert.Equal(162.7, day.Protein.TargetMaxGrams!.Value, tolerance: 0.1);
    }

    [Fact]
    public async Task Get_TheSameWeightOnAShorterBody_LowersTheTarget()
    {
        using var browser = await Onboarded();
        var atOneEighty = (await Day(browser, Today)).Protein.TargetMinGrams;

        await browser.PutAsJsonAsync(
            "/api/profile", new { weightKg = 82, heightCm = 165, activeLensId = "weight-loss" });

        var atOneSixtyFive = (await Day(browser, Today)).Protein.TargetMinGrams;
        Assert.True(atOneSixtyFive < atOneEighty);
    }

    [Fact]
    public async Task Get_TheSameBodyUnderFitness_MovesTheTargetAndNotTheProtein()
    {
        using var browser = await Onboarded();
        var food = await database.AddFood("Cheese, lensed", ownerId: null);
        await Log(browser, Today, "Lunch", (food, 200));
        var underWeightLoss = await Day(browser, Today);

        await browser.PutAsJsonAsync(
            "/api/profile", new { weightKg = 82, heightCm = 180, activeLensId = "fitness" });

        var underFitness = await Day(browser, Today);
        Assert.Equal(underWeightLoss.Protein.Grams, underFitness.Protein.Grams);
        Assert.True(underFitness.Protein.TargetMinGrams < underWeightLoss.Protein.TargetMinGrams);
    }

    [Fact]
    public async Task Get_BeforeOnboarding_StillCountsTheProteinButOffersNoTarget()
    {
        using var browser = await SignedIn();
        var food = await database.AddFood("Cheese, targetless", ownerId: null);
        await Log(browser, Today, "Lunch", (food, 200));

        var day = await Day(browser, Today);

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
