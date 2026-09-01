using Microsoft.AspNetCore.Identity;

namespace Sated.Data.Entities;

public class AppUser : IdentityUser
{
    public double? WeightKg { get; set; }

    public double? HeightCm { get; set; }

    public int? CalorieTargetKcal { get; set; }

    public string? ActiveLensId { get; set; }

    public int MealParsesUsed { get; set; }

    public DateTimeOffset? MealParseWindowStartedAt { get; set; }
}
