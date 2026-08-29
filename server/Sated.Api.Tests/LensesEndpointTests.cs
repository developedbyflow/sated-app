using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Sated.Api.Dtos;
using Sated.Scoring;

namespace Sated.Api.Tests;

public class LensesEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Get_Lenses_CarryTheWeightsTheEngineWasCalibratedWith()
    {
        var calibrated = Calibration.Load().Lenses
            .Select(lens => (lens.Name, lens.Satiety, lens.Density, lens.ProteinQuality));

        var served = await factory.CreateClient()
            .GetFromJsonAsync<LensResponseDto[]>("/api/lenses");

        Assert.Equal(
            calibrated,
            served!.Select(lens => (lens.Name, lens.Satiety, lens.Density, lens.ProteinQuality)));
    }
}
