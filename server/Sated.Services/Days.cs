using Microsoft.EntityFrameworkCore;
using Sated.Data;
using Sated.Data.Entities;
using Sated.Scoring;

namespace Sated.Services;

public record DayProtein(double Grams, ProteinTarget? Target);

public class Days(SatedDbContext database, FoodGrading grading, ICurrentUser currentUser)
{
    private const double NutrientsAreReportedPer = 100;

    public async Task<DayProtein> ProteinOf(Day? day)
    {
        var grams = day is null
            ? 0
            : day.Meals
                .SelectMany(meal => meal.Entries)
                .Sum(entry =>
                    entry.QuantityGrams / NutrientsAreReportedPer * entry.Food.Nutrients.Protein);

        var user = await database.Users.FirstAsync(stored => stored.Id == currentUser.Id);
        var lens = grading.LensFor(user.ActiveLensId);

        return new DayProtein(
            grams,
            lens is null ? null : ProteinTarget.For(user.WeightKg, user.HeightCm, lens));
    }
}
