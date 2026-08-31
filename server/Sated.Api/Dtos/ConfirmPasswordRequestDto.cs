using System.ComponentModel.DataAnnotations;

namespace Sated.Api.Dtos;

public record ConfirmPasswordRequestDto
{
    [Required]
    public string? Password { get; init; }
}
