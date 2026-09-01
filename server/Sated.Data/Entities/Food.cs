namespace Sated.Data.Entities;

public class Food
{
    public int Id { get; set; }
    public int? FdcId { get; set; }
    public required string Description { get; set; }
    public string? Slug { get; set; }
    public required string Category { get; set; }
    public required NutrientAmounts Nutrients { get; set; }
    public required FoodSource Source { get; set; }
    public double? TypicalGrams { get; set; }
    public string? OwnerId { get; set; }
    public List<FoodServing> Servings { get; set; } = [];
}
