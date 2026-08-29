using Microsoft.AspNetCore.Mvc;   // atributele: [ApiController], [Route], [HttpGet]
using Sated.Api.Dtos;             // LensResponseDto
using Sated.Scoring;              // Calibration

namespace Sated.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LensesController(Calibration calibration) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<LensResponseDto> Get() =>
        calibration.Lenses.Select(lens => new LensResponseDto(lens.Id, lens.Name, lens.Satiety, lens.Density, lens.ProteinQuality)).ToList();
}