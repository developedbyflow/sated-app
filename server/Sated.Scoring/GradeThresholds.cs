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

    private readonly double _dStartsAt;
    private readonly double _cStartsAt;
    private readonly double _bStartsAt;
    private readonly double _aStartsAt;

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

        _dStartsAt = dStartsAt;
        _cStartsAt = cStartsAt;
        _bStartsAt = bStartsAt;
        _aStartsAt = aStartsAt;
    }

    /// <param name="score">A combined score between 0 and 100.</param>
    public Grade GradeFor(double score) =>
        score < _dStartsAt ? Grade.E
        : score < _cStartsAt ? Grade.D
        : score < _bStartsAt ? Grade.C
        : score < _aStartsAt ? Grade.B
        : Grade.A;
}
