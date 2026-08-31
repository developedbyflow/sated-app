using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Sated.Api.Dtos;

namespace Sated.Api.Tests;

[Collection("Database")]
public class AuthEndpointTests(AccountsDatabase database) : IClassFixture<AccountsDatabase>
{
    private const string ValidPassword = "abcdefghijkl";

    [Fact]
    public async Task Register_NewEmail_SignsTheUserInImmediately()
    {
        using var browser = database.NewBrowser();
        var email = AccountsDatabase.UnusedEmail();

        await Register(browser, email, ValidPassword);

        var me = await browser.GetFromJsonAsync<CurrentUserDto>("/api/auth/me");
        Assert.Equal(email, me!.Email);
    }

    [Fact]
    public async Task Register_TwelveLowercaseLetters_IsAcceptedWithNoDigitOrSymbol()
    {
        using var browser = database.NewBrowser();

        var response = await Register(browser, AccountsDatabase.UnusedEmail(), "abcdefghijkl");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_ElevenCharacters_IsRejected()
    {
        using var browser = database.NewBrowser();

        var response = await Register(browser, AccountsDatabase.UnusedEmail(), "abcdefghijk");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_EmailAlreadyUsed_IsRejected()
    {
        using var browser = database.NewBrowser();
        var email = AccountsDatabase.UnusedEmail();
        await Register(browser, email, ValidPassword);

        var response = await Register(browser, email, ValidPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_Always_IssuesACookieScriptCannotRead()
    {
        using var browser = database.NewBrowser();

        var response = await Register(browser, AccountsDatabase.UnusedEmail(), ValidPassword);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_CorrectPassword_ReturnsTheUser()
    {
        var email = AccountsDatabase.UnusedEmail();
        using (var registering = database.NewBrowser())
        {
            await Register(registering, email, ValidPassword);
        }

        using var browser = database.NewBrowser();
        var response = await Login(browser, email, ValidPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_AnswersExactlyLikeAnUnknownEmail()
    {
        var email = AccountsDatabase.UnusedEmail();
        using (var registering = database.NewBrowser())
        {
            await Register(registering, email, ValidPassword);
        }

        using var browser = database.NewBrowser();
        var wrongPassword = await Login(browser, email, "wrongbutlongenough");
        var unknownEmail = await Login(browser, AccountsDatabase.UnusedEmail(), ValidPassword);

        Assert.Equal(unknownEmail.StatusCode, wrongPassword.StatusCode);
        Assert.Equal(await Reason(unknownEmail), await Reason(wrongPassword));
    }

    [Fact]
    public async Task Login_FiveWrongPasswords_LocksTheAccountAgainstTheRightOne()
    {
        var email = AccountsDatabase.UnusedEmail();
        using (var registering = database.NewBrowser())
        {
            await Register(registering, email, ValidPassword);
        }

        using var browser = database.NewBrowser();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Login(browser, email, "wrongbutlongenough");
        }

        var response = await Login(browser, email, ValidPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_MoreAttemptsThanTheLimit_IsRefusedWithTooManyRequests()
    {
        using var browser = database.NewThrottledBrowser();
        var email = AccountsDatabase.UnusedEmail();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await Login(browser, email, ValidPassword);
        }

        var response = await Login(browser, email, ValidPassword);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutASession_Answers401WithNowhereToBeSent()
    {
        using var browser = database.NewBrowser();

        var response = await browser.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Logout_AfterSigningIn_MakesTheSessionUnusable()
    {
        using var browser = database.NewBrowser();
        await Register(browser, AccountsDatabase.UnusedEmail(), ValidPassword);

        await browser.PostAsync("/api/auth/logout", null);

        var response = await browser.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<(string?, string?, int?)> Reason(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        return (problem?.Title, problem?.Detail, problem?.Status);
    }

    private static Task<HttpResponseMessage> Register(
        HttpClient browser, string email, string password) =>
        browser.PostAsJsonAsync("/api/auth/register", new { email, password });

    private static Task<HttpResponseMessage> Login(
        HttpClient browser, string email, string password) =>
        browser.PostAsJsonAsync("/api/auth/login", new { email, password });
}
