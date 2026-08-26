namespace Sated.Scoring;

/// <summary>
/// Turns the portions of a Recipe or Meal into one nutrient profile per 100 g, so the formula
/// that grades a food grades an aggregate unchanged (FR-8).
/// </summary>
public static class PortionAggregate
{
    // A grade is never the average of its parts' letters. 100 g of spinach (A) with 100 g of
    // butter (E) is not a C: the mixture carries about 740 kcal, 97% of them from the butter,
    // so per 100 kcal it reads as butter. Averaging letters hides that; summing nutrients and
    // renormalising cannot.

    /// <summary>
    /// The category an aggregate carries. It matches no rule on purpose: a mixed plate is not
    /// a member of any WWEIA category, and inheriting one from the largest ingredient would
    /// need a threshold nobody has measured.
    /// </summary>
    public const string MixedCategory = "Mixed portions";

    /// <returns>The portions as a single food, stated per 100 g of total weight.</returns>
    public static FoodInput Aggregate(IReadOnlyList<Portion> portions)
    {
        if (portions.Count == 0)
        {
            throw new ArgumentException(
                "A Recipe or Meal with no portions has no profile to grade.", nameof(portions));
        }

        foreach (var portion in portions)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(portion.Grams);
        }

        var totalGrams = portions.Sum(portion => portion.Grams);

        // Nutrients are stated per 100 g, so the amount on the plate is `value * grams / 100`
        // and spreading it over 100 g of plate multiplies by `100 / totalGrams`. The two
        // hundreds cancel — this is not a missing division.
        double Per100g(Func<FoodInput, double> nutrient) =>
            portions.Sum(portion => nutrient(portion.Food) * portion.Grams) / totalGrams;

        // One portion that does not know its vitamin C makes the plate not know it either. Summing
        // only the portions that do would report the spinach's vitamin C spread over the butter as
        // well, which is a larger claim than any ingredient made. Absent stays absent all the way
        // up, the same way one guessed leucine makes the whole plate a guess below.
        double? Per100gOrNull(Func<FoodInput, double?> nutrient) =>
            portions.Any(portion => nutrient(portion.Food) is null)
                ? null
                : portions.Sum(portion => nutrient(portion.Food)!.Value * portion.Grams)
                    / totalGrams;

        // Leucine is resolved per portion, and now the share is read per portion too. The plate
        // is given a category no catalogue carries, so asking for the share after merging would
        // fall back to the median for every plate — and a plate of nothing but butter would stop
        // grading like butter. Caught by PortionAggregateTests the moment the share stopped being
        // one number, which is the whole reason that test asserts equality.
        var leucine = portions.Sum(portion =>
            (portion.Food.LeucinePer100g
                ?? ProteinCompleteness.EstimateLeucinePer100g(
                    portion.Food.Protein, portion.Food.Category))
            * portion.Grams) / totalGrams;

        // One guessed ingredient makes the whole plate a guess. A Recipe nested in a Meal
        // carries its own flag, so the answer stays right however deep the portions go.
        var estimated = portions.Any(portion =>
            portion.Food.LeucinePer100g is null || portion.Food.LeucineIsEstimated);

        return new FoodInput(
            Category: MixedCategory,
            Calories: Per100g(food => food.Calories),
            Protein: Per100g(food => food.Protein),
            Fat: Per100g(food => food.Fat),
            Fiber: Per100g(food => food.Fiber),
            VitaminA: Per100gOrNull(food => food.VitaminA),
            VitaminC: Per100gOrNull(food => food.VitaminC),
            VitaminE: Per100gOrNull(food => food.VitaminE),
            Calcium: Per100gOrNull(food => food.Calcium),
            Iron: Per100gOrNull(food => food.Iron),
            Magnesium: Per100gOrNull(food => food.Magnesium),
            Potassium: Per100gOrNull(food => food.Potassium),
            SaturatedFat: Per100g(food => food.SaturatedFat),
            Sodium: Per100g(food => food.Sodium),
            VitaminD: Per100gOrNull(food => food.VitaminD),
            Thiamine: Per100gOrNull(food => food.Thiamine),
            LeucinePer100g: leucine,
            LeucineIsEstimated: estimated);
    }
}
