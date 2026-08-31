using Microsoft.EntityFrameworkCore;
using Sated.Data;
using Sated.Data.Entities;
using Sated.Scoring;

namespace Sated.Services;

public record DayProtein(double Grams, ProteinTarget? Target);

public record DayCalories(double Consumed, int TargetKcal);

public record DaySummary(DayProtein Protein, DayCalories? Calories, GradedFood? Grade);

public class Days(SatedDbContext database, FoodGrading grading, ICurrentUser currentUser)
{
    private const double NutrientsAreReportedPer = 100;

    public async Task<DaySummary> Summarise(Day? day)
    {
        var user = await database.Users.FirstAsync(stored => stored.Id == currentUser.Id);
        var lens = grading.LensFor(user.ActiveLensId);

        MealEntry[] entries = day is null
            ? []
            : [.. day.Meals.SelectMany(meal => meal.Entries)];

        var protein = new DayProtein(
            entries.Sum(entry =>
                entry.QuantityGrams / NutrientsAreReportedPer * entry.Food.Nutrients.Protein),
            lens is null ? null : ProteinTarget.For(user.WeightKg, user.HeightCm, lens));

        var calories = user.CalorieTargetKcal is null
            ? null
            : new DayCalories(
                entries.Sum(entry =>
                    entry.QuantityGrams / NutrientsAreReportedPer * entry.Food.Nutrients.Calories),
                user.CalorieTargetKcal.Value);

        var grade = entries.Length == 0 || lens is null
            ? null
            : grading.Grade(Profile(entries), day!.Id, day.Date.ToString("O"), lens);

        return new DaySummary(protein, calories, grade);
    }

    public static FoodInput Profile(IReadOnlyList<MealEntry> entries) =>
        PortionAggregate.Aggregate(
            [.. entries.Select(entry =>
                new Portion(ScoringInput.From(entry.Food), entry.QuantityGrams))]);
}
