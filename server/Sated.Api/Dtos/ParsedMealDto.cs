using System.ComponentModel.DataAnnotations;
using Sated.Services;

namespace Sated.Api.Dtos;

public record ParseMealRequestDto
{
    [Required]
    [StringLength(500, MinimumLength = 2)]
    public string? Text { get; init; }
}

public record ParsedMealItemDto(
    int FoodId, string Description, string RawText, double Grams, bool QuantityEstimated);

public record ParsedMealDto(ParsedMealItemDto[] Items, string[] Unrecognised)
{
    public static ParsedMealDto From(MealParse parse) => new(
        [.. parse.Items.Select(item => new ParsedMealItemDto(
            item.FoodId,
            item.Description,
            item.RawText,
            item.QuantityGrams,
            item.QuantityEstimated))],
        parse.Unrecognised);
}
