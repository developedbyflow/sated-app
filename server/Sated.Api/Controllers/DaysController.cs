using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sated.Api.Dtos;
using Sated.Services;

namespace Sated.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DaysController(Meals meals) : ControllerBase
{
    [HttpGet("{date}")]
    public async Task<ActionResult<DayDto>> Get(DateOnly date)
    {
        var day = await meals.On(date);

        if (day is null)
        {
            return new DayDto(date, []);
        }

        var listed = new List<MealDetailDto>();

        foreach (var meal in day.Meals)
        {
            var graded = await meals.GradeOf(meal);

            listed.Add(MealDetailDto.From(
                meal, graded is null ? null : GradeResponseDto.From(graded.Grade, graded.Score)));
        }

        return new DayDto(date, [.. listed]);
    }
}
