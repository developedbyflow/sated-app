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

        Lenses = [.. file.Lenses.Select(lens =>
            new Lens(lens.Name, lens.Satiety, lens.Density, lens.ProteinQuality))];

        foreach (var lens in file.Lenses)
        {
            // Add, not the indexer: two lenses under the same name would leave the winner
            // decided by file order, exactly as two rules over one component would.
            _thresholds.Add(lens.Name, new GradeThresholds(
                lens.Thresholds.DStartsAt,
                lens.Thresholds.CStartsAt,
                lens.Thresholds.BStartsAt,
                lens.Thresholds.AStartsAt));
        }

        SatietyScale = new PercentileScale(file.Percentiles.Satiety);
        DensityScale = new PercentileScale(file.Percentiles.Density);

        Rules = new CategoryRules(file.CategoryRules.Select(rule => new CategoryRule(
            rule.Category,
            rule.Lens,
            Enum.Parse<ScoreComponent>(rule.Component, ignoreCase: true),
            StrategyNamed(rule.Strategy))));
    }

    /// <summary>The catalogue these numbers were measured on, and the date they were read.</summary>
    public string Catalogue { get; }

    public string MeasuredOn { get; }

    /// <summary>Why these numbers are what they are: the file carries its own reasoning.</summary>
    public IReadOnlyList<string> Notes { get; }
    public double ReferenceMealGrams { get; }
    public Lens[] Lenses { get; }
    public PercentileScale SatietyScale { get; }
    public PercentileScale DensityScale { get; }
    public CategoryRules Rules { get; }

    /// <summary>
    /// The cutoffs calibrated for a lens. Throws for a lens the file does not calibrate, rather
    /// than borrowing another lens's numbers and grading everything slightly wrong.
    /// </summary>
    public GradeThresholds ThresholdsFor(Lens lens) =>
        _thresholds.TryGetValue(lens.Name, out var thresholds)
            ? thresholds
            : throw new ArgumentException(
                $"No calibrated thresholds for the {lens.Name} lens.", nameof(lens));

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
    public required LensFile[] Lenses { get; init; }
    public required RuleFile[] CategoryRules { get; init; }
    public required PercentileFile Percentiles { get; init; }
}

internal sealed record LensFile
{
    public required string Name { get; init; }
    public required double Satiety { get; init; }
    public required double Density { get; init; }
    public required double ProteinQuality { get; init; }
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
    public required double[] Density { get; init; }
}
