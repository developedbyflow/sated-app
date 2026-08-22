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
    ComponentValue? ProteinQuality
)
{
    /// <summary>True when fewer than three components went into the score (FR-7).</summary>
    public bool IsPartial => Density is null || ProteinQuality is null;

    /// <summary>True when any component that did count was estimated rather than measured.</summary>
    public bool HasEstimatedComponents =>
        Satiety.IsEstimated
        || Density?.IsEstimated == true
        || ProteinQuality?.IsEstimated == true;
}
