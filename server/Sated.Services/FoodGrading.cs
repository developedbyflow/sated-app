using Microsoft.EntityFrameworkCore;
using Sated.Data;
using Sated.Scoring;

namespace Sated.Services;

public class FoodGrading(SatedDbContext database, Calibration calibration, ScoreCombiner combiner)
{
    public Lens? LensFor(string? lensId) =>
        calibration.Lenses.FirstOrDefault(lens =>
            string.Equals(lens.Id, lensId, StringComparison.OrdinalIgnoreCase));

    public async Task<GradedFood?> Grade(int foodId, Lens lens)
    {
        var food = await database.Foods.FirstOrDefaultAsync(food => food.Id == foodId);

        return food is null
            ? null
            : Grade(ScoringInput.From(food), food.Id, food.Description, lens);
    }

    public async Task<IReadOnlyList<LensGrade>?> GradeUnderEveryLens(int foodId)
    {
        var food = await database.Foods.FirstOrDefaultAsync(food => food.Id == foodId);

        if (food is null)
        {
            return null;
        }

        var input = ScoringInput.From(food);

        return [.. calibration.Lenses.Select(lens =>
            new LensGrade(lens, Grade(input, food.Id, food.Description, lens)))];
    }

    public GradedFood Grade(FoodInput input, int id, string description, Lens lens)
    {
        var score = combiner.Combine(input, lens);

        return new GradedFood(id, description, calibration.GradeFor(score, lens), score);
    }
}
