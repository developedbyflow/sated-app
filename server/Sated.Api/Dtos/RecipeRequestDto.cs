using System.ComponentModel.DataAnnotations;
using Sated.Data.Entities;

namespace Sated.Api.Dtos;

public record RecipeIngredientRequestDto
{
    [Required]
    public int? FoodId { get; init; }

    [Required]
    [Range(0.1, 100000)]
    public double? Grams { get; init; }
}

public record RecipeRequestDto
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string? Name { get; init; }

    [Required]
    public RecipeIngredientRequestDto[]? Ingredients { get; init; }

    public RecipeIngredient[] ToIngredients() =>
        [.. Ingredients!.Select(ingredient => new RecipeIngredient
        {
            FoodId = ingredient.FoodId!.Value,
            Grams = ingredient.Grams!.Value
        })];

    public Recipe ToRecipe(string ownerId) => new()
    {
        Name = Name!,
        OwnerId = ownerId,
        Ingredients = [.. ToIngredients()]
    };
}
