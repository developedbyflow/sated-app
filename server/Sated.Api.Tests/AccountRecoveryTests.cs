using System.Net;
using System.Net.Http.Json;
using Sated.Api.Dtos;

namespace Sated.Api.Tests;

[Collection("Database")]
public class AccountRecoveryTests(AccountsDatabase database) : IClassFixture<AccountsDatabase>
{
    private const string Password = "abcdefghijkl";

    private const string NewPassword = "mnopqrstuvwx";

    [Fact]
    public async Task Register_ANewAccount_SendsALinkToConfirmTheAddress()
    {
        var email = AccountsDatabase.UnusedEmail();

        using var browser = await Registered(email);

        var sent = Assert.Single(database.Post.To(email));
        Assert.Equal("Confirm your email address", sent.Subject);
    }

    [Fact]
    public async Task ConfirmEmail_TheLinkFromRegistration_ConfirmsTheAddress()
    {
        var email = AccountsDatabase.UnusedEmail();
        using var browser = await Registered(email);

        var confirmed = await Confirm(browser, database.Post.To(email).Single());

        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        Assert.True(await database.EmailIsConfirmed(email));
    }

    [Fact]
    public async Task ConfirmEmail_ATokenNobodyWasSent_IsRefused()
    {
        var email = AccountsDatabase.UnusedEmail();
        using var browser = await Registered(email);
        var sent = database.Post.To(email).Single();

        var confirmed = await browser.PostAsJsonAsync("/api/auth/confirm-email", new
        {
            userId = database.Post.UserIdIn(sent),
            token = "a token nobody generated"
        });

        Assert.Equal(HttpStatusCode.BadRequest, confirmed.StatusCode);
        Assert.False(await database.EmailIsConfirmed(email));
    }

    [Fact]
    public async Task ForgotPassword_AnAddressWithNoAccount_AnswersLikeAnAddressWithOne()
    {
        var stranger = AccountsDatabase.UnusedEmail();
        var known = AccountsDatabase.UnusedEmail();
        using var browser = await Registered(known);

        var forStranger = await Forgot(browser, stranger);

        Assert.Equal((await Forgot(browser, known)).StatusCode, forStranger.StatusCode);
        Assert.Empty(database.Post.To(stranger));
    }

    [Fact]
    public async Task ResetPassword_TheLinkThatWasEmailed_LetsTheNewPasswordIn()
    {
        var email = AccountsDatabase.UnusedEmail();
        using var browser = await Registered(email);
        await Forgot(browser, email);

        var reset = await Reset(browser, ResetMailTo(email), NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(email, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(email, Password)).StatusCode);
    }

    [Fact]
    public async Task ResetPassword_TheSameLinkASecondTime_IsRefused()
    {
        var email = AccountsDatabase.UnusedEmail();
        using var browser = await Registered(email);
        await Forgot(browser, email);
        var link = ResetMailTo(email);
        await Reset(browser, link, NewPassword);

        var again = await Reset(browser, link, "yzabcdefghij");

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task ResetPassword_AnAddressThatWasNeverConfirmed_ConfirmsItOnTheWay()
    {
        var email = AccountsDatabase.UnusedEmail();
        using var browser = await Registered(email);
        await Forgot(browser, email);

        await Reset(browser, ResetMailTo(email), NewPassword);

        Assert.True(await database.EmailIsConfirmed(email));
    }

    [Fact]
    public async Task Login_AnAccountWhoseAddressIsNotConfirmed_WorksLikeAnyOther()
    {
        var email = AccountsDatabase.UnusedEmail();
        using var browser = await Registered(email);

        var me = await browser.GetFromJsonAsync<CurrentUserDto>("/api/auth/me");

        Assert.False(await database.EmailIsConfirmed(email));
        Assert.Equal(email, me!.Email);
    }

    [Fact]
    public async Task Login_FiveWrongPasswords_TellsTheOwnerOnceAndAnswersTheSame401()
    {
        var email = AccountsDatabase.UnusedEmail();
        using var browser = await Registered(email);

        for (var attempt = 0; attempt < 7; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Login(email, "wrongwrongwrong")).StatusCode);
        }

        var warning = Assert.Single(database.Post.To(email), sent => sent.Subject.StartsWith("Somebody tried"));
        Assert.Contains("Nobody got in", warning.Body, StringComparison.Ordinal);
        Assert.Contains("attempts that failed", warning.Body, StringComparison.Ordinal);
    }

    private EmailMessageUnderTest ResetMailTo(string email)
    {
        var sent = database.Post.To(email).Last(message => message.Subject.StartsWith("Reset"));

        return new EmailMessageUnderTest(database.Post.UserIdIn(sent), database.Post.TokenIn(sent));
    }

    private Task<HttpResponseMessage> Confirm(HttpClient browser, Services.EmailMessage sent) =>
        browser.PostAsJsonAsync("/api/auth/confirm-email", new
        {
            userId = database.Post.UserIdIn(sent),
            token = database.Post.TokenIn(sent)
        });

    private static Task<HttpResponseMessage> Reset(
        HttpClient browser, EmailMessageUnderTest link, string password) =>
        browser.PostAsJsonAsync("/api/auth/reset-password", new
        {
            userId = link.UserId,
            token = link.Token,
            password
        });

    private static Task<HttpResponseMessage> Forgot(HttpClient browser, string email) =>
        browser.PostAsJsonAsync("/api/auth/forgot-password", new { email });

    private async Task<HttpResponseMessage> Login(string email, string password)
    {
        using var browser = database.NewBrowser();

        return await browser.PostAsJsonAsync("/api/auth/login", new { email, password });
    }

    private async Task<HttpClient> Registered(string email)
    {
        var browser = database.NewBrowser();

        await browser.PostAsJsonAsync("/api/auth/register", new { email, password = Password });

        return browser;
    }

    private record EmailMessageUnderTest(string UserId, string Token);
}
