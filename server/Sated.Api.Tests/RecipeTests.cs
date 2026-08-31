using System.Net;
using System.Net.Http.Json;
using Sated.Api.Dtos;

namespace Sated.Api.Tests;

[Collection("Database")]
public class RecipeTests(AccountsDatabase database) : IClassFixture<AccountsDatabase>
{
    private const string Password = "abcdefghijkl";

    [Fact]
    public async Task Post_WithoutASession_Answers401()
    {
        using var browser = database.NewBrowser();
        var cheese = await database.AddFood("Cheese for nobody", ownerId: null);

        var response = await Post(browser, "Soup", (cheese, 100));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_TwoIngredients_Creates201WithTheDerivedProfile()
    {
        using var browser = await SignedIn();
        var cheese = await database.AddFood("Cheese, 250 kcal", ownerId: null);
        var water = await database.Water();

        var response = await Post(browser, "Half and half", (cheese, 100), (water, 100));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var recipe = await Body(response);
        Assert.Equal(200, recipe.TotalGrams);
        Assert.Equal(125, recipe.Nutrients.Calories);
    }

    [Fact]
    public async Task Post_WithNoIngredients_IsRejected()
    {
        using var browser = await SignedIn();

        var response = await Post(browser, "Nothing at all");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_AFoodThatBelongsToSomebodyElse_IsRejected()
    {
        using var stranger = await SignedIn();
        var theirs = await database.AddFood("Their private cheese", await IdOf(stranger));

        using var browser = await SignedIn();
        var response = await Post(browser, "Stolen soup", (theirs, 100));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_AFoodOfMyOwn_IsAccepted()
    {
        using var browser = await SignedIn();
        var mine = await database.AddFood("My cheese", await IdOf(browser));

        var response = await Post(browser, "My soup", (mine, 100));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Get_ARecipeSomebodyElseComposed_IsNotFound()
    {
        using var owner = await SignedIn();
        var cheese = await database.AddFood("Cheese, shared", ownerId: null);
        var id = (await Body(await Post(owner, "Their soup", (cheese, 100)))).Id;

        using var stranger = await SignedIn();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/recipes/{id}")).StatusCode);
    }

    [Fact]
    public async Task Get_TheList_HoldsOnlyMine()
    {
        using var owner = await SignedIn();
        var cheese = await database.AddFood("Cheese, listed", ownerId: null);
        await Post(owner, "Mine alone", (cheese, 100));

        using var stranger = await SignedIn();
        var theirs = await stranger.GetFromJsonAsync<RecipeListItemDto[]>("/api/recipes");

        Assert.Empty(theirs!);
    }

    [Fact]
    public async Task Grade_ARecipe_IsGradedAsOneFood()
    {
        using var browser = await SignedIn();
        var cheese = await database.AddFood("Cheese, graded", ownerId: null);
        var id = (await Body(await Post(browser, "Graded soup", (cheese, 100)))).Id;

        var graded = await browser.GetFromJsonAsync<GradeResponseDto>(
            $"/api/recipes/{id}/grade?lensId=weight-loss", ApiJson.Options);

        Assert.NotNull(graded!.Grade);
    }

    [Fact]
    public async Task Put_NewIngredients_ReplacesThemAndTheProfileFollows()
    {
        using var browser = await SignedIn();
        var cheese = await database.AddFood("Cheese, edited", ownerId: null);
        var water = await database.Water();
        var id = (await Body(await Post(browser, "Before", (cheese, 100)))).Id;

        var response = await browser.PutAsJsonAsync($"/api/recipes/{id}", Body("After", (water, 100)));

        var recipe = await Body(response);
        Assert.Equal("After", recipe.Name);
        Assert.Equal(0, recipe.Nutrients.Calories);
    }

    [Fact]
    public async Task Delete_MyRecipe_TakesItAndItsIngredients()
    {
        using var browser = await SignedIn();
        var cheese = await database.AddFood("Cheese, deleted", ownerId: null);
        var id = (await Body(await Post(browser, "Doomed", (cheese, 100)))).Id;

        await browser.DeleteAsync($"/api/recipes/{id}");

        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/recipes/{id}")).StatusCode);
        Assert.Equal(0, await database.IngredientsOf(id));
    }

    [Fact]
    public async Task DeletingTheAccount_WithARecipeOverMyOwnFood_TakesEverything()
    {
        using var browser = await SignedIn();
        var mine = await database.AddFood("Cheese only I have", await IdOf(browser));
        var id = (await Body(await Post(browser, "Personal soup", (mine, 100)))).Id;

        var response = await browser.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, "/api/account")
            {
                Content = JsonContent.Create(new { password = Password })
            });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await database.IngredientsOf(id));
        Assert.Equal(0, await database.FoodsWithId(mine));
    }

    [Fact]
    public async Task Export_AfterComposingARecipe_CarriesItWithItsIngredients()
    {
        using var browser = await SignedIn();
        var cheese = await database.AddFood("Cheese, exported", ownerId: null);
        await Post(browser, "Exported soup", (cheese, 150));

        var response = await browser.PostAsJsonAsync(
            "/api/account/export", new { password = Password });

        var export = await response.Content.ReadFromJsonAsync<AccountExportDto>(ApiJson.Options);
        var recipe = Assert.Single(export!.Recipes);
        Assert.Equal("Exported soup", recipe.Name);
        Assert.Equal(150, Assert.Single(recipe.Ingredients).Grams);
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

    private static async Task<string> IdOf(HttpClient browser) =>
        (await browser.GetFromJsonAsync<CurrentUserDto>("/api/auth/me"))!.Id;

    private static Task<HttpResponseMessage> Post(
        HttpClient browser, string name, params (int FoodId, double Grams)[] ingredients) =>
        browser.PostAsJsonAsync("/api/recipes", Body(name, ingredients));

    private static object Body(string name, params (int FoodId, double Grams)[] ingredients) =>
        new
        {
            name,
            ingredients = ingredients
                .Select(ingredient => new { foodId = ingredient.FoodId, grams = ingredient.Grams })
                .ToArray()
        };

    private static async Task<RecipeDetailDto> Body(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<RecipeDetailDto>(ApiJson.Options))!;
}
