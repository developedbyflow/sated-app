using System.ComponentModel.DataAnnotations;

namespace Sated.Api.Dtos;

public record GiveConsentRequestDto
{
    [Required]
    public string? Version { get; init; }
}
