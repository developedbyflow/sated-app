namespace Sated.Scoring;

/// <summary>
/// The four score cutoffs that turn a combined score into a letter, for one Lens (FR-5).
/// </summary>
public sealed class GradeThresholds
{
    // The 20th, 40th, 60th and 80th percentiles of the combined score, measured once on
    // FNDDS 2021-2023 by tools/LetterThresholdQuery and then frozen (P28): a food's letter must
    // not change because other foods joined the catalogue. Provisional in the same way as the
    // percentile breakpoints of Story 1.6 — both are tied to the catalogue they were read from.
    // Architecture §Butoane de reglaj moves these to a versioned JSON file, together with the
    // lens weights and the breakpoints; until that story exists, they live here.
    // See 04_delivery/5.letter-threshold-report.

    public static GradeThresholds WeightLoss { get; } =
        new(dStartsAt: 25.56, cStartsAt: 41.20, bStartsAt: 57.70, aStartsAt: 76.77);

    public static GradeThresholds Fitness { get; } =
        new(dStartsAt: 26.01, cStartsAt: 41.64, bStartsAt: 56.16, aStartsAt: 76.82);

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

    /// <summary>
    /// The thresholds calibrated for a lens. Throws for a lens nobody has calibrated, rather
    /// than falling back to another lens's numbers and grading everything slightly wrong.
    /// </summary>
    public static GradeThresholds For(Lens lens) => lens.Name switch
    {
        "Weight Loss" => WeightLoss,
        "Fitness" => Fitness,
        _ => throw new ArgumentException(
            $"No calibrated thresholds for the {lens.Name} lens.", nameof(lens))
    };

    /// <param name="score">A combined score between 0 and 100.</param>
    public Grade GradeFor(double score) =>
        score < _dStartsAt ? Grade.E
        : score < _cStartsAt ? Grade.D
        : score < _bStartsAt ? Grade.C
        : score < _aStartsAt ? Grade.B
        : Grade.A;
}
