using Sated.Services;

namespace Sated.Api.Dtos;

public record SwapResponseDto(IReadOnlyList<SwapAlternativeDto> Alternatives, string? Message)
{
    private const string NothingBetter = "No higher-graded foods in this category.";

    public static SwapResponseDto From(IReadOnlyList<GradedFood> alternatives) => new(
        [.. alternatives.Select(food =>
            new SwapAlternativeDto(food.Id, food.Description, food.Grade!.Value, food.Score.Value))],
        alternatives.Count == 0 ? NothingBetter : null);
}
