using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sated.Api.Dtos;
using Sated.Data;
using Sated.Data.Entities;
using Sated.Services;

namespace Sated.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoodsController(SatedDbContext database, FoodGrading grading) : ControllerBase
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

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FoodDetailDto>> Get(int id)
    {
        var food = await database.Foods
            .Where(food => food.Id == id)
            .Select(food => new FoodDetailDto(
                food.Id,
                food.FdcId,
                food.Description,
                food.Category,
                new NutrientAmountsDto(
                    food.Nutrients.Calories,
                    food.Nutrients.Protein,
                    food.Nutrients.Fat,
                    food.Nutrients.Fiber,
                    food.Nutrients.SaturatedFat,
                    food.Nutrients.Sodium,
                    food.Nutrients.VitaminA,
                    food.Nutrients.VitaminC,
                    food.Nutrients.VitaminD,
                    food.Nutrients.VitaminE,
                    food.Nutrients.Thiamine,
                    food.Nutrients.Calcium,
                    food.Nutrients.Iron,
                    food.Nutrients.Magnesium,
                    food.Nutrients.Potassium,
                    food.Nutrients.Leucine)))
            .FirstOrDefaultAsync();

        if (food is null)
        {
            return NotFound();
        }

        return food;
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
