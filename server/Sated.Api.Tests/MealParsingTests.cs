using System.Net;
using System.Net.Http.Json;
using Sated.Api.Dtos;
using Sated.Parsing;

namespace Sated.Api.Tests;

[Collection("Database")]
public class MealParsingTests(AccountsDatabase database) : IClassFixture<AccountsDatabase>
{
    private const string Password = "abcdefghijkl";

    private const string Sentence = "a bowl of cheese and something nobody sells";

    [Fact]
    public async Task Parse_WithoutASession_Answers401()
    {
        using var browser = database.NewBrowser();

        var response = await Post(browser, Sentence);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Parse_AnEmptySentence_Answers400()
    {
        using var browser = await SignedIn();

        var response = await Post(browser, "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Parse_WithNoProviderConfigured_Answers503AndPointsAtSearch()
    {
        using var browser = await SignedIn();
        database.Parser.Answer = null;

        var response = await Post(browser, Sentence);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("/api/foods?search=", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Parse_ASentenceTheParserResolves_NamesTheFoodAndSavesNothing()
    {
        using var browser = await SignedIn();
        var cheese = await database.AddFood("Cheddar, parsed", ownerId: null);
        Answer(new ParsedItem(cheese, "a bowl of cheese", 120, QuantityEstimated: true));

        var parsed = await Parsed(browser, Sentence);

        var item = Assert.Single(parsed.Items);
        Assert.Equal(cheese, item.FoodId);
        Assert.Equal("Cheddar, parsed", item.Description);
        Assert.Equal(120, item.Grams);
        Assert.True(item.QuantityEstimated);
        Assert.Equal(0, await database.MealsOf(await IdOf(browser)));
    }

    [Fact]
    public async Task Parse_AFoodIdNoRowCarries_IsUnrecognisedRatherThanSubstituted()
    {
        using var browser = await SignedIn();
        Answer(new ParsedItem(999999, "a bowl of cheese", 120, QuantityEstimated: false));

        var parsed = await Parsed(browser, Sentence);

        Assert.Empty(parsed.Items);
        Assert.Equal(["a bowl of cheese"], parsed.Unrecognised);
    }

    [Fact]
    public async Task Parse_AFoodSomebodyElseAdded_IsUnrecognisedToMe()
    {
        using var stranger = await SignedIn();
        var theirs = await database.AddFood(
            "Telemea, somebody else's", ownerId: await IdOf(stranger));
        using var browser = await SignedIn();
        Answer(new ParsedItem(theirs, "telemea", 80, QuantityEstimated: false));

        var parsed = await Parsed(browser, Sentence);

        Assert.Empty(parsed.Items);
        Assert.Equal(["telemea"], parsed.Unrecognised);
    }

    [Fact]
    public async Task Parse_AQuantityOfZero_IsUnrecognisedRatherThanLoggedAsNothing()
    {
        using var browser = await SignedIn();
        var cheese = await database.AddFood("Cheddar, weightless", ownerId: null);
        Answer(new ParsedItem(cheese, "some cheese", 0, QuantityEstimated: false));

        var parsed = await Parsed(browser, Sentence);

        Assert.Empty(parsed.Items);
        Assert.Equal(["some cheese"], parsed.Unrecognised);
    }

    [Fact]
    public async Task Parse_WhatTheParserItselfCouldNotPlace_ComesBackUntouched()
    {
        using var browser = await SignedIn();
        database.Parser.Answer = new ParsedMeal([], ["something nobody sells"]);

        var parsed = await Parsed(browser, Sentence);

        Assert.Equal(["something nobody sells"], parsed.Unrecognised);
    }

    [Fact]
    public async Task Parse_ForAUserWithFoodsOfTheirOwn_SendsTheSharedCatalogueFirst()
    {
        using var browser = await SignedIn();
        await database.AddFood("Cheddar, shared", ownerId: null);
        await database.AddFood("Telemea, mine", ownerId: await IdOf(browser));
        database.Parser.Answer = new ParsedMeal([], []);

        await Post(browser, Sentence);

        var catalogue = database.Parser.Catalogue;
        Assert.StartsWith("CATALOGUE\n", catalogue, StringComparison.Ordinal);
        Assert.True(
            catalogue.IndexOf("Cheddar, shared", StringComparison.Ordinal)
            < catalogue.IndexOf("FOODS THIS PERSON ADDED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Parse_ASentenceThatWasRead_CountsAgainstTheDay()
    {
        using var browser = await SignedIn();
        var cheese = await database.AddFood("Cheddar, counted", ownerId: null);
        Answer(new ParsedItem(cheese, "cheese", 100, QuantityEstimated: false));

        await Post(browser, Sentence);

        Assert.Equal(1, await database.ParsesUsedBy(await IdOf(browser)));
    }

    [Fact]
    public async Task Parse_ASentenceNobodyCouldRead_DoesNotCountAgainstTheDay()
    {
        using var browser = await SignedIn();
        database.Parser.Answer = null;

        await Post(browser, Sentence);

        Assert.Equal(0, await database.ParsesUsedBy(await IdOf(browser)));
    }

    [Fact]
    public async Task Parse_OneSentenceTooManyInADay_IsRefusedAndSaysWhenTheNextIsFree()
    {
        using var browser = await SignedIn();
        var opened = DateTimeOffset.UtcNow.AddHours(-3);
        await database.OpenedTheParseWindowAt(await IdOf(browser), opened, used: 20);
        database.Parser.Answer = new ParsedMeal([], []);

        var response = await Post(browser, Sentence);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Contains(
            opened.AddDays(1).ToString("u"), await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Parse_TheFirstSentenceAfterTheWindowClosed_IsRead()
    {
        using var browser = await SignedIn();
        var cheese = await database.AddFood("Cheddar, a day later", ownerId: null);
        await database.OpenedTheParseWindowAt(
            await IdOf(browser), DateTimeOffset.UtcNow.AddHours(-25), used: 20);
        Answer(new ParsedItem(cheese, "cheese", 100, QuantityEstimated: false));

        var response = await Post(browser, Sentence);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await database.ParsesUsedBy(await IdOf(browser)));
    }

    [Fact]
    public async Task Parse_OneAccountAtItsLimit_LeavesAnotherAccountAlone()
    {
        using var stopped = await SignedIn();
        await database.OpenedTheParseWindowAt(
            await IdOf(stopped), DateTimeOffset.UtcNow, used: 20);
        using var browser = await SignedIn();
        var cheese = await database.AddFood("Cheddar, somebody else's day", ownerId: null);
        Answer(new ParsedItem(cheese, "cheese", 100, QuantityEstimated: false));

        var response = await Post(browser, Sentence);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Post(stopped, Sentence)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AddEntry_AQuantityTheParserGuessed_IsStoredAsGuessed()
    {
        using var browser = await SignedIn();
        var cheese = await database.AddFood("Cheddar, confirmed", ownerId: null);

        var meal = await Logged(browser, cheese, quantityEstimated: true);

        Assert.True(meal.Entries.Single().QuantityEstimated);
    }

    [Fact]
    public async Task Rewrite_AQuantityTheUserTypedOverAGuess_StopsBeingAGuess()
    {
        using var browser = await SignedIn();
        var cheese = await database.AddFood("Cheddar, corrected", ownerId: null);
        var meal = await Logged(browser, cheese, quantityEstimated: true);
        var entry = meal.Entries.Single().Id;

        var rewritten = await browser.PutAsJsonAsync(
            $"/api/meals/{meal.Id}/entries/{entry}", new { grams = 90.0 });

        var after = (await rewritten.Content.ReadFromJsonAsync<MealDetailDto>(ApiJson.Options))!;
        Assert.False(after.Entries.Single().QuantityEstimated);
    }

    [Fact]
    public async Task AddEntry_AQuantityTheUserTyped_IsNotAGuess()
    {
        using var browser = await SignedIn();
        var cheese = await database.AddFood("Cheddar, typed", ownerId: null);

        var meal = await Logged(browser, cheese, quantityEstimated: false);

        Assert.False(meal.Entries.Single().QuantityEstimated);
    }

    private static async Task<MealDetailDto> Logged(
        HttpClient browser, int foodId, bool quantityEstimated)
    {
        var created = await browser.PostAsJsonAsync(
            "/api/meals", new { date = new DateOnly(2026, 9, 1), name = "Lunch" });
        var meal = (await created.Content.ReadFromJsonAsync<MealDetailDto>(ApiJson.Options))!;

        var logged = await browser.PostAsJsonAsync(
            $"/api/meals/{meal.Id}/entries",
            new { foodId, grams = 120.0, quantityEstimated });

        return (await logged.Content.ReadFromJsonAsync<MealDetailDto>(ApiJson.Options))!;
    }

    private void Answer(ParsedItem item) =>
        database.Parser.Answer = new ParsedMeal([item], []);

    private static Task<HttpResponseMessage> Post(HttpClient browser, string text) =>
        browser.PostAsJsonAsync("/api/meals/parse", new { text });

    private static async Task<ParsedMealDto> Parsed(HttpClient browser, string text) =>
        (await (await Post(browser, text)).Content
            .ReadFromJsonAsync<ParsedMealDto>(ApiJson.Options))!;

    private static async Task<string> IdOf(HttpClient browser) =>
        (await browser.GetFromJsonAsync<CurrentUserDto>("/api/auth/me"))!.Id;

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
}
