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
    private const double VitaminDDv = 20;        // µg — FDA 2016, same reference as the rest
    private const double ThiamineDv = 1.2;       // mg

    // Density is stated per 100 kcal, so the scale factor is 100 / calories. Below 10 kcal that
    // factor passes 10 and trace amounts become a full score: diet cola carries 2 kcal and scored
    // 85 out of 100 on sodium and iron residue alone, which graded it A. Satiety has had the same
    // guard since Story 1.3; density never got one. Measured over FNDDS: 70 foods of 5,403 sit
    // below this floor, and the 41 vegetables under 40 kcal are untouched. See P42.
    internal const double CalorieFloor = 10;

    /// <summary>NRF9.2 as Drewnowski published it: the set every lens uses unless it says otherwise.</summary>
    public static readonly DensityNutrients Nrf92 = new(
        "nrf9.2",
        Encouraged:
        [
            new(food => food.Protein, ProteinDv),
            new(food => food.Fiber, FiberDv),
            new(food => food.VitaminA, VitaminADv),
            new(food => food.VitaminC, VitaminCDv),
            new(food => food.VitaminE, VitaminEDv),
            new(food => food.Calcium, CalciumDv),
            new(food => food.Iron, IronDv),
            new(food => food.Magnesium, MagnesiumDv),
            new(food => food.Potassium, PotassiumDv)
        ],
        Limited:
        [
            new(food => food.SaturatedFat, SaturatedFatDv),
            new(food => food.Sodium, SodiumDv)
        ]);

    // Written as NRF9.2 plus two rather than as eleven nutrients copied out, because that is the
    // claim: the GLP-1 lens does not disagree with NRF9.2 about anything, it counts two more
    // things. A second hand-written list would let the two drift apart silently.
    /// <summary>NRF9.2 with the two nutrients GLP-1 treatment depletes (FR-26).</summary>
    public static readonly DensityNutrients Nrf112 = new(
        "nrf11.2",
        Encouraged:
        [
            .. Nrf92.Encouraged,
            new DensityNutrient(food => food.VitaminD, VitaminDDv),
            new DensityNutrient(food => food.Thiamine, ThiamineDv)
        ],
        Limited: Nrf92.Limited);

    /// <summary>
    /// Calculates the Density Score from a food's per-100g nutrient values.
    /// </summary>
    /// <returns>
    /// A raw NRF9.2 score per 100 kcal. Higher means more nutrients per calorie.
    /// Null when the food has no calories: "nutrients per calorie" has no answer there,
    /// and the missing component carries on to the partial-grade path of FR-7.
    /// Not normalised — the 0-100 range comes later, from measured percentiles.
    /// </returns>
    public static double? Calculate(DensityInput food) => Calculate(food, Nrf92);

    /// <returns>A raw score per 100 kcal under the given nutrient set. See the overload above.</returns>
    public static double? Calculate(DensityInput food, DensityNutrients nutrients)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(food.Calories);

        // FNDDS reports energy in whole kcal — all 5,431 values, none between 0 and 1 —
        // so this equality is exact, not the usual floating-point trap.
        if (food.Calories == 0)
        {
            return null;
        }

        var scaleTo100Kcal = 100 / Math.Max(CalorieFloor, food.Calories);

        // Accumulated in list order rather than with Sum, so the nine additions happen in exactly
        // the order they did when the shipped percentiles were measured. Floating-point addition
        // does not associate, and a re-ordered sum would move the scale under every food.
        var encouraged = 0.0;
        var counted = 0;

        foreach (var nutrient in nutrients.Encouraged)
        {
            var amount = nutrient.AmountPer100g(food);

            if (amount is null)
            {
                continue;
            }

            encouraged += CappedPercentDv(amount.Value * scaleTo100Kcal, nutrient.DailyValue);
            counted++;
        }

        if (counted == 0)
        {
            return null;
        }

        // The nutrients nobody supplied are assumed to behave like the ones somebody did. That is
        // an estimate and IsComplete says so, but it is the only honest one available: counting
        // them as zero is a claim the food contains none, and measured on the gate's 68 foods that
        // claim costs exactly one letter on twenty of them. Rescaling recovers thirteen.
        encouraged *= (double)nutrients.Encouraged.Count / counted;

        var limited = 0.0;

        foreach (var nutrient in nutrients.Limited)
        {
            // Never absent: the two limiters are non-nullable on DensityInput, on purpose. Skipping
            // one would raise a food's score, and that is the direction a missing value must never
            // move it in.
            limited += PercentDv(
                nutrient.AmountPer100g(food)!.Value * scaleTo100Kcal, nutrient.DailyValue);
        }

        return encouraged - limited;
    }

    /// <summary>
    /// True when the food carries every nutrient this set counts. False means the score above was
    /// rescaled over the ones it had, and the component must be reported as an estimate.
    /// </summary>
    public static bool IsComplete(DensityInput food, DensityNutrients nutrients) =>
        nutrients.Encouraged.All(nutrient => nutrient.AmountPer100g(food) is not null);

    private static double PercentDv(double amount, double dailyValue) =>
        amount / dailyValue * 100;

    private static double CappedPercentDv(double amount, double dailyValue) =>
        Math.Min(100, PercentDv(amount, dailyValue));
}