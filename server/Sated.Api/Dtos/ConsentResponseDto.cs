using Sated.Data.Entities;

namespace Sated.Api.Dtos;

public record ConsentResponseDto(
    ConsentPurpose Purpose,
    string Version,
    string Text,
    DateTimeOffset? GivenAt);
