using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sated.Api.Dtos;
using Sated.Data;
using Sated.Data.Entities;
using Sated.Services;

namespace Sated.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecipesController(
    Recipes recipes, FoodGrading grading, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<RecipeListItemDto[]> Get() =>
        [.. (await recipes.Mine()).Select(RecipeDetailDto.Listed)];

    [HttpGet("{id:int}", Name = "RecipeById")]
    public async Task<ActionResult<RecipeDetailDto>> Get(int id)
    {
        var recipe = await recipes.Find(id);

        return recipe is null ? NotFound() : RecipeDetailDto.From(recipe);
    }

    [HttpPost]
    public async Task<ActionResult<RecipeDetailDto>> Post(RecipeRequestDto request)
    {
        var recipe = request.ToRecipe(currentUser.Id!);

        var rejection = await recipes.Add(recipe);

        if (rejection is not RecipeRejection.None)
        {
            return Rejected(rejection);
        }

        return CreatedAtRoute(
            "RecipeById", new { id = recipe.Id }, RecipeDetailDto.From((await recipes.Find(recipe.Id))!));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RecipeDetailDto>> Put(int id, RecipeRequestDto request)
    {
        var stored = await recipes.Find(id);

        if (stored is null)
        {
            return NotFound();
        }

        var rejection = await recipes.Replace(stored, request.Name!, request.ToIngredients());

        if (rejection is not RecipeRejection.None)
        {
            return Rejected(rejection);
        }

        return RecipeDetailDto.From((await recipes.Find(id))!);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var recipe = await recipes.Find(id);

        if (recipe is null)
        {
            return NotFound();
        }

        await recipes.Remove(recipe);

        return NoContent();
    }

    [HttpGet("{id:int}/grade")]
    public async Task<ActionResult<GradeResponseDto>> Grade(int id, [FromQuery] string? lensId)
    {
        var lens = grading.LensFor(lensId);

        if (lens is null)
        {
            ModelState.AddModelError(
                nameof(lensId),
                $"No lens has the id '{lensId}'. GET /api/lenses lists the ones that exist.");

            return ValidationProblem(ModelState);
        }

        var recipe = await recipes.Find(id);

        if (recipe is null)
        {
            return NotFound();
        }

        var graded = grading.Grade(Services.Recipes.Profile(recipe), recipe.Id, recipe.Name, lens);

        return GradeResponseDto.From(graded.Grade, graded.Score);
    }

    private ActionResult Rejected(RecipeRejection rejection)
    {
        ModelState.AddModelError(
            nameof(RecipeRequestDto.Ingredients),
            rejection is RecipeRejection.NoIngredients
                ? "A recipe needs at least one ingredient. There is nothing to grade otherwise."
                : "One of the foods does not exist, or belongs to somebody else. "
                    + "GET /api/foods lists the ones you can use.");

        return ValidationProblem(ModelState);
    }
}
