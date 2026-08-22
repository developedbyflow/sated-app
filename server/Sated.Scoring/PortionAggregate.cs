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
            Sodium: Per100g(food => food.Sodium));
    }
}
