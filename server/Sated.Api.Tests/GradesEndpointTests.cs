using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Sated.Api.Dtos;
using Sated.Scoring;

namespace Sated.Api.Tests;

public class GradesEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    // The client reads the letter, so it needs the converter the server writes it with. Nothing
    // about a serialiser crosses the wire.
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static readonly GradeRequestDto ChickenBreast = new()
    {
        Lens = "Weight Loss",
        Calories = 165,
        Protein = 31,
        Fat = 3.6,
        Fiber = 0,
        SaturatedFat = 1.0,
        Sodium = 74,
        Carbohydrate = 0
    };

    [Fact]
    public async Task Post_ChickenBreastUnderWeightLoss_GradesA()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/grades", ChickenBreast);

        var body = await response.Content.ReadFromJsonAsync<GradeResponseDto>(Json);

        Assert.Equal(Grade.A, body!.Grade);
    }

    [Fact]
    public async Task Post_TheSameFoodUnderTwoLenses_ScoresDifferently()
    {
        var client = factory.CreateClient();

        var underWeightLoss = await Score(client, ChickenBreast);
        var underFitness = await Score(client, ChickenBreast with { Lens = "Fitness" });

        Assert.NotEqual(underWeightLoss, underFitness);
    }

    [Fact]
    public async Task Post_LensNameInAnotherCase_IsTheSameLens()
    {
        var client = factory.CreateClient();

        var written = await Score(client, ChickenBreast);
        var shouted = await Score(client, ChickenBreast with { Lens = "WEIGHT LOSS" });

        Assert.Equal(written, shouted);
    }

    [Fact]
    public async Task Post_UnknownLens_RejectsTheLensField()
    {
        var response = await factory.CreateClient()
            .PostAsJsonAsync("/api/grades", ChickenBreast with { Lens = "Keto" });

        var problem = await Problem(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(nameof(GradeRequestDto.Lens), problem.Errors.Keys);
    }

    [Fact]
    public async Task Post_MissingNutrients_NamesEveryOneOfThem()
    {
        var response = await factory.CreateClient()
            .PostAsJsonAsync("/api/grades", new GradeRequestDto { Lens = "Fitness", Calories = 165 });

        var problem = await Problem(response);

        Assert.Equal(
            ["Carbohydrate", "Fat", "Fiber", "Protein", "SaturatedFat", "Sodium"],
            problem.Errors.Keys.Order());
    }

    [Fact]
    public async Task Post_KilojoulesSentAsCalories_RejectsTheCaloriesField()
    {
        var response = await factory.CreateClient()
            .PostAsJsonAsync("/api/grades", ChickenBreast with { Calories = 690 });

        var problem = await Problem(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(nameof(GradeRequestDto.Calories), problem.Errors.Keys);
    }

    [Fact]
    public async Task Post_Water_AnswersWithoutALetter()
    {
        var water = new GradeRequestDto
        {
            Lens = "Weight Loss",
            Calories = 0,
            Protein = 0,
            Fat = 0,
            Fiber = 0,
            SaturatedFat = 0,
            Sodium = 4,
            Carbohydrate = 0
        };

        var response = await factory.CreateClient().PostAsJsonAsync("/api/grades", water);

        var body = await response.Content.ReadFromJsonAsync<GradeResponseDto>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(body!.Grade);
    }

    [Fact]
    public async Task Post_NutrientsTheRequestLeftOut_AreReportedAsEstimated()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/grades", ChickenBreast);

        var body = await response.Content.ReadFromJsonAsync<GradeResponseDto>(Json);

        Assert.True(body!.Density!.IsEstimated);
    }

    private static async Task<double> Score(HttpClient client, GradeRequestDto request)
    {
        var response = await client.PostAsJsonAsync("/api/grades", request);

        return (await response.Content.ReadFromJsonAsync<GradeResponseDto>(Json))!.Score;
    }

    private static async Task<ValidationProblemDetails> Problem(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!;
}
