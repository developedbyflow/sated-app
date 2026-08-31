using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sated.Api.Dtos;

namespace Sated.Api.Tests;

[Collection("Database")]
public class AccountEndpointTests(AccountsDatabase database) : IClassFixture<AccountsDatabase>
{
    private const string Password = "abcdefghijkl";

    private const string HealthData = "/api/consents/HealthData";

    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task Export_WithTheWrongPassword_IsRefused()
    {
        using var browser = await SignedIn();

        var response = await Export(browser, "not the password");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Export_AfterOnboarding_CarriesTheEmailTheWeightAndTheLens()
    {
        var email = AccountsDatabase.UnusedEmail();
        using var browser = await SignedIn(email);
        await Consent(browser);
        await Save(browser, weightKg: 82, lensId: "weight-loss");

        var export = await Exported(browser);

        Assert.Equal(email, export.Email);
        Assert.Equal(82, export.WeightKg);
        Assert.Equal("weight-loss", export.ActiveLensId);
    }

    [Fact]
    public async Task Export_AfterConsenting_CarriesTheWholeTextThatWasSigned()
    {
        using var browser = await SignedIn();
        var offered = await browser.GetFromJsonAsync<ConsentResponseDto>(HealthData, Json);
        await Consent(browser);

        var export = await Exported(browser);

        Assert.Equal(offered!.Text, Assert.Single(export.Consents).Text);
    }

    [Fact]
    public async Task Export_AfterWithdrawing_KeepsTheSignatureAndSaysWhenItEnded()
    {
        using var browser = await SignedIn();
        await Consent(browser);
        await browser.DeleteAsync(HealthData);

        var export = await Exported(browser);

        Assert.NotNull(Assert.Single(export.Consents).WithdrawnAt);
    }

    [Fact]
    public async Task Export_Always_ArrivesAsAFileTheBrowserSaves()
    {
        using var browser = await SignedIn();

        var response = await Export(browser, Password);

        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Contains("sated-export", response.Content.Headers.ContentDisposition.FileName!);
    }

    [Fact]
    public async Task Export_AfterFiveWrongPasswords_IsRefusedEvenWithTheRightOne()
    {
        using var browser = await SignedIn();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Export(browser, "not the password");
        }

        var response = await Export(browser, Password);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithTheWrongPassword_IsRefused()
    {
        using var browser = await SignedIn();

        var response = await Delete(browser, "not the password");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithTheWrongPassword_LeavesTheAccountWhereItWas()
    {
        using var browser = await SignedIn();

        await Delete(browser, "not the password");

        var response = await browser.GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithTheRightPassword_ClearsTheSessionCookie()
    {
        using var browser = await SignedIn();

        var response = await Delete(browser, Password);

        Assert.Contains(SetCookies(response), cookie => cookie.StartsWith("sated.session=;"));
    }

    [Fact]
    public async Task Delete_WithTheRightPassword_FreesTheEmailForANewAccount()
    {
        var email = AccountsDatabase.UnusedEmail();
        using var browser = await SignedIn(email);

        await Delete(browser, Password);

        using var newcomer = database.NewBrowser();
        var response = await Register(newcomer, email);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithTheRightPassword_TakesTheConsentsWithIt()
    {
        using var browser = await SignedIn();
        await Consent(browser);
        var me = await browser.GetFromJsonAsync<CurrentUserDto>("/api/auth/me");

        await Delete(browser, Password);

        Assert.Equal(0, await database.ConsentsOf(me!.Id));
    }

    [Fact]
    public async Task ACopiedCookie_WhileTheAccountLives_Works()
    {
        using var browser = database.NewBrowser();
        using var copy = CopyOfTheSession(await Register(browser, AccountsDatabase.UnusedEmail()));

        var response = await copy.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ACopiedCookie_AfterTheAccountIsDeleted_IsDead()
    {
        using var browser = database.NewBrowser();
        using var copy = CopyOfTheSession(await Register(browser, AccountsDatabase.UnusedEmail()));
        await Delete(browser, Password);

        var response = await copy.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Account_WithoutASession_Answers401()
    {
        using var browser = database.NewBrowser();

        var response = await Export(browser, Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpClient> SignedIn(string? email = null)
    {
        var browser = database.NewBrowser();

        await Register(browser, email ?? AccountsDatabase.UnusedEmail());

        return browser;
    }

    private HttpClient CopyOfTheSession(HttpResponseMessage registered)
    {
        var copy = database.NewBrowser();

        copy.DefaultRequestHeaders.TryAddWithoutValidation(
            "Cookie",
            registered.Headers.GetValues("Set-Cookie")
                .First(cookie => cookie.StartsWith("sated.session"))
                .Split(';')[0]);

        return copy;
    }

    private static IEnumerable<string> SetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [];

    private static Task<HttpResponseMessage> Register(HttpClient browser, string email) =>
        browser.PostAsJsonAsync("/api/auth/register", new { email, password = Password });

    private static Task<HttpResponseMessage> Export(HttpClient browser, string password) =>
        browser.PostAsJsonAsync("/api/account/export", new { password });

    private static async Task<AccountExportDto> Exported(HttpClient browser)
    {
        var response = await Export(browser, Password);

        return (await response.Content.ReadFromJsonAsync<AccountExportDto>(Json))!;
    }

    private static Task<HttpResponseMessage> Delete(HttpClient browser, string password) =>
        browser.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/account")
        {
            Content = JsonContent.Create(new { password })
        });

    private static async Task Consent(HttpClient browser)
    {
        var offered = await browser.GetFromJsonAsync<ConsentResponseDto>(HealthData, Json);

        await browser.PostAsJsonAsync(HealthData, new { version = offered!.Version });
    }

    private static Task<HttpResponseMessage> Save(
        HttpClient browser, double weightKg, string lensId) =>
        browser.PutAsJsonAsync("/api/profile", new { weightKg, activeLensId = lensId });
}
