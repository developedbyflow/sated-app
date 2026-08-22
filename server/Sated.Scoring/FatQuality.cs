namespace Sated.Scoring;

/// <summary>
/// Density for foods that are almost entirely fat, where NRF9.2 carries no information: all nine
/// encouraged nutrients sit near zero, so olive oil and butter both land at the bottom of the
/// catalogue and take the same letter (FR-6).
/// </summary>
public static class FatQuality
{
    // A share needs no reference value, which is what stopped P24 from bringing unsaturated fat
    // back into the general formula: FR-2 asked for it, but neither a formula nor a %DV existed,
    // and inventing both would have shipped our guess as a borrowed standard.
    // Sodium stays as the one limiter — it is what separates mayonnaise from olive oil, and
    // without it the two tie. Saturated fat is not subtracted again: it is already the other
    // half of the share.
    // FNDDS reports no trans fat at all, so "unsaturated" here means "not saturated" and trans
    // fat is credited as good. Margarine is the food this flatters.

    public static ComponentValue? UnsaturatedShare(FoodInput food, double grams)
    {
        if (food.Fat <= 0 || food.Calories <= 0)
        {
            return null;
        }

        var unsaturated = 100 * (food.Fat - food.SaturatedFat) / food.Fat;
        var sodiumPercentDv = food.Sodium * (100 / food.Calories) / DensityScore.SodiumDv * 100;

        return ComponentValue.Measured(unsaturated - sodiumPercentDv);
    }
}
