namespace Sated.Api.Dtos;

public record ProfileResponseDto(
    double? WeightKg,
    string? ActiveLensId,
    bool HealthDataConsentGiven);
