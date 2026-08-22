namespace Sated.Scoring;

/// <summary>
/// Combines the three components into one 0-100 score under a Lens (FR-4).
/// </summary>
public sealed class ScoreCombiner
{
    private readonly ComponentStrategy _satiety;
    private readonly ComponentStrategy _density;
    private readonly ComponentStrategy _proteinQuality;

    /// <summary>The engine with the general formula for all three components.</summary>
    public ScoreCombiner(PercentileScale satietyScale, PercentileScale densityScale)
    {
        // The two scales were measured on one catalogue and belong to it: changing the food
        // source invalidates them. See 04_delivery/4.grade-distribution-report.
        var general = new GeneralStrategies(satietyScale, densityScale);

        _satiety = general.Satiety;
        _density = general.Density;
        _proteinQuality = general.ProteinQuality;
    }

    /// <summary>The engine with one or more components computed some other way (FR-6).</summary>
    public ScoreCombiner(
        ComponentStrategy satiety,
        ComponentStrategy density,
        ComponentStrategy proteinQuality)
    {
        _satiety = satiety;
        _density = density;
        _proteinQuality = proteinQuality;
    }

    /// <param name="grams">
    /// How much of the food is being scored. A catalogue entry uses the reference 100 g,
    /// a logged meal uses what was eaten.
    /// </param>
    /// <returns>A score between 0 and 100 under this lens, plus the components behind it.</returns>
    public CombinedScore Combine(FoodInput food, double grams, Lens lens)
    {
        // Every food that can be graded at all carries the four nutrients satiety needs, so this
        // is the one component guaranteed to be there. A strategy that returns nothing here would
        // leave a lens with no weight to divide by, and the score would come out NaN in silence.
        var satiety = _satiety(food, grams)
            ?? throw new InvalidOperationException(
                $"The satiety strategy returned nothing for a food in {food.Category}.");

        var density = _density(food, grams);
        var proteinQuality = _proteinQuality(food, grams);

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
