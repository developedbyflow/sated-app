namespace Sated.Data.Entities;

public class Day
{
    public int Id { get; set; }
    public required string OwnerId { get; set; }
    public required DateOnly Date { get; set; }
    public List<Meal> Meals { get; set; } = [];
}
