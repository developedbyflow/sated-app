namespace Sated.Scoring;

/// <summary>
/// How good a food's fat is, standing in for whichever component cannot tell such foods apart
/// (FR-6). For the fat categories that is satiety: the Fullness Factor floor catches everything
/// that is almost entirely fat, so olive oil and butter both score zero on it. For nuts it is
/// density: NRF9.2 counts nutrients per calorie and cannot see that the calories are unsaturated.
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

    public static ComponentValue? UnsaturatedShare(FoodInput food)
    {
        if (food.Fat <= 0 || food.Calories <= 0)
        {
            return null;
        }

        var unsaturated = 100 * (food.Fat - food.SaturatedFat) / food.Fat;
        var sodiumPercentDv = food.Sodium * (100 / food.Calories) / DensityScore.SodiumDv * 100;

        // Clamped because the sodium penalty is unbounded: a fat-free dressing carries ~20 kcal and
        // enough sodium to push the penalty past 100, and ComponentStrategy promises 0-100. Found by
        // a generated food with fat that was entirely saturated; measured on the catalogue it is
        // real, and two foods came out with a negative combined score — Italian dressing, fat free
        // at -17.48. Costs nothing: 6 scores move, no letter does.
        return ComponentValue.Measured(Math.Clamp(unsaturated - sodiumPercentDv, 0, 100));
    }
}
