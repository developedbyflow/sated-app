using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sated.Api.Dtos;
using Sated.Data;
using Sated.Data.Entities;
using Sated.Services;

namespace Sated.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoodsController(
    SatedDbContext database,
    FoodGrading grading,
    FoodCatalogue catalogue,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<FoodListResponseDto> Get([FromQuery] FoodQueryDto query)
    {
        IQueryable<Food> foods = database.Foods;

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search}%";

            foods = foods.Where(food => EF.Functions.ILike(food.Description, pattern));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            foods = foods.Where(food => food.Category == query.Category);
        }

        var total = await foods.CountAsync();

        var items = await foods
            .OrderBy(food => food.Description)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(food => new FoodListItemDto(food.Id, food.Description, food.Category))
            .ToListAsync();

        return new FoodListResponseDto(items, query.Page, query.PageSize, total);
    }

    [HttpGet("{id:int}", Name = "FoodById")]
    public async Task<ActionResult<FoodDetailDto>> Get(int id)
    {
        var food = await database.Foods.FirstOrDefaultAsync(food => food.Id == id);

        if (food is null)
        {
            return NotFound();
        }

        return FoodDetailDto.From(food);
    }

    [HttpGet("categories")]
    public Task<string[]> Categories() => catalogue.Categories();

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<FoodDetailDto>> Post(CreateFoodRequestDto request)
    {
        var food = request.ToFood(currentUser.Id!);

        var rejection = await catalogue.Add(food, request.Carbohydrate!.Value);

        if (rejection is FoodRejection.UnknownCategory)
        {
            ModelState.AddModelError(
                nameof(request.Category),
                $"No catalogue food is filed under '{request.Category}'. "
                + "GET /api/foods/categories lists the ones the engine has rules for.");

            return ValidationProblem(ModelState);
        }

        if (rejection is FoodRejection.EnergyTooHighForAnyFood)
        {
            ModelState.AddModelError(
                nameof(request.Calories),
                "No 100 g of food carries this much energy. Check that the label is in "
                + "kilocalories, not kilojoules.");

            return ValidationProblem(ModelState);
        }

        if (rejection is FoodRejection.EnergyDisagreesWithTheMacronutrients)
        {
            ModelState.AddModelError(
                nameof(request.Calories),
                "The energy does not follow from the protein, fat and carbohydrate given. "
                + "One of the four is in the wrong unit or was mistyped.");

            return ValidationProblem(ModelState);
        }

        return CreatedAtRoute("FoodById", new { id = food.Id }, FoodDetailDto.From(food));
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

        var graded = await grading.Grade(id, lens);

        if (graded is null)
        {
            return NotFound();
        }

        return GradeResponseDto.From(graded.Grade, graded.Score);
    }
}
