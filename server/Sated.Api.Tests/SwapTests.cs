using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Sated.Api.Dtos;
using Sated.Scoring;

namespace Sated.Api.Tests;

[Collection("Database")]
public class SwapTests(SwapsDatabase database) : IClassFixture<SwapsDatabase>
{
    [Fact]
    public async Task Swap_ACannedPeach_ReturnsTheThreeBestOfItsCategoryByScore()
    {
        var swap = await Swapped(await database.IdOf("Peach, canned, in syrup"));

        Assert.Equal(
            ["Nectarine, raw", "Peach, canned, juice pack", "Peach, raw"],
            swap.Alternatives.Select(food => food.Description));
        Assert.All(swap.Alternatives, food => Assert.Equal(Grade.A, food.Grade));
        Assert.Null(swap.Message);
    }

    [Fact]
    public async Task Swap_TwoFoodsScoringTheSame_AlwaysPicksTheOlderRow()
    {
        var id = await database.IdOf("Peach, canned, in syrup");

        var swap = await Swapped(id);

        var raw = swap.Alternatives.Single(food => food.Description == "Peach, raw");
        Assert.Equal(await database.IdOf("Peach, raw"), raw.Id);
        Assert.DoesNotContain(swap.Alternatives, food => food.Description == "Peach, frozen");
    }

    [Fact]
    public async Task Swap_AskedTwice_AnswersWithTheSameThreeInTheSameOrder()
    {
        var id = await database.IdOf("Peach, canned, in syrup");

        var swap = await Swapped(id);

        Assert.Equal(
            (await Swapped(id)).Alternatives.Select(food => food.Id),
            swap.Alternatives.Select(food => food.Id));
    }

    [Fact]
    public async Task Swap_TheBestFoodInItsCategory_ReturnsNothingAndSaysWhy()
    {
        var swap = await Swapped(await database.IdOf("Nectarine, raw"));

        Assert.Empty(swap.Alternatives);
        Assert.Equal("No higher-graded foods in this category.", swap.Message);
    }

    [Fact]
    public async Task Swap_AFoodWithAPartialGrade_IsNeverSuggested()
    {
        var swap = await Swapped(await database.IdOf("Peach, canned, in syrup"));

        Assert.DoesNotContain(swap.Alternatives, food => food.Description == SwapsDatabase.PartialRow);
    }

    [Fact]
    public async Task Swap_AFoodWithNoLetterAtAll_HasNothingToBeBetterThan()
    {
        var swap = await Swapped(await database.IdOf(SwapsDatabase.NoLetter));

        Assert.Empty(swap.Alternatives);
        Assert.Equal("No higher-graded foods in this category.", swap.Message);
    }

    [Fact]
    public async Task Swap_YourOwnFood_IsNotSuggestedEvenToYou()
    {
        using var browser = await SignedIn();
        var mine = await Created(browser);
        var id = await database.IdOf("Peach, canned, in syrup");

        var swap = (await browser.GetFromJsonAsync<SwapResponseDto>(
            $"/api/foods/{id}/swap?lensId=weight-loss", ApiJson.Options))!;

        Assert.DoesNotContain(swap.Alternatives, food => food.Id == mine);
        Assert.Equal(
            (await Swapped(id)).Alternatives.Select(food => food.Id),
            swap.Alternatives.Select(food => food.Id));
    }

    [Fact]
    public async Task Swap_AFoodThatIsNotThere_Returns404()
    {
        var response = await database.Client.GetAsync("/api/foods/999999/swap?lensId=weight-loss");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Swap_UnknownLens_RejectsTheLensIdField()
    {
        var response = await database.Client.GetAsync(
            $"/api/foods/{await database.IdOf("Peach, raw")}/swap?lensId=keto");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("lensId", problem!.Errors.Keys);
    }

    private async Task<SwapResponseDto> Swapped(int id) =>
        (await database.Client.GetFromJsonAsync<SwapResponseDto>(
            $"/api/foods/{id}/swap?lensId=weight-loss", ApiJson.Options))!;

    private async Task<HttpClient> SignedIn()
    {
        var browser = database.NewBrowser();

        await browser.PostAsJsonAsync("/api/auth/register", new
        {
            email = SwapsDatabase.UnusedEmail(),
            password = "abcdefghijkl"
        });

        return browser;
    }

    private static async Task<int> Created(HttpClient browser)
    {
        var response = await browser.PostAsJsonAsync("/api/foods", new CreateFoodRequestDto
        {
            Description = "Peach from my garden",
            Category = SwapsDatabase.Category,
            Calories = 40,
            Protein = 1.2,
            Fat = 0.2,
            Carbohydrate = 8,
            Fiber = 2.5,
            SaturatedFat = 0,
            Sodium = 1,
            VitaminD = 5,
            Calcium = 20,
            Iron = 1,
            Potassium = 300
        });

        var food = await response.Content.ReadFromJsonAsync<FoodDetailDto>(ApiJson.Options);

        return food!.Id;
    }
}
