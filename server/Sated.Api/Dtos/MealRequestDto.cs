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
    public int? FoodId { get; init; }

    public int? RecipeId { get; init; }

    [Range(0.1, 20000)]
    public double? Grams { get; init; }

    [Range(0.01, 1000)]
    public double? ServingCount { get; init; }

    public string? ServingDescription { get; init; }

    public bool? QuantityEstimated { get; init; }
}

public record MealRenameRequestDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; init; }
}

public record MealQuantityRequestDto
{
    [Range(0.1, 20000)]
    public double? Grams { get; init; }

    [Range(0.01, 1000)]
    public double? ServingCount { get; init; }

    public string? ServingDescription { get; init; }
}
