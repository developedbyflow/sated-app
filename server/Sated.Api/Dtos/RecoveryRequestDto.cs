using System.ComponentModel.DataAnnotations;

namespace Sated.Api.Dtos;

public record ForgotPasswordRequestDto
{
    [Required]
    [EmailAddress]
    public string? Email { get; init; }
}

public record ResetPasswordRequestDto
{
    [Required]
    public string? UserId { get; init; }

    [Required]
    public string? Token { get; init; }

    [Required]
    [StringLength(128, MinimumLength = 12)]
    public string? Password { get; init; }
}

public record ConfirmEmailRequestDto
{
    [Required]
    public string? UserId { get; init; }

    [Required]
    public string? Token { get; init; }
}
