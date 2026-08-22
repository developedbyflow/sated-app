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

    public double? Satiety(FoodInput food, double grams) =>
        _satietyScale.Normalize(SatietyScore.Calculate(food.ForSatiety()));

    public double? Density(FoodInput food, double grams)
    {
        var raw = DensityScore.Calculate(food.ForDensity());

        if (raw is null)
        {
            return null;
        }

        return _densityScale.Normalize(raw.Value);
    }

    // No scale here: this one is already 0-100, being a percentage of the leucine threshold
    // rather than a raw quantity that needs a catalogue to be read against.
    public double? ProteinQuality(FoodInput food, double grams) =>
        ProteinQualityScore.Calculate(food.LeucinePer100g, grams);
}
