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

        var fatQuality = Component(
            ScoreComponent.FatQuality, _general.FatQuality, food, lens);

        // Fat quality carries no weight of its own. It takes satiety's, in proportion to how much
        // of the food's energy is fat — which is the thing the Fullness Factor cannot read. Olive
        // oil hands over all of it and is graded on its fat; broccoli hands over almost none.
        // Written as a share rather than as a fourth weight in calibration.json on purpose: a new
        // weight would be a number nobody measured, and the handoff already names the lens weights
        // as the engine's biggest unvalidated lever. This adds no fourth one.
        // It is also why no cliff can come back. The category rule this replaces was a switch, and
        // every switch in this engine has produced the same defect: two foods a nutrient cannot
        // tell apart, graded far apart because one fell on the far side of a boundary.
        var handover = FatQuality.ShareOfSatietyWeight(food);
        var satietyWeight = lens.Satiety * (1 - handover);
        var fatWeight = lens.Satiety * handover;

        // A missing component drops out of both sums. Dividing by the weight actually used,
        // instead of by 100, is the redistribution FR-7 asks for: a zero-calorie food has no
        // density, so satiety 50 and protein 20 end up counting 71.4% and 28.6%.
        // Only the general strategies can leave a component out now — a category rule that has
        // no answer hands the food back to them instead of removing the component.
        var weighted = satietyWeight * satiety.Score;
        var usedWeight = satietyWeight;

        if (fatQuality is not null && fatWeight > 0)
        {
            weighted += fatWeight * fatQuality.Score;
            usedWeight += fatWeight;
        }

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

        // Every food that carries fat enough to zero the satiety weight carries a fat quality to
        // put in its place, so this cannot divide by zero. Stated rather than assumed: the two
        // facts live in different files and a change to either would otherwise produce NaN.
        if (usedWeight <= 0)
        {
            throw new InvalidOperationException(
                $"No component carried any weight for a food in {food.Category}.");
        }

        return new CombinedScore(weighted / usedWeight, satiety, density, proteinQuality, fatQuality)
        {
            // A profile rule exempts a food from the density floor exactly as a category rule does.
            // Replacing a component by one route and being floored anyway by the other would grade
            // a hand-entered olive oil on a rule and then overrule it, which is the whole point of
            // the exemption at P44.
            IsNutritionallyEmpty = ProfileRules.IsNutritionallyEmpty(food),
            CategoryIsRuled = _rules.Has(food.Category, lens)
                || (IsUnrecognised(food) && ProfileRules.Judges(food))
        };
    }

    // A Recipe or a Meal lands here too: PortionAggregate gives a plate a category that belongs to
    // no catalogue, on purpose, so a plate that is nine tenths oil by calories is judged as the fat
    // it is rather than by a formula written for foods.
    private bool IsUnrecognised(FoodInput food) =>
        food.Category is null || !_rules.Recognises(food.Category);

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

        // For a food this catalogue does not recognise: no category at all, or a category from
        // somewhere else. A category the catalogue does know but nobody ruled has been looked at —
        // whipped cream and avocado are both in that state — and guessing over their heads from
        // the profile is what P50 measured and killed.
        if (IsUnrecognised(food))
        {
            var profile = component switch
            {
                ScoreComponent.Satiety => ProfileRules.Satiety(food),
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
