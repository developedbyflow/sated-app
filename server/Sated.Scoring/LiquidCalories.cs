namespace Sated.Scoring;

/// <summary>
/// The satiety of a drink that carries its calories as sugar water (FR-6). It is zero, and that
/// is the whole of the claim: liquid calories are poorly compensated, so a drink must not earn a
/// satiety score by being mostly water.
/// </summary>
public static class LiquidCalories
{
    // The Fullness Factor's first term is 41.7 / calories^0.7, which pays a food for carrying few
    // calories per 100 g. A soft drink is water with sugar in it, so it collects the whole of that
    // payment: measured, cola scores 91.0 on satiety where chicken breast scores 86.4, and satiety
    // carries half the Weight Loss lens. That is how cola came out C.
    // Keeping the formula and dropping only its calorie term was measured too: cola lands at 0.4
    // out of 100 and the energy drink at 0.6, so it buys nothing a constant does not, at the cost
    // of a strategy that would need the percentile scale a static one cannot reach.
    // Only drinks that carry calories take this rule. A diet drink has none to dilute, and its own
    // fault was the density denominator — see CalorieFloor in DensityScore (P42). See P43.

    public static ComponentValue? NoSatiety(FoodInput food) => ComponentValue.Measured(0);
}
