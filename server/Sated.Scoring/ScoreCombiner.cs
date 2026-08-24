namespace Sated.Scoring;

/// <summary>
/// Combines the three components into one 0-100 score under a Lens (FR-4).
/// </summary>
public sealed class ScoreCombiner
{
    private readonly GeneralStrategies _general;
    private readonly CategoryRules _rules;

    /// <summary>The engine with the general formula everywhere and no category rules.</summary>
    public ScoreCombiner(
        PercentileScale satietyScale, PercentileScale densityScale, double referenceMealGrams)
        : this(new GeneralStrategies(satietyScale, densityScale, referenceMealGrams),
               CategoryRules.None)
    {
    }

    public ScoreCombiner(GeneralStrategies general, CategoryRules rules)
    {
        _general = general;
        _rules = rules;
    }

    /// <returns>A score between 0 and 100 under this lens, plus the components behind it.</returns>
    public CombinedScore Combine(FoodInput food, Lens lens)
    {
        // Every food that can be graded at all carries the four nutrients satiety needs, so this
        // is the one component guaranteed to be there. A strategy that returns nothing here would
        // leave a lens with no weight to divide by, and the score would come out NaN in silence.
        var satiety = Component(ScoreComponent.Satiety, _general.Satiety, food, lens)
            ?? throw new InvalidOperationException(
                $"The satiety strategy returned nothing for a food in {food.Category}.");

        var density = Component(
            ScoreComponent.Density, food => _general.Density(food, lens), food, lens);

        var proteinQuality = Component(
            ScoreComponent.ProteinQuality, _general.ProteinQuality, food, lens);

        // A missing component drops out of both sums. Dividing by the weight actually used,
        // instead of by 100, is the redistribution FR-7 asks for: a zero-calorie food has no
        // density, so satiety 50 and protein 20 end up counting 71.4% and 28.6%.
        // Only the general strategies can leave a component out now — a category rule that has
        // no answer hands the food back to them instead of removing the component.
        var weighted = lens.Satiety * satiety.Score;
        var usedWeight = lens.Satiety;

        if (density is not null)
        {
            weighted += lens.Density * density.Score;
            usedWeight += lens.Density;
        }

        if (proteinQuality is not null)
        {
            weighted += lens.ProteinQuality * proteinQuality.Score;
            usedWeight += lens.ProteinQuality;
        }

        return new CombinedScore(weighted / usedWeight, satiety, density, proteinQuality)
        {
            CategoryIsRuled = _rules.Has(food.Category, lens)
        };
    }

    private ComponentValue? Component(
        ScoreComponent component,
        ComponentStrategy general,
        FoodInput food,
        Lens lens)
    {
        var rule = _rules.Find(food.Category, lens, component);

        // A rule replaces a component, it never removes one. FatQuality has no answer for a food
        // with no fat, and fat-free mayonnaise is one: without this fallback that food would lose
        // its density and take a partial grade for data the catalogue actually carries. Dropping
        // a component is for data that is missing (FR-7), not for a strategy that does not apply.
        // On satiety the fallback is what keeps the guard above from firing on a real catalogue
        // food; the guard stays for a general strategy that could one day come back empty.
        if (rule is not null)
        {
            return rule(food) ?? general(food);
        }

        // Only for a food with no category at all. A category that exists but carries no rule has
        // been looked at by somebody — whipped cream and avocado are both in that state — and
        // guessing over their heads from the profile is what P50 measured and killed.
        if (food.Category is null)
        {
            var profile = component switch
            {
                ScoreComponent.Satiety => ProfileRules.Satiety(food),
                ScoreComponent.Density => ProfileRules.Density(food),
                _ => null
            };

            if (profile is not null)
            {
                return profile;
            }
        }

        return general(food);
    }
}
