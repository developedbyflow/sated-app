namespace Sated.Api.Dtos;

public record FoodListResponseDto(
    IReadOnlyList<FoodListItemDto> Items,
    int Page,
    int PageSize,
    int Total
);

public record FoodListItemDto(int Id, string Description, string Category);