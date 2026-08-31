namespace Sated.Api.Dtos;

public record AccountExportDto(
    DateTimeOffset ExportedAt,
    string Email,
    double? WeightKg,
    string? ActiveLensId,
    ConsentExportDto[] Consents);
