using Microsoft.AspNetCore.Identity;

namespace Sated.Data.Entities;

public class AppUser : IdentityUser
{
    public double? WeightKg { get; set; }

    public string? ActiveLensId { get; set; }
}
