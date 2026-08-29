using System.ComponentModel.DataAnnotations;

namespace Sated.Api.Dtos;

/// <summary>
/// One food's nutrients per 100 g, and the lens to grade it under. Grams unless said otherwise;
/// vitamin A and vitamin D are micrograms, every other micronutrient is milligrams.
/// </summary>
/// <remarks>
/// A micronutrient left out is unknown, never zero: the engine drops an unknown component and
/// says so in the response, where a zero would claim the food contains none of it.
/// </remarks>
public record GradeRequestDto
{
    [Required]
    public string? Lens { get; init; }

    /// <summary>The catalogue's own category name, or absent for a food a person typed in.</summary>
    public string? Category { get; init; }

    /// <summary>Kilocalories per 100 g. Not kilojoules — the response says so if it looks like it.</summary>
    [Required]
    [Range(0, double.MaxValue)]
    public double? Calories { get; init; }

    [Required]
    [Range(0, 100)]
    public double? Protein { get; init; }

    [Required]
    [Range(0, 100)]
    public double? Fat { get; init; }

    [Required]
    [Range(0, 100)]
    public double? Fiber { get; init; }

    [Required]
    [Range(0, 100)]
    public double? SaturatedFat { get; init; }

    [Required]
    [Range(0, double.MaxValue)]
    public double? Sodium { get; init; }

    /// <summary>Not scored. It is what tells kilojoules from kilocalories.</summary>
    [Required]
    [Range(0, 100)]
    public double? Carbohydrate { get; init; }

    /// <summary>Grams of ethanol per 100 g. Absent for everything that is not a drink.</summary>
    [Range(0, 100)]
    public double? Alcohol { get; init; }

    [Range(0, double.MaxValue)]
    public double? VitaminA { get; init; }

    [Range(0, double.MaxValue)]
    public double? VitaminC { get; init; }

    [Range(0, double.MaxValue)]
    public double? VitaminE { get; init; }

    [Range(0, double.MaxValue)]
    public double? Calcium { get; init; }

    [Range(0, double.MaxValue)]
    public double? Iron { get; init; }

    [Range(0, double.MaxValue)]
    public double? Magnesium { get; init; }

    [Range(0, double.MaxValue)]
    public double? Potassium { get; init; }

    [Range(0, double.MaxValue)]
    public double? VitaminD { get; init; }

    [Range(0, double.MaxValue)]
    public double? Thiamine { get; init; }
}
