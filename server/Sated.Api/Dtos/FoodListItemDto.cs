using Sated.Data.Entities;

namespace Sated.Api.Dtos;

public record FoodListItemDto(int Id, string Description, string Category, FoodSource Source);
