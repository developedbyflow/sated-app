using Sated.Data.Entities;

namespace Sated.Api.Dtos;

public record ConsentExportDto(
    ConsentPurpose Purpose,
    string Version,
    DateTimeOffset GivenAt,
    DateTimeOffset? WithdrawnAt,
    string Text);
