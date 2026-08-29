namespace Sated.Api.Dtos;

public record LensResponseDto(
    string Name,
    double Satiety,
    double Density,
    double ProteinQuality
);