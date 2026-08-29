using System.Text.Json;

namespace Sated.Scoring;

/// <summary>
/// The five calibration tables, read from calibration.json instead of compiled in (Story 1.12):
/// the lens weightings, their letter cutoffs, the measured percentile breakpoints, the category
/// dispatch table, and the reference meal the protein component is measured against.
/// </summary>
public sealed class Calibration
{
    // A file holds names, never code. A name that is not in this table is one the engine cannot
    // honour, so loading throws: a silently dropped rule would grade olive oil by the general
    // formula and report nothing.
    private static readonly Dictionary<string, ComponentStrategy> KnownStrategies =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["unsaturatedShare"] = FatQuality.UnsaturatedShare,
            ["noSatiety"] = LiquidCalories.NoSatiety
        };

    // The same table for density: a lens is not three weights. SATED.md defines the GLP-1 lens
    // by what its density counts, not by how it weighs the three components. Measured before the
    // set existed: a lens named GLP-1 with weights and cutoffs loaded and graded all 5,431 foods
    // with the ordinary formula, in silence. Naming the set makes that impossible, and a set with
    // no measured percentiles is refused later, by GeneralStrategies.
    private static readonly Dictionary<string, DensityNutrients> KnownNutrientSets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [DensityScore.Nrf92.Name] = DensityScore.Nrf92,
            [DensityScore.Nrf112.Name] = DensityScore.Nrf112
        };

    private static readonly JsonSerializerOptions Format = new(JsonSerializerDefaults.Web);

    /// <summary>Reads calibration.json from the folder the binary runs in.</summary>
    // Not a path relative to the working directory: the harness starts inside its own project
    // folder and the API elsewhere, so a relative path would resolve differently depending on
    // where dotnet was typed.
    public static Calibration Load() =>
        Load(Path.Combine(AppContext.BaseDirectory, "calibration.json"));

    public static Calibration Load(string path)
    {
        var file = JsonSerializer.Deserialize<CalibrationFile>(File.ReadAllText(path), Format)
            ?? throw new ArgumentException($"{path} holds no calibration.", nameof(path));

        return new Calibration(file);
    }

    private readonly Dictionary<string, GradeThresholds> _thresholds =
        new(StringComparer.OrdinalIgnoreCase);

    private Calibration(CalibrationFile file)
    {
        Catalogue = file.Catalogue;
        MeasuredOn = file.MeasuredOn;
        Notes = file.Notes;
        ReferenceMealGrams = file.ReferenceMealGrams;
        DensityFloor = file.DensityFloor;

        Lenses = [.. file.Lenses.Select(lens => new Lens(
            lens.Id, lens.Name, lens.Satiety, lens.Density, lens.ProteinQuality,
            NutrientsNamed(lens.DensityNutrients)))];

        foreach (var lens in file.Lenses)
        {
            // Add, not the indexer: two lenses under the same id would leave the winner
            // decided by file order, exactly as two rules over one component would.
            _thresholds.Add(lens.Id, new GradeThresholds(
                lens.Thresholds.DStartsAt,
                lens.Thresholds.CStartsAt,
                lens.Thresholds.BStartsAt,
                lens.Thresholds.AStartsAt));
        }

        SatietyScale = new PercentileScale(file.Percentiles.Satiety);

        DensityScales = file.Percentiles.Density.ToDictionary(
            entry => entry.Key,
            entry => new PercentileScale(entry.Value),
            StringComparer.OrdinalIgnoreCase);

        CatalogueCategories = file.CatalogueCategories.ToHashSet(StringComparer.OrdinalIgnoreCase);

        Rules = new CategoryRules(file.CategoryRules.Select(rule => new CategoryRule(
            rule.Category,
            rule.Lens,
            Enum.Parse<ScoreComponent>(rule.Component, ignoreCase: true),
            StrategyNamed(rule.Strategy))), CatalogueCategories);
    }

    /// <summary>The catalogue these numbers were measured on, and the date they were read.</summary>
    public string Catalogue { get; }

    public string MeasuredOn { get; }

    /// <summary>Why these numbers are what they are: the file carries its own reasoning.</summary>
    public IReadOnlyList<string> Notes { get; }
    public double ReferenceMealGrams { get; }

    /// <summary>The density below which no food may beat E, whatever its other components (P44).</summary>
    public double DensityFloor { get; }
    public Lens[] Lenses { get; }
    public PercentileScale SatietyScale { get; }

    /// <summary>One measured scale per nutrient set: a set without one cannot be ranked.</summary>
    public IReadOnlyDictionary<string, PercentileScale> DensityScales { get; }

    /// <summary>The NRF9.2 scale, which is what a tool measuring the general formula wants.</summary>
    public PercentileScale DensityScale => DensityScales[DensityScore.Nrf92.Name];
    public CategoryRules Rules { get; }

    /// <summary>
    /// Every category name the reference catalogue uses. A food carrying a category that is not in
    /// here did not come from that catalogue, so the rules table has no opinion about it — which is
    /// a different thing from a category somebody looked at and wrote no rule for.
    /// </summary>
    public IReadOnlySet<string> CatalogueCategories { get; }

    /// <summary>
    /// The engine these tables describe. One place per process builds it, so the gate and the API
    /// cannot end up grading with different scales or a different rules table.
    /// </summary>
    public ScoreCombiner Engine() => new(
        new GeneralStrategies(SatietyScale, DensityScales, ReferenceMealGrams), Rules);

    /// <summary>
    /// The cutoffs calibrated for a lens. Throws for a lens the file does not calibrate, rather
    /// than borrowing another lens's numbers and grading everything slightly wrong.
    /// </summary>
    public GradeThresholds ThresholdsFor(Lens lens) =>
        _thresholds.TryGetValue(lens.Id, out var thresholds)
            ? thresholds
            : throw new ArgumentException(
                $"No calibrated thresholds for the {lens.Name} lens.", nameof(lens));

    /// <summary>
    /// The letter a score earns under a lens, once the density floor has had its say, or null for
    /// a food there is no letter for. Null is not a bad grade and must not be shown as one: the
    /// product shows no letter at all. See <see cref="CombinedScore.IsNutritionallyEmpty"/>.
    /// </summary>
    // A weighted average lets one catastrophic component be outvoted. Bacon scores 4.5 on density
    // and 100 on protein: the engine had already diagnosed the food correctly and was simply
    // outnumbered, coming out C under Weight Loss and B under Fitness. No weighting repairs that,
    // and no category rule either — the strategy it would need does not exist as a measurement.
    // A food with no density is never floored: null compares false against the floor, which is the
    // answer wanted here. Water has no density to be bad at.
    public Grade? GradeFor(CombinedScore score, Lens lens) =>
        score.IsNutritionallyEmpty
            ? null
        : !score.CategoryIsRuled
        && score.Density is { IsEstimated: false } density
        && density.Score < DensityFloor
            ? Grade.E
            : ThresholdsFor(lens).GradeForScoreAlone(score.Value);

    private static DensityNutrients NutrientsNamed(string name) =>
        KnownNutrientSets.TryGetValue(name, out var nutrients)
            ? nutrients
            : throw new ArgumentException($"No density nutrient set is named {name}.", nameof(name));

    private static ComponentStrategy StrategyNamed(string name) =>
        KnownStrategies.TryGetValue(name, out var strategy)
            ? strategy
            : throw new ArgumentException($"No strategy is named {name}.", nameof(name));
}

