using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sated.Api.Dtos;
using Sated.Data.Entities;
using Sated.Services;

namespace Sated.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MealsController(Meals meals, MealParsing parsing) : ControllerBase
{
    [HttpPost("parse")]
    public async Task<ActionResult<ParsedMealDto>> Parse(
        ParseMealRequestDto request, CancellationToken cancellation)
    {
        var parsed = await parsing.Of(request.Text!, cancellation);

        return parsed is null
            ? Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Reading a sentence is unavailable",
                detail: "Nothing was logged and nothing was lost. Search for each food instead: "
                    + "GET /api/foods?search=…")
            : ParsedMealDto.From(parsed);
    }

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

        if (request.FoodId is null == request.RecipeId is null)
        {
            ModelState.AddModelError(
                nameof(request.FoodId),
                "Give either foodId or recipeId. Not both, and not neither.");

            return ValidationProblem(ModelState);
        }

        var rejection = request.RecipeId is null
            ? await meals.AddEntry(
                meal,
                request.FoodId!.Value,
                request.Grams,
                request.ServingCount,
                request.ServingDescription,
                request.QuantityEstimated ?? false)
            : await meals.AddRecipe(meal, request.RecipeId.Value, request.Grams);

        if (rejection is not MealRejection.None)
        {
            return Rejected(rejection, request);
        }

        return await Detail(id);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<MealDetailDto>> Rename(int id, MealRenameRequestDto request)
    {
        var meal = await meals.Find(id);

        if (meal is null)
        {
            return NotFound();
        }

        await meals.Rename(meal, request.Name!);

        return await Detail(id);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var meal = await meals.Find(id);

        if (meal is null)
        {
            return NotFound();
        }

        await meals.Remove(meal);

        return NoContent();
    }

    [HttpPut("{id:int}/entries/{entryId:int}")]
    public async Task<ActionResult<MealDetailDto>> Rewrite(
        int id, int entryId, MealQuantityRequestDto request)
    {
        var meal = await meals.Find(id);

        if (meal is null)
        {
            return NotFound();
        }

        var rejection = await meals.Rewrite(
            meal, entryId, request.Grams, request.ServingCount, request.ServingDescription);

        if (rejection is MealRejection.UnknownEntry)
        {
            return NotFound();
        }

        if (rejection is not MealRejection.None)
        {
            return Rejected(rejection, new MealEntryRequestDto
            {
                Grams = request.Grams,
                ServingCount = request.ServingCount,
                ServingDescription = request.ServingDescription
            });
        }

        return await Detail(id);
    }

    [HttpDelete("{id:int}/entries/{entryId:int}")]
    public async Task<ActionResult<MealDetailDto>> RemoveEntry(int id, int entryId)
    {
        var meal = await meals.Find(id);

        if (meal is null || !await meals.RemoveEntry(meal, entryId))
        {
            return NotFound();
        }

        return await Detail(id);
    }

    [HttpDelete("{id:int}/recipes/{fromRecipeId:int}")]
    public async Task<ActionResult<MealDetailDto>> RemoveLoggedRecipe(int id, int fromRecipeId)
    {
        var meal = await meals.Find(id);

        if (meal is null || await meals.RemoveLoggedRecipe(meal, fromRecipeId) == 0)
        {
            return NotFound();
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
        else if (rejection is MealRejection.UnknownRecipe)
        {
            ModelState.AddModelError(
                nameof(request.RecipeId),
                $"No recipe you can see has the id {request.RecipeId}, or it has no ingredients. "
                + "GET /api/recipes lists yours.");
        }
        else if (rejection is MealRejection.RecipeNeedsGrams)
        {
            ModelState.AddModelError(
                nameof(request.Grams),
                "Say how many grams of the recipe you ate. Servings belong to a food, not a recipe.");
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
