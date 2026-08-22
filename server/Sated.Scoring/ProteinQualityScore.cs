namespace Sated.Scoring;

public static class ProteinQualityScore
{
    // Muscle protein synthesis is triggered by 2.5-3 g of leucine in a single meal.
    // The response saturates: more leucine in the same meal does not trigger more synthesis,
    // so the score caps at the top of that range rather than rewarding excess.
    // FR-3 also asked for a completeness component. It is not here — USDA amino acid data
    // cannot support it per food, so completeness became a category rule (Story 1.8).

    private const double LeucineThresholdGrams = 3;

    /// <summary>
    /// The meal a grade is read against. The leucine threshold is defined per meal, but a
    /// catalogue entry is 100 g, so a grade asks "if the whole meal were this food, would it
    /// reach the threshold?" — never "how much did you eat?". That keeps the number per 100 g,
    /// so Food, Recipe, Meal and Day are graded on one scale.
    /// </summary>
    // Measured by tools/ProteinScaleQuery, see 04_delivery/7.protein-scale-report: against the
    // previous 100 g, the two lenses fall from the same letter on 55.3% of the catalogue to
    // 36.5%, watermelon reaches the A/C of Story 8.3, and cheddar stays out of A. 250 and 400
    // clear those too — 300 was picked because it is a plausible meal, and choosing on the
    // numbers alone would be fitting the catalogue. Cost: 24.7% of foods sit at the cap.
    public const double ReferenceMealGrams = 300;

    /// <summary>
    /// Calculates the Protein Quality Score from a food's leucine content and the amount eaten.
    /// </summary>
    /// <param name="leucinePer100g">Grams of leucine per 100 g of food.</param>
    /// <returns>A score between 0 and 100.</returns>
    public static double Calculate(double leucinePer100g, double grams)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(grams);

        var leucineEaten = leucinePer100g * grams / 100;

        return Math.Min(100, leucineEaten / LeucineThresholdGrams * 100);
    }

    /// <summary>
    /// The same score for a catalogue that may not carry leucine at all.
    /// </summary>
    /// <param name="leucinePer100g">Grams of leucine per 100 g, or null when unknown.</param>
    /// <returns>
    /// A score between 0 and 100, or null when leucine data is missing. Missing data is not a
    /// zero — it carries on to the partial-grade path of FR-7.
    /// </returns>
    public static double? Calculate(double? leucinePer100g, double grams)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(grams);

        if (leucinePer100g is null)
        {
            return null;
        }

        return Calculate(leucinePer100g.Value, grams);
    }
}
