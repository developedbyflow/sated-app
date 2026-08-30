using System.ComponentModel.DataAnnotations;

namespace Sated.Api.Dtos;

public record FoodQueryDto
{
    public string? Search { get; init; }

    public string? Category { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 25;
}