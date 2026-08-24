namespace Sated.Scoring;

/// <summary>
/// The four score cutoffs that turn a combined score into a letter, for one Lens (FR-5).
/// </summary>
public sealed class GradeThresholds
{
    // The cutoffs themselves are measured, frozen (P28) and read from calibration.json, together
    // with the lens they belong to — a food's letter must not change because other foods joined
    // the catalogue, and the file carries which catalogue and when. This class only decides which
    // letter a score falls into.

    public GradeThresholds(double dStartsAt, double cStartsAt, double bStartsAt, double aStartsAt)
    {
        // A cutoff at or below zero leaves E unreachable, one above 100 leaves A unreachable,
        // and two equal cutoffs erase the letter between them. Each of those is a rung of the
        // scale no food can ever land on — a broken calibration, not a strict one.
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dStartsAt);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(aStartsAt, 100);

        if (dStartsAt >= cStartsAt || cStartsAt >= bStartsAt || bStartsAt >= aStartsAt)
        {
            throw new ArgumentException(
                $"Cutoffs must increase, but they read " +
                $"{dStartsAt}, {cStartsAt}, {bStartsAt}, {aStartsAt}.",
                nameof(dStartsAt));
        }

        DStartsAt = dStartsAt;
        CStartsAt = cStartsAt;
        BStartsAt = bStartsAt;
        AStartsAt = aStartsAt;
    }

    // Readable because four measurement tools had copied them out by hand rather than ask.
    // A hand-copied cutoff goes stale in silence the first time the calibration is refitted.
    public double DStartsAt { get; }
    public double CStartsAt { get; }
    public double BStartsAt { get; }
    public double AStartsAt { get; }

    /// <summary>
    /// The letter a score earns from the cutoffs alone, with no density floor applied.
    /// Not the grade a food gets: that is <see cref="Calibration.GradeFor"/>, and bacon differs
    /// between the two. This exists for tools measuring the raw scale, and the name says so
    /// because the two used to be one call apart.
    /// </summary>
    /// <param name="score">A combined score between 0 and 100.</param>
    public Grade GradeForScoreAlone(double score) =>
        score < DStartsAt ? Grade.E
        : score < CStartsAt ? Grade.D
        : score < BStartsAt ? Grade.C
        : score < AStartsAt ? Grade.B
        : Grade.A;
}
