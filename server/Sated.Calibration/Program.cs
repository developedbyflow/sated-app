using System.Globalization;
using Sated.Scoring;

// The G0 harness (Story 1.11). It reads only committed files: the 68 benchmark foods with their
// nutrients, the grades required of them, and the frozen percentile breakpoints. The 63 MB FNDDS
// catalogue is deliberately not one of them — a gate that cannot run on a push is not a gate.
// Architecture §Butoane de reglaj (Story 1.12) gathers the breakpoints with the other four
// calibration tables; until that story exists they are read from the tool that measured them.

var breakpoints = ReadCsv("../../tools/GradeDistributionQuery/percentiles.csv");

var combiner = new ScoreCombiner(
    new GeneralStrategies(
        new PercentileScale([.. breakpoints.Select(row => Number(row[1]))]),
        new PercentileScale([.. breakpoints.Select(row => Number(row[2]))])),
    CategoryRules.Standard);

var nutrients = ReadCsv("benchmark-nutrients.csv").ToDictionary(
    row => row[0],
    row => new FoodInput(
        Category: row[1],
        Calories: Number(row[2]),
        Protein: Number(row[3]),
        Fat: Number(row[4]),
        Fiber: Number(row[5]),
        VitaminA: Number(row[6]),
        VitaminC: Number(row[7]),
        VitaminE: Number(row[8]),
        Calcium: Number(row[9]),
        Iron: Number(row[10]),
        Magnesium: Number(row[11]),
        Potassium: Number(row[12]),
        SaturatedFat: Number(row[13]),
        Sodium: Number(row[14])));

var benchmark = ReadCsv("benchmark.csv")
    .Select(row => new BenchmarkFood(row[0], row[1], row[2], row[3], row[4]))
    .ToArray();

Console.WriteLine($"Set etalon: {benchmark.Length} rânduri · {nutrients.Count} alimente distincte");

foreach (var lens in new[] { Lens.WeightLoss, Lens.Fitness })
{
    var thresholds = GradeThresholds.For(lens);

    Console.WriteLine();
    Console.WriteLine($"── {lens.Name} ".PadRight(100, '─'));
    Console.WriteLine($"{"#",-4} {"aliment",-34} {"cerut",6} {"scor",6} {"dat",4} " +
        $"{"sat",6} {"den",6} {"prot",6}  semne");

    foreach (var food in benchmark)
    {
        var input = nutrients[food.FdcId];
        var score = combiner.Combine(input, lens);

        Console.WriteLine($"{food.Id,-4} {Truncate(food.Description, 34),-34} " +
            $"{food.RequiredGrade,6} {score.Value,6:F1} {thresholds.GradeFor(score.Value),4} " +
            $"{score.Satiety.Score,6:F1} {Cell(score.Density),6} {Cell(score.ProteinQuality),6}" +
            $"  {Signs(food, input, score, lens)}");
    }
}

// Everything the report has to show beyond the grade itself: whether a component was missing,
// whether one was guessed, which category rule fired, and whether FNDDS forced a substitution.
// The last one matters most — a substituted food that grades badly is not the formula's fault.
static string Signs(BenchmarkFood food, FoodInput input, CombinedScore score, Lens lens)
{
    var signs = new List<string>();

    if (score.IsPartial)
    {
        signs.Add("lipsă");
    }

    if (score.HasEstimatedComponents)
    {
        signs.Add("estimat");
    }

    var ruled = new[]
        { ScoreComponent.Satiety, ScoreComponent.Density, ScoreComponent.ProteinQuality }
        .Where(component =>
            CategoryRules.Standard.Find(input.Category, lens, component) is not null)
        .ToArray();

    if (ruled.Length > 0)
    {
        signs.Add($"regulă:{string.Join("+", ruled)}");
    }

    if (food.Deviation.Length > 0)
    {
        signs.Add("abatere");
    }

    return string.Join(" ", signs);
}

static string Cell(ComponentValue? value) => value is null ? "—" : value.Score.ToString("F1");

static string Truncate(string text, int length) =>
    text.Length <= length ? text : text[..length];

static double Number(string cell) => double.Parse(cell, CultureInfo.InvariantCulture);

static string[][] ReadCsv(string path) =>
    [.. File.ReadAllLines(path)
        .Where(line => line.Length > 0 && !line.StartsWith('#'))
        .Skip(1)
        .Select(SplitRow)];

// A comma inside quotes is part of the value, not a separator — the deviation column is full of
// them. String.Split would cut "Beef, ground" in half and shift every column after it.
static string[] SplitRow(string line)
{
    var cells = new List<string>();
    var current = "";
    var quoted = false;

    foreach (var character in line)
    {
        if (character == '"')
        {
            quoted = !quoted;
        }
        else if (character == ',' && !quoted)
        {
            cells.Add(current);
            current = "";
        }
        else
        {
            current += character;
        }
    }

    cells.Add(current);

    return [.. cells];
}

record BenchmarkFood(
    string Id, string RequiredGrade, string FdcId, string Description, string Deviation);
