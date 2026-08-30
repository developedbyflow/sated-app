using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sated.Api.Dtos;
using Sated.Data;
using Sated.Data.Entities;

namespace Sated.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoodsController(SatedDbContext database) : ControllerBase
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
}