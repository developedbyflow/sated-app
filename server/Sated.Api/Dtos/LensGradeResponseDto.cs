using Sated.Scoring;

namespace Sated.Api.Dtos;

public record LensGradeResponseDto(string LensId, string Name, GradeResponseDto Grade)
{
    public static LensGradeResponseDto From(Lens lens, Grade? grade, CombinedScore score) =>
        new(lens.Id, lens.Name, GradeResponseDto.From(grade, score));
}
