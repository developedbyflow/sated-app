namespace Sated.Scoring;

/// <summary>
/// The three components as computed when no category rule applies — the general formula (FR-6).
/// </summary>
public sealed class GeneralStrategies
{
    private readonly PercentileScale _satietyScale;
    private readonly IReadOnlyDictionary<string, PercentileScale> _densityScales;
    private readonly double _referenceMealGrams;

    /// <summary>One nutrient set, one scale — the engine before FR-26 needed a third lens.</summary>
    public GeneralStrategies(
        PercentileScale satietyScale, PercentileScale densityScale, double referenceMealGrams)
        : this(satietyScale,
               new Dictionary<string, PercentileScale>(StringComparer.OrdinalIgnoreCase)
               {
                   [DensityScore.Nrf92.Name] = densityScale
               },
               referenceMealGrams)
    {
    }

    public GeneralStrategies(
        PercentileScale satietyScale,
        IReadOnlyDictionary<string, PercentileScale> densityScales,
        double referenceMealGrams)
    {
        _satietyScale = satietyScale;
        _densityScales = densityScales;
        _referenceMealGrams = referenceMealGrams;
    }

    public ComponentValue? Satiety(FoodInput food) =>
        ComponentValue.Measured(
            _satietyScale.Normalize(SatietyScore.Calculate(food.ForSatiety())));

    // Asked of every food, not of five categories. As a replacement for satiety it was a switch:
    // a food inside a fat category was lifted to the unsaturated share and a food outside it kept
    // the general formula, so two foods a nutrient could not tell apart were graded thirty points
    // apart. The catalogue audit counted 725 pairs where a food better on every number the engine
    // reads scored lower. As a component it has no inside and no outside.
    public ComponentValue? FatQuality(FoodInput food) =>
        Sated.Scoring.FatQuality.UnsaturatedShare(food);

    public ComponentValue? Density(FoodInput food, Lens lens)
    {
        var nutrients = lens.DensityNutrients;
        var raw = DensityScore.Calculate(food.ForDensity(), nutrients);

        if (raw is null)
        {
            return null;
        }

        // A missing scale is not a food with no density: it is a lens asking to be ranked against
        // a distribution nobody measured. Falling back to another set's ranks would grade every
        // food on this lens against a formula it was not computed with, and nothing would say so.
        if (!_densityScales.TryGetValue(nutrients.Name, out var scale))
        {
            throw new InvalidOperationException(
                $"The {lens.Name} lens asks for the {nutrients.Name} scale, and only " +
                $"{string.Join(", ", _densityScales.Keys)} was measured.");
        }

        var score = scale.Normalize(raw.Value);

        // Below the calorie floor the number was divided by 10 rather than by the food's own
        // calories, so it describes a food that does not exist. Keeping it still beats dropping
        // the component — a diet cola with no density would be graded on satiety alone and come
        // out B, the letter tap water gets — but it is not a measurement, and P44's floor must
        // not sentence a food to E on a number nobody measured. See P49.
        // Two separate reasons the number is not a measurement. Below the calorie floor it
        // describes a food that does not exist; incomplete, it was rescaled over the nutrients the
        // food happened to carry. Either way SM-C4 says it must not read as measured.
        return food.Calories < DensityScore.CalorieFloor
            || !DensityScore.IsComplete(food.ForDensity(), nutrients)
            ? ComponentValue.Estimated(score)
            : ComponentValue.Measured(score);
    }

    // No scale here: this one is already 0-100, being a percentage of the leucine threshold
    // rather than a raw quantity that needs a catalogue to be read against.
    // The portion is ignored on purpose: a grade is read against the reference meal, so 200 g
    // of a food carries the same letter as 100 g of it. What a meal actually delivered is a
    // fact for the day's total, not part of a food's letter.
    public ComponentValue? ProteinQuality(FoodInput food)
    {
        var measured = ProteinQualityScore.Calculate(
            food.LeucinePer100g, _referenceMealGrams);

        if (measured is not null)
        {
            return food.LeucineIsEstimated
                ? ComponentValue.Estimated(measured.Value)
                : ComponentValue.Measured(measured.Value);
        }

        // FNDDS carries no amino acid data at all, so this is the path the entire catalogue
        // takes. Leaving the component empty instead would drop the one axis that separates
        // Fitness from Weight Loss, and the two lenses would land on the same letter for 87.6%
        // of the catalogue (P29). The estimate is marked as such: SM-C4 counts a guess that
        // reads as a measurement as a failure of the product, not a shortcut in the engine.
        var estimatedLeucine = ProteinCompleteness.EstimateLeucinePer100g(food.Protein, food.Category);

        return ComponentValue.Estimated(ProteinQualityScore.Calculate(
            estimatedLeucine, _referenceMealGrams));
    }
}
