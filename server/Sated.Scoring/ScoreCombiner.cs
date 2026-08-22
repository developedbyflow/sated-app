namespace Sated.Scoring;

/// <summary>
/// Combines the three raw scores into one 0-100 score under a Lens (FR-4).
/// </summary>
public sealed class ScoreCombiner
{
    // The two scales were measured on one catalogue and belong to it: changing the food source
    // invalidates them. They are constructor arguments so the pairing cannot drift silently.
    // See 04_delivery/4.grade-distribution-report.

    private readonly PercentileScale _satietyScale;
    private readonly PercentileScale _densityScale;

    public ScoreCombiner(PercentileScale satietyScale, PercentileScale densityScale)
    {
        _satietyScale = satietyScale;
        _densityScale = densityScale;
    }

    /// <param name="grams">
    /// How much of the food is being scored. A catalogue entry uses the reference 100 g,
    /// a logged meal uses what was eaten. Only Protein Quality reads it — the other two
    /// components are per 100 g and per 100 kcal by construction.
    /// </param>
    /// <returns>A score between 0 and 100 under this lens, plus the components behind it.</returns>
    public CombinedScore Combine(FoodInput food, double grams, Lens lens)
    {
        var satiety = _satietyScale.Normalize(SatietyScore.Calculate(food.ForSatiety()));

        double? density = null;
        var rawDensity = DensityScore.Calculate(food.ForDensity());

        if (rawDensity is not null)
        {
            density = _densityScale.Normalize(rawDensity.Value);
        }

        // No scale here: this one is already 0-100, being a percentage of the leucine
        // threshold rather than a raw quantity that needs a catalogue to be read against.
        var proteinQuality = ProteinQualityScore.Calculate(food.LeucinePer100g, grams);

        // A missing component drops out of both sums. Dividing by the weight actually used,
        // instead of by 100, is the redistribution FR-7 asks for: with satiety 50 and
        // density 30 left, the two end up counting 62.5% and 37.5%.
        var weighted = lens.Satiety * satiety;
        var usedWeight = lens.Satiety;

        if (density is not null)
        {
            weighted += lens.Density * density.Value;
            usedWeight += lens.Density;
        }

        if (proteinQuality is not null)
        {
            weighted += lens.ProteinQuality * proteinQuality.Value;
            usedWeight += lens.ProteinQuality;
        }

        return new CombinedScore(weighted / usedWeight, satiety, density, proteinQuality);
    }
}
