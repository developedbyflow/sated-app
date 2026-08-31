using System.ComponentModel.DataAnnotations;

namespace Sated.Api.Dtos;

public record CalorieTargetRequestDto
{
    [Required]
    [Range(500, 20000)]
    public int? Kcal { get; init; }
}
