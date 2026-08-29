using System.ComponentModel.DataAnnotations;

namespace Sated.Api.Dtos;

public record GradeRequestDto
{
    [Required]
    public string? LensId { get; init; }

    public string? Category { get; init; }

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

    [Required]
    [Range(0, 100)]
    public double? Carbohydrate { get; init; }

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
