using Microsoft.EntityFrameworkCore;
using Sated.Data;
using Sated.Scoring;

namespace Sated.Services;

public class FoodSwaps(SatedDbContext database, FoodGrading grading)
{
    private const int HowMany = 3;

    public async Task<IReadOnlyList<GradedFood>?> Better(int foodId, Lens lens)
    {
        var food = await database.Foods.FirstOrDefaultAsync(food => food.Id == foodId);

        if (food is null)
        {
            return null;
        }

        var graded = grading.Grade(ScoringInput.From(food), food.Id, food.Description, lens);

        var sameCategory = await database.Foods
            .Where(other => other.Category == food.Category && other.OwnerId == null)
            .ToListAsync();

        return
        [
            .. sameCategory
                .Select(other => grading.Grade(
                    ScoringInput.From(other), other.Id, other.Description, lens))
                .Where(other => other.Grade < graded.Grade && !other.Score.IsPartial)
                .OrderByDescending(other => other.Score.Value)
                .ThenBy(other => other.Id)
                .Take(HowMany)
        ];
    }
}
