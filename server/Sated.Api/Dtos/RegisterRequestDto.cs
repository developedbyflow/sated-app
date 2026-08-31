using System.ComponentModel.DataAnnotations;

namespace Sated.Api.Dtos;

public record RegisterRequestDto
{
    [Required]
    [EmailAddress]
    public string? Email { get; init; }

    [Required]
    public string? Password { get; init; }
}
