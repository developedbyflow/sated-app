using System.ComponentModel.DataAnnotations;

namespace Sated.Api.Dtos;

public record ProfileRequestDto
{
    [Required]
    [Range(20, 500)]
    public double? WeightKg { get; init; }

    [Required]
    [Range(100, 250)]
    public double? HeightCm { get; init; }

    [Required]
    public string? ActiveLensId { get; init; }
}
