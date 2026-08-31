using System.Net;
using System.Net.Http.Json;
using Sated.Api.Dtos;

namespace Sated.Api.Tests;

[Collection("Database")]
public class ProfileEndpointTests(AccountsDatabase database) : IClassFixture<AccountsDatabase>
{
    private const string HealthData = "/api/consents/HealthData";

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
        Assert.Equal(180, profile.HeightCm);
        Assert.Equal("weight-loss", profile.ActiveLensId);
    }

    [Fact]
    public async Task Put_WithoutAHeight_IsRejected()
    {
        using var browser = await SignedIn();
        await Consent(browser);

        var response = await browser.PutAsJsonAsync(
            "/api/profile", new { weightKg = 82, activeLensId = "weight-loss" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_HeightBelowTheRange_IsRejected()
    {
        using var browser = await SignedIn();
        await Consent(browser);

        var response = await browser.PutAsJsonAsync(
            "/api/profile", new { weightKg = 82, heightCm = 40, activeLensId = "weight-loss" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

        var consent = await browser.GetFromJsonAsync<ConsentResponseDto>(HealthData, ApiJson.Options);

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
    public async Task Give_Always_RecordsATimeThatSurvivesTheRoundTrip()
    {
        using var browser = await SignedIn();

        var givenAt = await Consent(browser);

        Assert.Equal(0, givenAt!.Value.Ticks % TimeSpan.TicksPerMicrosecond);
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
    public async Task PutCalorieTarget_TwoThousand_IsStoredWithoutAWarning()
    {
        using var browser = await SignedIn();

        var response = await Target(browser, kcal: 2000);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var target = await response.Content.ReadFromJsonAsync<CalorieTargetResponseDto>(ApiJson.Options);
        Assert.Equal(2000, target!.Kcal);
        Assert.Null(target.Warning);
    }

    [Fact]
    public async Task PutCalorieTarget_BelowTwelveHundred_IsStoredAnywayAndWarns()
    {
        using var browser = await SignedIn();

        var response = await Target(browser, kcal: 1100);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var target = await response.Content.ReadFromJsonAsync<CalorieTargetResponseDto>(ApiJson.Options);
        Assert.Equal(1100, target!.Kcal);
        Assert.Equal("Below 1,200 calories a day. Consider talking to a doctor.", target.Warning);

        var profile = await browser.GetFromJsonAsync<ProfileResponseDto>("/api/profile");
        Assert.Equal(1100, profile!.CalorieTargetKcal);
    }

    [Fact]
    public async Task PutCalorieTarget_ExactlyTwelveHundred_DoesNotWarn()
    {
        using var browser = await SignedIn();

        var response = await Target(browser, kcal: 1200);

        var target = await response.Content.ReadFromJsonAsync<CalorieTargetResponseDto>(ApiJson.Options);
        Assert.Null(target!.Warning);
    }

    [Fact]
    public async Task PutCalorieTarget_BelowTheRange_IsRejected()
    {
        using var browser = await SignedIn();

        var response = await Target(browser, kcal: 100);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutCalorieTarget_WithoutConsent_IsStoredAnyway()
    {
        using var browser = await SignedIn();

        var response = await Target(browser, kcal: 2000);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCalorieTarget_AfterSettingOne_LeavesTheRestOfTheProfileAlone()
    {
        using var browser = await SignedIn();
        await Consent(browser);
        await Save(browser, weightKg: 82, lensId: "weight-loss");
        await Target(browser, kcal: 2000);

        var response = await browser.DeleteAsync("/api/profile/calorie-target");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var profile = await browser.GetFromJsonAsync<ProfileResponseDto>("/api/profile");
        Assert.Null(profile!.CalorieTargetKcal);
        Assert.Equal(82, profile.WeightKg);
        Assert.Equal(180, profile.HeightCm);
        Assert.Equal("weight-loss", profile.ActiveLensId);
    }

    [Fact]
    public async Task Withdraw_AfterConsenting_ErasesTheHeightToo()
    {
        using var browser = await SignedIn();
        await Consent(browser);
        await Save(browser, weightKg: 82, lensId: "weight-loss");

        await browser.DeleteAsync(HealthData);

        var profile = await browser.GetFromJsonAsync<ProfileResponseDto>("/api/profile");
        Assert.Null(profile!.HeightCm);
    }

    [Fact]
    public async Task Withdraw_AfterLogging_ErasesWhatTheDocumentPromisesItErases()
    {
        using var browser = await SignedIn();
        await Consent(browser);
        await Save(browser, weightKg: 82, lensId: "weight-loss");

        var created = await browser.PostAsJsonAsync(
            "/api/meals", new { date = new DateOnly(2026, 8, 31), name = "Lunch" });
        var meal = (await created.Content.ReadFromJsonAsync<MealDetailDto>(ApiJson.Options))!;

        await browser.DeleteAsync(HealthData);

        var day = await browser.GetFromJsonAsync<DayDto>("/api/days/2026-08-31", ApiJson.Options);
        Assert.Empty(day!.Meals);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/meals/{meal.Id}")).StatusCode);
    }

    [Fact]
    public async Task Withdraw_ByOneAccount_LeavesAnotherAccountsLogAlone()
    {
        using var mine = await SignedIn();
        using var theirs = await SignedIn();
        await Consent(mine);
        await Consent(theirs);
        await Save(mine, weightKg: 82, lensId: "weight-loss");
        await Save(theirs, weightKg: 70, lensId: "fitness");
        await theirs.PostAsJsonAsync(
            "/api/meals", new { date = new DateOnly(2026, 8, 31), name = "Theirs" });

        await mine.DeleteAsync(HealthData);

        var day = await theirs.GetFromJsonAsync<DayDto>("/api/days/2026-08-31", ApiJson.Options);
        Assert.Single(day!.Meals);
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
        var offered = await browser.GetFromJsonAsync<ConsentResponseDto>(HealthData, ApiJson.Options);

        var response = await browser.PostAsJsonAsync(HealthData, new { version = offered!.Version });
        var given = await response.Content.ReadFromJsonAsync<ConsentResponseDto>(ApiJson.Options);

        return given!.GivenAt;
    }

    private static Task<HttpResponseMessage> Target(HttpClient browser, int kcal) =>
        browser.PutAsJsonAsync("/api/profile/calorie-target", new { kcal });

    private static Task<HttpResponseMessage> Save(
        HttpClient browser, double weightKg, string lensId) =>
        browser.PutAsJsonAsync(
            "/api/profile", new { weightKg, heightCm = 180, activeLensId = lensId });
}
