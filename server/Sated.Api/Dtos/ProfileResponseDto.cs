namespace Sated.Api.Dtos;

public record ProfileResponseDto(
    double? WeightKg,
    double? HeightCm,
    int? CalorieTargetKcal,
    string? ActiveLensId,
    bool HealthDataConsentGiven);
