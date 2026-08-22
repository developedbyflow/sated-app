namespace Sated.Scoring;

/// <summary>
/// Combines the three components into one 0-100 score under a Lens (FR-4).
/// </summary>
public sealed class ScoreCombiner
{
    private readonly GeneralStrategies _general;
    private readonly CategoryRules _rules;

    /// <summary>The engine with the general formula everywhere and no category rules.</summary>
    public ScoreCombiner(PercentileScale satietyScale, PercentileScale densityScale)
        : this(new GeneralStrategies(satietyScale, densityScale), CategoryRules.None)
    {
    }

    public ScoreCombiner(GeneralStrategies general, CategoryRules rules)
    {
        _general = general;
        _rules = rules;
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
        var satiety = Component(ScoreComponent.Satiety, _general.Satiety, food, grams, lens)
            ?? throw new InvalidOperationException(
                $"The satiety strategy returned nothing for a food in {food.Category}.");

        var density = Component(ScoreComponent.Density, _general.Density, food, grams, lens);

        var proteinQuality = Component(
            ScoreComponent.ProteinQuality, _general.ProteinQuality, food, grams, lens);

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

    private double? Component(
        ScoreComponent component,
        ComponentStrategy general,
        FoodInput food,
        double grams,
        Lens lens)
    {
        var strategy = _rules.Find(food.Category, lens, component) ?? general;

        return strategy(food, grams);
    }
}
