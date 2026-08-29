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
);

public record ComponentResponseDto(double Score, bool IsEstimated);