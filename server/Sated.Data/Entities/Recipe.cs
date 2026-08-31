namespace Sated.Data.Entities;

public class Recipe
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string OwnerId { get; set; }
    public List<RecipeIngredient> Ingredients { get; set; } = [];
}
