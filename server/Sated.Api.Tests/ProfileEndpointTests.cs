using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sated.Api.Dtos;

namespace Sated.Api.Tests;

[Collection("Database")]
public class ProfileEndpointTests(AccountsDatabase database) : IClassFixture<AccountsDatabase>
{
    private const string HealthData = "/api/consents/HealthData";

    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task Get_BeforeOnboarding_IsEmptyAndWithoutConsent()
    {
        using var browser = await SignedIn();

        var profile = await browser.GetFromJsonAsync<ProfileResponseDto>("/api/profile");

        Assert.Null(profile!.WeightKg);
        Assert.Null(profile.ActiveLensId);
        Assert.False(profile.HealthDataConsentGiven);
    }

    [Fact]
    public async Task Put_WithoutConsent_IsRefused()
    {
        using var browser = await SignedIn();

        var response = await Save(browser, weightKg: 82, lensId: "weight-loss");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithoutConsent_StoresNothing()
    {
        using var browser = await SignedIn();

        await Save(browser, weightKg: 82, lensId: "weight-loss");

        var profile = await browser.GetFromJsonAsync<ProfileResponseDto>("/api/profile");
        Assert.Null(profile!.WeightKg);
    }

    [Fact]
    public async Task Put_AfterConsent_StoresTheWeightAndTheLens()
    {
        using var browser = await SignedIn();
        await Consent(browser);

        var response = await Save(browser, weightKg: 82, lensId: "weight-loss");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<ProfileResponseDto>();
        Assert.Equal(82, profile!.WeightKg);
        Assert.Equal("weight-loss", profile.ActiveLensId);
    }

    [Fact]
    public async Task Put_LensThatDoesNotExist_IsRejected()
    {
        using var browser = await SignedIn();
        await Consent(browser);

        var response = await Save(browser, weightKg: 82, lensId: "keto");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_WeightBelowTheRange_IsRejected()
    {
        using var browser = await SignedIn();
        await Consent(browser);

        var response = await Save(browser, weightKg: 10, lensId: "weight-loss");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_TheConsentText_CarriesAVersionAndIsNotYetGiven()
    {
        using var browser = await SignedIn();

        var consent = await browser.GetFromJsonAsync<ConsentResponseDto>(HealthData, Json);

        Assert.False(string.IsNullOrWhiteSpace(consent!.Version));
        Assert.Contains("withdraw", consent.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Null(consent.GivenAt);
    }

    [Fact]
    public async Task Give_AVersionThatWasNeverPublished_IsRejected()
    {
        using var browser = await SignedIn();

        var response = await browser.PostAsJsonAsync(HealthData, new { version = "1999-01-01" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Give_TheSameVersionTwice_KeepsTheFirstSignature()
    {
        using var browser = await SignedIn();
        var first = await Consent(browser);

        var second = await Consent(browser);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Withdraw_AfterConsenting_ErasesTheWeightItCovered()
    {
        using var browser = await SignedIn();
        await Consent(browser);
        await Save(browser, weightKg: 82, lensId: "weight-loss");

        await browser.DeleteAsync(HealthData);

        var profile = await browser.GetFromJsonAsync<ProfileResponseDto>("/api/profile");
        Assert.Null(profile!.WeightKg);
        Assert.False(profile.HealthDataConsentGiven);
    }

    [Fact]
    public async Task Withdraw_WithNothingToWithdraw_IsNotFound()
    {
        using var browser = await SignedIn();

        var response = await browser.DeleteAsync(HealthData);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WithoutASession_Answers401()
    {
        using var browser = database.NewBrowser();

        var response = await browser.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Consents_WithoutASession_Answers401()
    {
        using var browser = database.NewBrowser();

        var response = await browser.GetAsync(HealthData);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

    private static async Task<DateTimeOffset?> Consent(HttpClient browser)
    {
        var offered = await browser.GetFromJsonAsync<ConsentResponseDto>(HealthData, Json);

        var response = await browser.PostAsJsonAsync(HealthData, new { version = offered!.Version });
        var given = await response.Content.ReadFromJsonAsync<ConsentResponseDto>(Json);

        return given!.GivenAt;
    }

    private static Task<HttpResponseMessage> Save(
        HttpClient browser, double weightKg, string lensId) =>
        browser.PutAsJsonAsync("/api/profile", new { weightKg, activeLensId = lensId });
}
