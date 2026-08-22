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
    .Select(row => new BenchmarkFood(row[0], row[1], row[2], row[3], row[4], row[5]))
    .ToArray();

Console.WriteLine($"Set etalon: {benchmark.Length} rânduri · {nutrients.Count} alimente distincte");

var lenses = new[] { Lens.WeightLoss, Lens.Fitness };
var grades = new Dictionary<(string Lens, string Id), (double Score, Grade Grade)>();

foreach (var lens in lenses)
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
        var grade = thresholds.GradeFor(score.Value);

        grades[(lens.Name, food.Id)] = (score.Value, grade);

        Console.WriteLine($"{food.Id,-4} {Truncate(food.Description, 34),-34} " +
            $"{food.RequiredGrade,6} {score.Value,6:F1} {grade,4} " +
            $"{score.Satiety.Score,6:F1} {Cell(score.Density),6} {Cell(score.ProteinQuality),6}" +
            $"  {Signs(food, input, score, lens)}");
    }
}

var pairs = ReadCsv("benchmark-pairs.csv");
var top = benchmark.Where(food => Numbered(food.Id, 1, 30)).ToArray();
var bottom = benchmark.Where(food => Numbered(food.Id, 31, 60)).ToArray();
var traps = benchmark.Where(food => food.Id.StartsWith('C')).ToArray();
var failed = 0;

foreach (var lens in lenses)
{
    Console.WriteLine();
    Console.WriteLine($"── Verdict · {lens.Name} ".PadRight(100, '─'));

    // P35: the 30/30/8 criteria are asked of Weight Loss only — it is the one lens whose required
    // grades were written blind. The ordering pairs are asked of both: they do not depend on the
    // goal, so they are what stands in for a second column of 60 letters.
    if (lens.Name == Lens.WeightLoss.Name)
    {
        failed += Tally(lens, "cele 30 de sus", top, 27, ["1", "8", "30"], [Grade.A, Grade.B]);
        failed += Tally(lens, "cele 30 de jos", bottom, 27, ["40"], [Grade.D, Grade.E]);
        failed += Tally(lens, "capcanele", traps, 6, [], []);
    }

    failed += TallyPairs(lens);
}

Console.WriteLine();
Console.WriteLine(failed == 0
    ? "G0: TRECE"
    : $"G0: PICĂ — {failed} {(failed == 1 ? "criteriu" : "criterii")} nesatisfăcut{(failed == 1 ? "" : "e")}");

// The process exit code, read straight by GitHub Actions in step 4: a failed gate has to stop
// the push, not just print a sad table nobody reads.
return failed == 0 ? 0 : 1;

// Not static: both read `grades`, which the report filled in. Passing 69 scores through a
// parameter to save a keyword would make the call sites unreadable for nothing.
int Tally(Lens lens, string label, BenchmarkFood[] group, int minimum, string[] mustHold, Grade[] band)
{
    var missed = group
        .Where(food => !Holds(food, grades[(lens.Name, food.Id)].Grade, band))
        .ToArray();

    var blocking = missed.Where(food => mustHold.Contains(food.Id)).ToArray();
    var passed = group.Length - missed.Length;
    var ok = passed >= minimum && blocking.Length == 0;

    Console.WriteLine(
        $"{label,-16} {passed,2}/{group.Length} · prag {minimum} · {(ok ? "TRECE" : "PICĂ")}");

    foreach (var food in missed)
    {
        var (score, grade) = grades[(lens.Name, food.Id)];

        Console.WriteLine($"   {food.Id,-4} {Truncate(food.Description, 32),-34} " +
            $"cerut {food.RequiredGrade,-4} dat {grade} ({score,5:F1})" +
            $"{(mustHold.Contains(food.Id) ? "   ← NU ARE VOIE SĂ PICE" : "")}");
    }

    return ok ? 0 : 1;
}

int TallyPairs(Lens lens)
{
    var broken = pairs
        .Where(pair => grades[(lens.Name, pair[0])].Score <= grades[(lens.Name, pair[1])].Score)
        .ToArray();

    Console.WriteLine($"{"perechile",-16} {pairs.Length - broken.Length,2}/{pairs.Length} · " +
        $"fără toleranță · {(broken.Length == 0 ? "TRECE" : "PICĂ")}");

    foreach (var pair in broken)
    {
        Console.WriteLine($"   {pair[0]} ({grades[(lens.Name, pair[0])].Score:F1}) nu bate " +
            $"{pair[1]} ({grades[(lens.Name, pair[1])].Score:F1}) — {pair[2]}");
    }

    return broken.Length == 0 ? 0 : 1;
}

// The gate is a band, not the exact letter: the set asks the top 30 to come out A or B, so a food
// required A that lands B has not failed it. Where the required grade widens the band — #8, after
// FNDDS forced an 80% lean substitution — the wider one wins.
static HashSet<Grade> Accepted(string required, Grade[] band) =>
    [.. band,
       .. required.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<Grade>)];

// Traps pass no group band, so they carry their own direction instead. A trap exists to catch
// one mistake, not both: C7 popcorn is there because a whole grain that looks like junk invites
// the model to underrate it, and coming out better than asked is that trap held, not sprung.
// C8 Cheddar is the opposite — it is there because protein invites the model to overrate cheese.
// Without this the traps were the one group judged on the exact letter, while the 30 above and
// the 30 below already ran on a band. The direction is read from benchmark.csv, per trap: a food
// with no direction still has to land exactly, which is what the 60 do inside their group band.
// Grade is ordered A to E, so a better letter compares as smaller.
static bool Holds(BenchmarkFood food, Grade grade, Grade[] band)
{
    var accepted = Accepted(food.RequiredGrade, band);

    if (accepted.Contains(grade))
    {
        return true;
    }

    return food.Direction switch
    {
        "under" => grade < accepted.Min(),
        "over" => grade > accepted.Max(),
        _ => false
    };
}

static bool Numbered(string id, int from, int to) =>
    int.TryParse(id, out var number) && number >= from && number <= to;

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
    string Id, string RequiredGrade, string FdcId, string Description, string Deviation,
    string Direction);
