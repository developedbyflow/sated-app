using System.ComponentModel.DataAnnotations;

namespace Sated.Api.Dtos;

public record MealRequestDto
{
    [Required]
    public DateOnly? Date { get; init; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; init; }
}

public record MealEntryRequestDto
{
    [Required]
    public int? FoodId { get; init; }

    [Range(0.1, 20000)]
    public double? Grams { get; init; }

    [Range(0.01, 1000)]
    public double? ServingCount { get; init; }

    public string? ServingDescription { get; init; }
}
