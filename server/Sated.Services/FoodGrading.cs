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

        if (food is null)
        {
            return null;
        }

        var score = combiner.Combine(ScoringInput.From(food), lens);

        return new GradedFood(food.Id, food.Description, calibration.GradeFor(score, lens), score);
    }
}
