namespace Sated.Api.Dtos;

public record ProfileResponseDto(
    double? WeightKg,
    double? HeightCm,
    string? ActiveLensId,
    bool HealthDataConsentGiven);
