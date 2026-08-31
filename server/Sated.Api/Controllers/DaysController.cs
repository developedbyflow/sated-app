using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sated.Api.Dtos;
using Sated.Services;

namespace Sated.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DaysController(Meals meals, Days days) : ControllerBase
{
    [HttpGet("{date}")]
    public async Task<ActionResult<DayDto>> Get(DateOnly date)
    {
        var day = await meals.On(date);
        var summary = await days.Summarise(day);

        var protein = DayProteinDto.From(summary.Protein);
        var calories = DayCaloriesDto.From(summary.Calories);
        var grade = summary.Grade is null
            ? null
            : GradeResponseDto.From(summary.Grade.Grade, summary.Grade.Score);

        if (day is null)
        {
            return new DayDto(date, protein, calories, grade, []);
        }

        var listed = new List<MealDetailDto>();

        foreach (var meal in day.Meals)
        {
            var graded = await meals.GradeOf(meal);

            listed.Add(MealDetailDto.From(
                meal, graded is null ? null : GradeResponseDto.From(graded.Grade, graded.Score)));
        }

        return new DayDto(date, protein, calories, grade, [.. listed]);
    }
}
