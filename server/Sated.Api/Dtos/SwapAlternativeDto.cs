using Sated.Scoring;

namespace Sated.Api.Dtos;

public record SwapAlternativeDto(int Id, string Description, Grade Grade, double Score);
