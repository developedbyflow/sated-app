using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sated.Api.Dtos;
using Sated.Data.Entities;
using Sated.Services;

namespace Sated.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MealsController(Meals meals) : ControllerBase
{
    [HttpGet("{id:int}", Name = "MealById")]
    public async Task<ActionResult<MealDetailDto>> Get(int id)
    {
        var meal = await meals.Find(id);

        return meal is null ? NotFound() : MealDetailDto.From(meal, await Graded(meal));
    }

    [HttpPost]
    public async Task<ActionResult<MealDetailDto>> Post(MealRequestDto request)
    {
        var meal = await meals.Add(request.Date!.Value, request.Name!);

        return CreatedAtRoute("MealById", new { id = meal.Id }, await Detail(meal.Id));
    }

    [HttpPost("{id:int}/entries")]
    public async Task<ActionResult<MealDetailDto>> AddEntry(int id, MealEntryRequestDto request)
    {
        var meal = await meals.Find(id);

        if (meal is null)
        {
            return NotFound();
        }

        var rejection = await meals.AddEntry(
            meal,
            request.FoodId!.Value,
            request.Grams,
            request.ServingCount,
            request.ServingDescription);

        if (rejection is not MealRejection.None)
        {
            return Rejected(rejection, request);
        }

        return await Detail(id);
    }

    private async Task<MealDetailDto> Detail(int id)
    {
        var meal = (await meals.Find(id))!;

        return MealDetailDto.From(meal, await Graded(meal));
    }

    private async Task<GradeResponseDto?> Graded(Meal meal)
    {
        var graded = await meals.GradeOf(meal);

        return graded is null ? null : GradeResponseDto.From(graded.Grade, graded.Score);
    }


    private ActionResult Rejected(MealRejection rejection, MealEntryRequestDto request)
    {
        if (rejection is MealRejection.UnknownFood)
        {
            ModelState.AddModelError(
                nameof(request.FoodId),
                $"No food you can see has the id {request.FoodId}. "
                + "GET /api/foods lists the catalogue and your own.");
        }
        else if (rejection is MealRejection.UnknownServing)
        {
            ModelState.AddModelError(
                nameof(request.ServingDescription),
                $"This food has no serving called '{request.ServingDescription}'. "
                + "GET /api/foods/{id} lists the ones it has.");
        }
        else
        {
            ModelState.AddModelError(
                nameof(request.Grams),
                "Give either grams, or servingCount together with servingDescription. "
                + "Not both, and not neither.");
        }

        return ValidationProblem(ModelState);
    }
}
