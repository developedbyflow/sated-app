namespace Sated.Scoring;

/// <summary>
/// The three components as computed when no category rule applies — the general formula (FR-6).
/// </summary>
public sealed class GeneralStrategies
{
    private readonly PercentileScale _satietyScale;
    private readonly PercentileScale _densityScale;

    public GeneralStrategies(PercentileScale satietyScale, PercentileScale densityScale)
    {
        _satietyScale = satietyScale;
        _densityScale = densityScale;
    }

    public ComponentValue? Satiety(FoodInput food, double grams) =>
        ComponentValue.Measured(
            _satietyScale.Normalize(SatietyScore.Calculate(food.ForSatiety())));

    public ComponentValue? Density(FoodInput food, double grams)
    {
        var raw = DensityScore.Calculate(food.ForDensity());

        if (raw is null)
        {
            return null;
        }

        return ComponentValue.Measured(_densityScale.Normalize(raw.Value));
    }

    // No scale here: this one is already 0-100, being a percentage of the leucine threshold
    // rather than a raw quantity that needs a catalogue to be read against.
    // The portion is ignored on purpose: a grade is read against the reference meal, so 200 g
    // of a food carries the same letter as 100 g of it. What a meal actually delivered is a
    // fact for the day's total, not part of a food's letter.
    public ComponentValue? ProteinQuality(FoodInput food, double grams)
    {
        var measured = ProteinQualityScore.Calculate(
            food.LeucinePer100g, ProteinQualityScore.ReferenceMealGrams);

        if (measured is not null)
        {
            return ComponentValue.Measured(measured.Value);
        }

        // FNDDS carries no amino acid data at all, so this is the path the entire catalogue
        // takes. Leaving the component empty instead would drop the one axis that separates
        // Fitness from Weight Loss, and the two lenses would land on the same letter for 87.6%
        // of the catalogue (P29). The estimate is marked as such: SM-C4 counts a guess that
        // reads as a measurement as a failure of the product, not a shortcut in the engine.
        var estimatedLeucine =
            ProteinCompleteness.EstimateLeucinePer100g(food.Protein, food.Category);

        return ComponentValue.Estimated(ProteinQualityScore.Calculate(
            estimatedLeucine, ProteinQualityScore.ReferenceMealGrams));
    }
}
