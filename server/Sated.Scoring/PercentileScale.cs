namespace Sated.Scoring;

/// <summary>
/// Maps a raw score onto 0-100 by where it falls in a measured catalogue, rather than
/// between two chosen ends. A food's normalised score is its position in that catalogue.
/// </summary>
public sealed class PercentileScale
{
    // Breakpoints are measured, never chosen: tools/GradeDistributionQuery writes them from
    // the whole catalogue. A linear range cannot do this job — density runs from -884 to +536
    // on FNDDS, with an upper tail 5.4 times the lower one, so any pair of ends either clips
    // the extremes or crushes the bulk of the catalogue into the bottom fifth of the scale.
    // See 04_delivery/4.grade-distribution-report.

    private readonly double[] _breakpoints;

    /// <param name="breakpoints">
    /// Raw score values in non-decreasing order, evenly spaced by position: the first is
    /// the catalogue minimum, the last its maximum.
    /// </param>
    public PercentileScale(double[] breakpoints)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(breakpoints.Length, 2);

        for (var index = 1; index < breakpoints.Length; index++)
        {
            if (breakpoints[index] < breakpoints[index - 1])
            {
                throw new ArgumentException(
                    $"Breakpoints must not decrease, but [{index}] is {breakpoints[index]} " +
                    $"after {breakpoints[index - 1]}.",
                    nameof(breakpoints));
            }
        }

        _breakpoints = [.. breakpoints];
    }

    /// <returns>
    /// A score between 0 and 100. Higher means the food beats more of the catalogue.
    /// </returns>
    public double Normalize(double raw)
    {
        var step = 100.0 / (_breakpoints.Length - 1);

        if (raw <= _breakpoints[0])
        {
            return 0;
        }

        if (raw >= _breakpoints[^1])
        {
            return 100;
        }

        var above = 1;
        while (_breakpoints[above] < raw)
        {
            above++;
        }

        var below = above - 1;
        var width = _breakpoints[above] - _breakpoints[below];

        return (below + (raw - _breakpoints[below]) / width) * step;
    }
}
