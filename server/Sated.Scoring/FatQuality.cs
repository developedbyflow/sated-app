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

    // Where fat stops being an ingredient and starts being the food. Both ends were already
    // measured and already in this engine: 0.60 is the line that told nuts from cheese and bacon,
    // 0.80 the line that told olive oil from pecans. Between them the handover ramps rather than
    // switching, which is the whole point — every switch in this engine has produced two foods a
    // nutrient cannot tell apart, graded far apart because one fell on the far side of a boundary.
    //
    // Below 0.60 fat quality carries no weight at all, and that is not a rounding of the ramp. A
    // fried food's fat is unsaturated because it was fried in vegetable oil, so rewarding the
    // share there rewards the frying: measured, a linear ramp from zero lifted potato chips,
    // chicken nuggets, stuffed-crust pizza and granola from D/E to C and G0's bottom thirty fell
    // to 25 of 30. What a crisp is bad at is calories, and satiety already reads that.
    private const double FatBecomesTheFoodAt = 0.60;
    private const double FatIsTheWholeFoodAt = 1.00;

    /// <returns>
    /// How much of the satiety weight fat quality takes for this food, between 0 and 1. Zero for
    /// anything the Fullness Factor can still read.
    /// </returns>
    public static double ShareOfSatietyWeight(FoodInput food) =>
        Math.Clamp(
            (food.FatShareOfCalories - FatBecomesTheFoodAt)
                / (FatIsTheWholeFoodAt - FatBecomesTheFoodAt),
            0, 1);

    public static ComponentValue? UnsaturatedShare(FoodInput food)
    {
        if (food.Fat <= 0 || food.Calories <= 0)
        {
            return null;
        }

        var unsaturated = 100 * (food.Fat - food.SaturatedFat) / food.Fat;

        // Per 100 g, not per 100 kcal. Sodium harms by the gram eaten, and a denominator in
        // calories reads a fat as low-sodium precisely because it is fatty: measured, regular
        // mayonnaise carries 635 mg against olive oil's 2 and the per-calorie penalty separated
        // them by five points, 81 against 86. Per 100 g it is 56 against 84, which is the gap the
        // two foods actually have. It also stopped punishing the lighter version of a dressing for
        // having fewer calories to divide by — Thousand Island light and regular carry 955 and 962
        // mg, and the per-calorie denominator penalised the lighter one five times harder.
        var sodiumPercentDv = food.Sodium / DensityScore.SodiumDv * 100;

        // Clamped because the sodium penalty is unbounded: a fat-free dressing carries ~20 kcal and
        // enough sodium to push the penalty past 100, and ComponentStrategy promises 0-100. Found by
        // a generated food with fat that was entirely saturated; measured on the catalogue it is
        // real, and two foods came out with a negative combined score — Italian dressing, fat free
        // at -17.48. Costs nothing: 6 scores move, no letter does.
        return ComponentValue.Measured(Math.Clamp(unsaturated - sodiumPercentDv, 0, 100));
    }
}
