namespace Sated.Scoring;

/// <summary>
/// A goal profile: how much each of the three components counts towards a Grade.
/// Weights are percentages and add up to 100.
/// </summary>
public sealed record Lens
{
    private const double TotalWeight = 100;

    // Two lenses, not three. FR-26 leaves the GLP-1 weights undecided, and inventing them
    // would put a number nobody agreed on in front of users.
    // Architecture §Butoane de reglaj moves these to a versioned JSON file; until that story
    // exists they live here, so the engine can be exercised without loading one.

    public static Lens WeightLoss { get; } =
        new("Weight Loss", satiety: 50, density: 30, proteinQuality: 20);

    public static Lens Fitness { get; } =
        new("Fitness", satiety: 25, density: 25, proteinQuality: 50);

    public Lens(string name, double satiety, double density, double proteinQuality)
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
    }

    public string Name { get; }
    public double Satiety { get; }
    public double Density { get; }
    public double ProteinQuality { get; }
}
