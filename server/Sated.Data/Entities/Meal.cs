namespace Sated.Data.Entities;

public class Meal
{
    public int Id { get; set; }
    public int DayId { get; set; }
    public Day Day { get; set; } = null!;
    public required string Name { get; set; }
    public required DateTimeOffset LoggedAt { get; set; }
    public required string EngineVersion { get; set; }
    public List<MealEntry> Entries { get; set; } = [];
}
