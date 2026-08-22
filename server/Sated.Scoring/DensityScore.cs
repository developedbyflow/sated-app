namespace Sated.Scoring;

public static class DensityScore
{
    // NRF9.2 — Drewnowski 2009. Nine nutrients to encourage, two to limit.
    // Daily Values are the FDA 2016 labelling reference, for a 2000 kcal diet.
    // Unsaturated fats were measured and left out: the term tracks how much fat a food
    // carries, not how good it is — see 04_delivery/2.density-benchmark-report.

    private const double ProteinDv = 50;         // g
    private const double FiberDv = 28;           // g
    private const double VitaminADv = 900;       // µg RAE
    private const double VitaminCDv = 90;        // mg
    private const double VitaminEDv = 15;        // mg
    private const double CalciumDv = 1300;       // mg
    private const double IronDv = 18;            // mg
    private const double MagnesiumDv = 420;      // mg
    private const double PotassiumDv = 4700;     // mg
    private const double SaturatedFatDv = 20;    // g
    internal const double SodiumDv = 2300;       // mg — shared with FatQuality

    /// <summary>
    /// Calculates the Density Score from a food's per-100g nutrient values.
    /// </summary>
    /// <returns>
    /// A raw NRF9.2 score per 100 kcal. Higher means more nutrients per calorie.
    /// Null when the food has no calories: "nutrients per calorie" has no answer there,
    /// and the missing component carries on to the partial-grade path of FR-7.
    /// Not normalised — the 0-100 range comes later, from measured percentiles.
    /// </returns>
    public static double? Calculate(DensityInput food)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(food.Calories);

        // FNDDS reports energy in whole kcal — all 5,431 values, none between 0 and 1 —
        // so this equality is exact, not the usual floating-point trap.
        if (food.Calories == 0)
        {
            return null;
        }

        var scaleTo100Kcal = 100 / food.Calories;

        var encouraged =
              CappedPercentDv(food.Protein * scaleTo100Kcal, ProteinDv)
            + CappedPercentDv(food.Fiber * scaleTo100Kcal, FiberDv)
            + CappedPercentDv(food.VitaminA * scaleTo100Kcal, VitaminADv)
            + CappedPercentDv(food.VitaminC * scaleTo100Kcal, VitaminCDv)
            + CappedPercentDv(food.VitaminE * scaleTo100Kcal, VitaminEDv)
            + CappedPercentDv(food.Calcium * scaleTo100Kcal, CalciumDv)
            + CappedPercentDv(food.Iron * scaleTo100Kcal, IronDv)
            + CappedPercentDv(food.Magnesium * scaleTo100Kcal, MagnesiumDv)
            + CappedPercentDv(food.Potassium * scaleTo100Kcal, PotassiumDv);

        var limited =
              PercentDv(food.SaturatedFat * scaleTo100Kcal, SaturatedFatDv)
            + PercentDv(food.Sodium * scaleTo100Kcal, SodiumDv);

        return encouraged - limited;
    }

    private static double PercentDv(double amount, double dailyValue) =>
        amount / dailyValue * 100;

    private static double CappedPercentDv(double amount, double dailyValue) =>
        Math.Min(100, PercentDv(amount, dailyValue));
}