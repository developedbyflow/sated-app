using System.ComponentModel.DataAnnotations;
using Sated.Data.Entities;

namespace Sated.Api.Dtos;

public record CreateFoodRequestDto
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string? Description { get; init; }

    [Required]
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
    public double? Carbohydrate { get; init; }

    [Required]
    [Range(0, 100)]
    public double? Fiber { get; init; }

    [Required]
    [Range(0, 100)]
    public double? SaturatedFat { get; init; }

    [Required]
    [Range(0, 40000)]
    public double? Sodium { get; init; }

    [Range(0, double.MaxValue)]
    public double? VitaminD { get; init; }

    [Range(0, double.MaxValue)]
    public double? Calcium { get; init; }

    [Range(0, double.MaxValue)]
    public double? Iron { get; init; }

    [Range(0, double.MaxValue)]
    public double? Potassium { get; init; }

    public Food ToFood(string ownerId) => new()
    {
        Description = Description!,
        Category = Category!,
        OwnerId = ownerId,
        Nutrients = new NutrientAmounts
        {
            Calories = Calories!.Value,
            Protein = Protein!.Value,
            Fat = Fat!.Value,
            Fiber = Fiber!.Value,
            SaturatedFat = SaturatedFat!.Value,
            Sodium = Sodium!.Value,
            VitaminD = VitaminD,
            Calcium = Calcium,
            Iron = Iron,
            Potassium = Potassium
        }
    };
}
