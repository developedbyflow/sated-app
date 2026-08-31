using System.Net;
using System.Net.Http.Json;
using Sated.Api.Dtos;

namespace Sated.Api.Tests;

[Collection("Database")]
public class FoodOwnershipTests(AccountsDatabase database) : IClassFixture<AccountsDatabase>
{
    [Fact]
    public async Task AFoodIOwn_IsFoundByMySearch()
    {
        using var mine = await SignedIn();
        await database.AddFood("Telemea de oaie", await IdOf(mine));

        var found = await Search(mine, "telemea");

        Assert.Equal(1, found);
    }

    [Fact]
    public async Task AFoodSomeoneElseOwns_IsNotFoundByMySearch()
    {
        using var owner = await SignedIn();
        await database.AddFood("Telemea de oaie", await IdOf(owner));

        using var stranger = await SignedIn();

        Assert.Equal(0, await Search(stranger, "telemea"));
    }

    [Fact]
    public async Task AFoodSomeoneElseOwns_IsNotFoundBySignedOutSearch()
    {
        using var owner = await SignedIn();
        await database.AddFood("Telemea de oaie", await IdOf(owner));

        using var anonymous = database.NewBrowser();

        Assert.Equal(0, await Search(anonymous, "telemea"));
    }

    [Fact]
    public async Task AFoodSomeoneElseOwns_CannotBeFetchedByItsId()
    {
        using var owner = await SignedIn();
        var id = await database.AddFood("Telemea de oaie", await IdOf(owner));

        using var stranger = await SignedIn();
        var response = await stranger.GetAsync($"/api/foods/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AFoodSomeoneElseOwns_CannotBeGraded()
    {
        using var owner = await SignedIn();
        var id = await database.AddFood("Telemea de oaie", await IdOf(owner));

        using var stranger = await SignedIn();
        var response = await stranger.GetAsync($"/api/foods/{id}/grade?lensId=weight-loss");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AFoodInTheCatalogue_IsFoundByEveryone()
    {
        await database.AddFood("Cheddar of the commons", ownerId: null);

        using var anonymous = database.NewBrowser();

        Assert.Equal(1, await Search(anonymous, "commons"));
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

    private static async Task<string> IdOf(HttpClient browser) =>
        (await browser.GetFromJsonAsync<CurrentUserDto>("/api/auth/me"))!.Id;

    private static async Task<int> Search(HttpClient browser, string term) =>
        (await browser.GetFromJsonAsync<FoodListResponseDto>($"/api/foods?search={term}", ApiJson.Options))!.Total;
}