// The file's shape, one type per JSON object. These carry values across the boundary and nothing
// else: every rule about what counts as a legal value stays in the domain types above, so a
// hand-edited file fails exactly the way a bad constructor call fails.
internal sealed record CalibrationFile
{
    public required string Catalogue { get; init; }
    public required string MeasuredOn { get; init; }
    public required string[] Notes { get; init; }
    public required double ReferenceMealGrams { get; init; }
    public required double DensityFloor { get; init; }
    public required LensFile[] Lenses { get; init; }
    public required RuleFile[] CategoryRules { get; init; }
    public required string[] CatalogueCategories { get; init; }
    public required PercentileFile Percentiles { get; init; }
}

internal sealed record LensFile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required double Satiety { get; init; }
    public required double Density { get; init; }
    public required double ProteinQuality { get; init; }
    public required string DensityNutrients { get; init; }
    public required ThresholdFile Thresholds { get; init; }
}

internal sealed record ThresholdFile
{
    public required double DStartsAt { get; init; }
    public required double CStartsAt { get; init; }
    public required double BStartsAt { get; init; }
    public required double AStartsAt { get; init; }
}

internal sealed record RuleFile
{
    public required string Category { get; init; }
    public required string Lens { get; init; }
    public required string Component { get; init; }
    public required string Strategy { get; init; }
}

internal sealed record PercentileFile
{
    public required double[] Satiety { get; init; }
    public required Dictionary<string, double[]> Density { get; init; }
}
