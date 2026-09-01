using System.Net;
using System.Net.Http.Json;
using Sated.Api.Dtos;
using Sated.Data.Entities;

namespace Sated.Api.Tests;

[Collection("Database")]
public class OwnFoodTests(AccountsDatabase database) : IClassFixture<AccountsDatabase>
{
    private const string Password = "abcdefghijkl";

    [Fact]
    public async Task Post_WithoutASession_Answers401()
    {
        using var browser = database.NewBrowser();
        await Catalogued();

        var response = await Post(browser, Telemea());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_ALabelAndAKnownCategory_Creates201WithTheStoredFood()
    {
        using var browser = await SignedIn();
        await Catalogued();

        var response = await Post(browser, Telemea());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var food = await response.Content.ReadFromJsonAsync<FoodDetailDto>(ApiJson.Options);
        Assert.Equal("Telemea de oaie", food!.Description);
        Assert.Equal(250, food.Nutrients.Calories);
        Assert.Equal(450, food.Nutrients.Calcium);
        Assert.Null(food.Nutrients.Magnesium);
        Assert.Null(food.FdcId);
    }

    [Fact]
    public async Task Post_ACategoryTheCatalogueDoesNotUse_IsRejected()
    {
        using var browser = await SignedIn();
        await Catalogued();

        var response = await Post(browser, Telemea() with { Category = "Branzeturi" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_EnergyInKilojoules_IsRejected()
    {
        using var browser = await SignedIn();
        await Catalogued();

        var response = await Post(browser, Telemea() with { Calories = 1046 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_EnergyThatDoesNotFollowFromTheMacronutrients_IsRejected()
    {
        using var browser = await SignedIn();
        await Catalogued();

        var response = await Post(
            browser, Telemea() with { Protein = 0, Fat = 0, Carbohydrate = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_ALabelThatCannotCarryEveryNutrient_IsGradedAndSaysSo()
    {
        using var browser = await SignedIn();
        await Catalogued();
        var id = await Created(browser);

        var graded = await browser.GetFromJsonAsync<GradeResponseDto>(
            $"/api/foods/{id}/grade?lensId=weight-loss", ApiJson.Options);

        Assert.NotNull(graded!.Grade);
        Assert.True(graded.Density!.IsEstimated);
        Assert.True(graded.ProteinQuality!.IsEstimated);
    }

    [Fact]
    public async Task Post_Always_LeavesTheFoodInvisibleToEveryoneElse()
    {
        using var mine = await SignedIn();
        await Catalogued();
        await Created(mine);

        using var stranger = await SignedIn();
        var found = await stranger.GetFromJsonAsync<FoodListResponseDto>("/api/foods?search=telemea", ApiJson.Options);

        Assert.Equal(0, found!.Total);
    }

    [Fact]
    public async Task Categories_Always_ListsWhatTheCatalogueUses()
    {
        await Catalogued();
        using var browser = database.NewBrowser();

        var categories = await browser.GetFromJsonAsync<string[]>("/api/foods/categories");

        Assert.Contains("Cheese", categories!);
    }

    [Fact]
    public async Task Export_AfterAddingAFood_CarriesIt()
    {
        using var browser = await SignedIn();
        await Catalogued();
        await Created(browser);

        var response = await browser.PostAsJsonAsync(
            "/api/account/export", new { password = Password });

        var export = await response.Content.ReadFromJsonAsync<AccountExportDto>(ApiJson.Options);
        Assert.Equal("Telemea de oaie", Assert.Single(export!.Foods).Description);
    }

    [Fact]
    public async Task DeletingTheAccount_TakesMyFoodsWithIt()
    {
        using var browser = await SignedIn();
        await Catalogued();
        var id = await Created(browser);

        await browser.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/account")
        {
            Content = JsonContent.Create(new { password = Password })
        });

        Assert.Equal(0, await database.FoodsWithId(id));
    }

    [Fact]
    public async Task Post_Always_MarksTheFoodAsTypedInByAPerson()
    {
        using var browser = await SignedIn();
        await Catalogued();

        var response = await Post(browser, Telemea());

        var food = await response.Content.ReadFromJsonAsync<FoodDetailDto>(ApiJson.Options);
        Assert.Equal(FoodSource.UserEntered, food!.Provenance.Source);
    }

    [Fact]
    public async Task Post_ALabel_NamesTheNutrientsNoLabelCarries()
    {
        using var browser = await SignedIn();
        await Catalogued();

        var response = await Post(browser, Telemea());

        var food = await response.Content.ReadFromJsonAsync<FoodDetailDto>(ApiJson.Options);
        Assert.Contains("magnesium", food!.Provenance.Absent);
        Assert.Contains("thiamine", food.Provenance.Absent);
        Assert.DoesNotContain("calcium", food.Provenance.Absent);
    }

    [Fact]
    public async Task Post_AFoodOfMyOwn_CarriesNoSlugAndSoNoPublicPage()
    {
        using var browser = await SignedIn();
        await Catalogued();

        var response = await Post(browser, Telemea());

        var food = await response.Content.ReadFromJsonAsync<FoodDetailDto>(ApiJson.Options);
        Assert.Null(food!.Slug);
    }

    [Fact]
    public async Task Search_Always_SaysWhichRowsAreMineAndWhichAreTheCatalogue()
    {
        using var browser = await SignedIn();
        await Catalogued();
        await Created(browser);

        var found = await browser.GetFromJsonAsync<FoodListResponseDto>(
            "/api/foods?search=e", ApiJson.Options);

        Assert.Contains(found!.Items, row => row.Source is FoodSource.UserEntered);
        Assert.Contains(found.Items, row => row.Source is FoodSource.UsdaFndds);
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

    private Task<int> Catalogued() => database.AddFood("Cheddar, catalogued", ownerId: null);

    private static async Task<int> Created(HttpClient browser)
    {
        var response = await Post(browser, Telemea());
        var food = await response.Content.ReadFromJsonAsync<FoodDetailDto>(ApiJson.Options);

        return food!.Id;
    }

    private static Task<HttpResponseMessage> Post(HttpClient browser, CreateFoodRequestDto food) =>
        browser.PostAsJsonAsync("/api/foods", food);

    private static CreateFoodRequestDto Telemea() => new()
    {
        Description = "Telemea de oaie",
        Category = "Cheese",
        Calories = 250,
        Protein = 17,
        Fat = 20,
        Carbohydrate = 1,
        Fiber = 0,
        SaturatedFat = 12,
        Sodium = 900,
        Calcium = 450
    };
}
