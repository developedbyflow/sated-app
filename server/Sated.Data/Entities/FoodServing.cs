namespace Sated.Data.Entities;

public class FoodServing
{
    public int Id { get; set; }
    public int FoodId { get; set; }
    public Food Food { get; set; } = null!;
    public required string Description { get; set; }
    public required double Grams { get; set; }
    public required int Sequence { get; set; }
}
