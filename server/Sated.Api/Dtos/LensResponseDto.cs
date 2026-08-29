namespace Sated.Api.Dtos;

public record LensResponseDto(
    string Id,
    string Name,
    double Satiety,
    double Density,
    double ProteinQuality
);