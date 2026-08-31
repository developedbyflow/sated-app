namespace Sated.Data.Entities;

public class MealEntry
{
    public int Id { get; set; }
    public int MealId { get; set; }
    public int FoodId { get; set; }
    public Food Food { get; set; } = null!;
    public required double QuantityGrams { get; set; }
    public required double DisplayAmount { get; set; }
    public required string DisplayUnit { get; set; }
    public bool QuantityEstimated { get; set; }
}
