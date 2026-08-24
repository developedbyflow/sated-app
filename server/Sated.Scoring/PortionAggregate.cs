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

        // Leucine is still resolved per portion, though no longer for the reason it was: with one
        // share the arithmetic would commute. What does not commute is provenance — one portion
        // may carry a leucine the catalogue measured while the next has none, and merging first
        // would hand the measured half of a plate to the guessed half.
        var leucine = portions.Sum(portion =>
            (portion.Food.LeucinePer100g
                ?? ProteinCompleteness.EstimateLeucinePer100g(portion.Food.Protein))
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
            VitaminA: Per100g(food => food.VitaminA),
            VitaminC: Per100g(food => food.VitaminC),
            VitaminE: Per100g(food => food.VitaminE),
            Calcium: Per100g(food => food.Calcium),
            Iron: Per100g(food => food.Iron),
            Magnesium: Per100g(food => food.Magnesium),
            Potassium: Per100g(food => food.Potassium),
            SaturatedFat: Per100g(food => food.SaturatedFat),
            Sodium: Per100g(food => food.Sodium),
            VitaminD: Per100g(food => food.VitaminD),
            Thiamine: Per100g(food => food.Thiamine),
            LeucinePer100g: leucine,
            LeucineIsEstimated: estimated);
    }
}
