namespace Sated.Scoring;

/// <summary>
/// The 0-100 score of a food under one Lens, with the components behind it.
/// A null component was unavailable and did not contribute — its weight went to the others.
/// Not a Grade yet: the letter comes from Story 1.7.
/// </summary>
public record CombinedScore(
    double Value,
    ComponentValue Satiety,
    ComponentValue? Density,
    ComponentValue? ProteinQuality,
    ComponentValue? FatQuality = null
)
{
    /// <summary>True when fewer than three components went into the score (FR-7).</summary>
    public bool IsPartial => Density is null || ProteinQuality is null;

    /// <summary>
    /// True when the food carries neither energy nor any macronutrient. Every score in this engine
    /// is a quantity per calorie, so for such a food there is nothing to divide by and nothing to
    /// divide: the question the grade answers was never asked of it.
    /// Set by the combiner, which is the only place that still has the food.
    /// </summary>
    /// <remarks>
    /// It is not a low grade. Graded, these foods sort against a scale built for foods that have
    /// calories, and the catalogue audit measured what that produces: 89 such foods spread across
    /// four letters and 77.7 points, with diet Kool-Aid at A 76.1 and Powerade Zero at E 0.0, and
    /// tap water inverting against every drink it is nutritionally better than. Any single letter
    /// is a claim — A says eat this, E says avoid it — and for water all of them are false.
    /// </remarks>
    public bool IsNutritionallyEmpty { get; init; }

    /// <summary>
    /// True when this food's category carries a rule under the lens it was scored with.
    /// It exempts the food from the density floor (P44): a category with a rule has already been
    /// judged by hand, and a blanket floor must not overrule that. Measured: without the exemption
    /// flaxseed oil falls from C to E, which is the olive oil mistake of Story 1.8 all over again.
    /// </summary>
    public bool CategoryIsRuled { get; init; }

    /// <summary>True when any component that did count was estimated rather than measured.</summary>
    public bool HasEstimatedComponents =>
        Satiety.IsEstimated
        || Density?.IsEstimated == true
        || ProteinQuality?.IsEstimated == true
        || FatQuality?.IsEstimated == true;
}
