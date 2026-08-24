namespace Sated.Scoring;

/// <summary>
/// A goal profile: how much each of the three components counts towards a Grade.
/// Weights are percentages and add up to 100.
/// </summary>
public sealed record Lens
{
    private const double TotalWeight = 100;

    // Which lenses exist is a calibration question, not a code one: they are read from
    // calibration.json (Story 1.12). The file ships all three FR-26 asks for. GLP-1 carries the
    // Weight Loss weighting, because what defines it is DensityNutrients below and not these
    // three numbers: measured, the nutrient set alone gives a different letter to 10.0% of the
    // catalogue. That is a statement in the file, with its measurement, rather than a weighting
    // nobody agreed on presented as one somebody did.

    public Lens(string name, double satiety, double density, double proteinQuality,
        DensityNutrients? densityNutrients = null)
    {
        // Strictly positive, not merely non-negative. Satiety is the only component that can
        // never be missing, so a zero weight on it plus two unavailable components would leave
        // the combiner dividing by a total weight of zero — a silent NaN instead of a score.
        // A component a lens does not want is not a zero weight either: it would still be
        // reported in the breakdown as if it had counted.
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(satiety);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(density);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(proteinQuality);

        var total = satiety + density + proteinQuality;

        // A tenth of a point of slack, so three-way splits written as 33.3/33.3/33.4 load.
        if (Math.Abs(total - TotalWeight) > 0.1)
        {
            throw new ArgumentException(
                $"Weights must add up to {TotalWeight}, but {name} adds up to {total}.",
                nameof(name));
        }

        Name = name;
        Satiety = satiety;
        Density = density;
        ProteinQuality = proteinQuality;

        // Optional so a lens written in a test stays three numbers. The file is not allowed the
        // same silence: LensFile makes the name required, and Calibration refuses one it cannot
        // compute — a lens that quietly took the default is how GLP-1 would ship graded wrong.
        DensityNutrients = densityNutrients ?? DensityScore.Nrf92;
    }

    public string Name { get; }
    public double Satiety { get; }
    public double Density { get; }
    public double ProteinQuality { get; }

    /// <summary>Which nutrients this lens counts in density (FR-26).</summary>
    public DensityNutrients DensityNutrients { get; }
}
