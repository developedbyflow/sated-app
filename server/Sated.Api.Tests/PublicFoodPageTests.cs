using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sated.Api.Dtos;

namespace Sated.Api.Tests;

[Collection("Database")]
public class PublicFoodPageTests(AccountsDatabase database) : IClassFixture<AccountsDatabase>
{
    [Fact]
    public async Task Get_TheSlugOfACatalogueFood_AnswersTheSameBodyAsItsId()
    {
        var id = await database.AddFood(
            "Gouda, on a public page", ownerId: null, slug: "gouda-on-a-public-page");
        using var browser = database.NewBrowser();

        var bySlug = await browser.GetStringAsync("/api/foods/by-slug/gouda-on-a-public-page");

        Assert.Equal(await browser.GetStringAsync($"/api/foods/{id}"), bySlug);
    }

    [Fact]
    public async Task Get_TheSlugOfACatalogueFood_NamesTheSlugItWasFoundBy()
    {
        await database.AddFood(
            "Brie, on a public page", ownerId: null, slug: "brie-on-a-public-page");
        using var browser = database.NewBrowser();

        var food = await browser.GetFromJsonAsync<FoodDetailDto>(
            "/api/foods/by-slug/brie-on-a-public-page", ApiJson.Options);

        Assert.Equal("brie-on-a-public-page", food!.Slug);
    }

    [Fact]
    public async Task Get_ASlugNoFoodCarries_Answers404()
    {
        using var browser = database.NewBrowser();

        var response = await browser.GetAsync("/api/foods/by-slug/telemea-de-bufnita");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_TheSlugShouted_Answers404()
    {
        await database.AddFood(
            "Edam, on a public page", ownerId: null, slug: "edam-on-a-public-page");
        using var browser = database.NewBrowser();

        var response = await browser.GetAsync("/api/foods/by-slug/Edam-On-A-Public-Page");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_ASlugOnAFoodSomebodyOwns_Answers404ToItsOwnerToo()
    {
        using var owner = await SignedIn();
        var me = await owner.GetFromJsonAsync<CurrentUserDto>("/api/auth/me");
        await database.AddFood("Telemea de oaie", ownerId: me!.Id, slug: "telemea-de-oaie");

        var response = await owner.GetAsync("/api/foods/by-slug/telemea-de-oaie");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddFood_ASecondCatalogueFoodUnderTheSameSlug_IsRefusedByTheDatabase()
    {
        await database.AddFood(
            "Roquefort, on a public page", ownerId: null, slug: "roquefort-on-a-public-page");

        await Assert.ThrowsAsync<DbUpdateException>(() => database.AddFood(
            "Roquefort, a second row", ownerId: null, slug: "roquefort-on-a-public-page"));
    }

    private async Task<HttpClient> SignedIn()
    {
        var browser = database.NewBrowser();

        await browser.PostAsJsonAsync("/api/auth/register", new
        {
            email = AccountsDatabase.UnusedEmail(),
            password = "abcdefghijkl"
        });

        return browser;
    }
}
