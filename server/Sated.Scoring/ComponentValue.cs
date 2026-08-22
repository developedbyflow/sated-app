namespace Sated.Scoring;

/// <summary>
/// One component's contribution to a grade: the score, and where the number came from.
/// </summary>
/// <param name="Score">Between 0 and 100.</param>
/// <param name="IsEstimated">
/// True when the number stands in for data the catalogue does not carry. SM-C4 counts hidden
/// guesses as a failure mode of the product, not a detail of the engine: a component that was
/// estimated must never read as one that was measured.
/// </param>
public record ComponentValue(double Score, bool IsEstimated)
{
    public static ComponentValue Measured(double score) => new(score, IsEstimated: false);

    public static ComponentValue Estimated(double score) => new(score, IsEstimated: true);
}
