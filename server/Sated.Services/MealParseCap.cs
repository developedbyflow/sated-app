namespace Sated.Services;

public record MealParseCap(int PerDay)
{
    public static readonly TimeSpan Window = TimeSpan.FromDays(1);
}
