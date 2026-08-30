using Sated.Scoring;

namespace Sated.Api.Dtos;

public record GradeResponseDto(
    Grade? Grade,
    double Score,
    bool IsPartial,
    ComponentResponseDto Satiety,
    ComponentResponseDto? Density,
    ComponentResponseDto? ProteinQuality,
    ComponentResponseDto? FatQuality
)
{
    public static GradeResponseDto From(Grade? grade, CombinedScore score) => new(
        grade,
        score.Value,
        score.IsPartial,
        Component(score.Satiety),
        ComponentOrNull(score.Density),
        ComponentOrNull(score.ProteinQuality),
        ComponentOrNull(score.FatQuality));

    private static ComponentResponseDto Component(ComponentValue value) =>
        new(value.Score, value.IsEstimated);

    private static ComponentResponseDto? ComponentOrNull(ComponentValue? value) =>
        value is null ? null : Component(value);
}
