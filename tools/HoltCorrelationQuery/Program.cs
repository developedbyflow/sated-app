using System.Globalization;
using Sated.Scoring;

// Asks whether the satiety score tracks the only measured satiety data the project has.
// The formula comes from a patent and has never been checked against Holt et al. 1995, even
// though the benchmark set carries 21 of its values. G0 fails on the traps, and all four failing
// traps fail on the satiety axis — so the question is the formula, not which rule to add next.
// Predictions P1-P3 were written before this ran.

const string calibration = "../../server/Sated.Calibration/";

// The shipped calibration, not the tool's own copy of it: since Story 1.12 the scales and the
// reference meal live in calibration.json, and reading percentiles.csv here would measure whatever
// the last tool run happened to leave on disk.
var shipped = Calibration.Load();

// The engine's own path, not a re-implementation of it: SatietyInput is internal on purpose, and
// a tool that recomputed the formula would be measuring its own copy of it.
var general = new GeneralStrategies(
    shipped.SatietyScale, shipped.DensityScale, shipped.ReferenceMealGrams);

var nutrients = ReadCsv(calibration + "benchmark-nutrients.csv").ToDictionary(
    row => row[0],
    row => new FoodInput(
        Category: row[1],
        Calories: Number(row[2]), Protein: Number(row[3]), Fat: Number(row[4]),
        Fiber: Number(row[5]), VitaminA: Number(row[6]), VitaminC: Number(row[7]),
        VitaminE: Number(row[8]), Calcium: Number(row[9]), Iron: Number(row[10]),
        Magnesium: Number(row[11]), Potassium: Number(row[12]),
        SaturatedFat: Number(row[13]), Sodium: Number(row[14])));

var byId = ReadCsv(calibration + "benchmark.csv")
    .ToDictionary(row => row[0], row => (FdcId: row[2], Description: row[3]));

var holt = ReadCsv(calibration + "holt.csv")
    .Select(row => (Id: row[0], Value: Number(row[1])))
    .ToArray();

var measured = holt
    .Select(entry =>
    {
        var ours = general.Satiety(nutrients[byId[entry.Id].FdcId])!.Score;

        return (entry.Id, byId[entry.Id].Description, entry.Value, Ours: ours);
    })
    .ToArray();

Console.WriteLine($"{measured.Length} alimente cu valoare Holt măsurată.");

Section("Ordonate după Holt");
Console.WriteLine($"{"#",-4} {"aliment",-36} {"Holt",6} {"rang",5} {"noi",6} {"rang",5} {"dif",5}");

var holtRanks = Ranks([.. measured.Select(m => m.Value)]);
var ourRanks = Ranks([.. measured.Select(m => m.Ours)]);

foreach (var index in Enumerable.Range(0, measured.Length)
    .OrderByDescending(index => measured[index].Value))
{
    var m = measured[index];
    var drift = ourRanks[index] - holtRanks[index];

    Console.WriteLine($"{m.Id,-4} {Truncate(m.Description, 36),-36} {m.Value,6:F0} " +
        $"{holtRanks[index],5:F1} {m.Ours,6:F1} {ourRanks[index],5:F1} {drift,5:F1}");
}

Section("P1 / P2 — corelaţia");
Console.WriteLine($"P1 — Spearman pe toate {measured.Length}: {Spearman(measured):F3} (prezis > 0,600)");

var withoutPotato = measured.Where(m => m.Id != "C5").ToArray();
var gain = Spearman(withoutPotato) - Spearman(measured);
Console.WriteLine($"P2 — fără cartof ({withoutPotato.Length}): {Spearman(withoutPotato):F3} · " +
    $"câştig {gain:F3} (prezis > 0,050)");

Section("P3 — capcanele grase au valori Holt?");
foreach (var id in new[] { "C1", "C2", "C3", "C4" })
{
    var has = holt.Any(entry => entry.Id == id);
    Console.WriteLine($"  {id} {Truncate(byId[id].Description, 40),-42} {(has ? "DA" : "nu")}");
}
Console.WriteLine("Prezis: niciuna. Holt 1995 n-a testat grăsimi dense.");

static double Spearman((string Id, string Description, double Value, double Ours)[] rows)
{
    var a = Ranks([.. rows.Select(r => r.Value)]);
    var b = Ranks([.. rows.Select(r => r.Ours)]);
    var mean = (a.Length + 1) / 2.0;

    var covariance = a.Zip(b, (x, y) => (x - mean) * (y - mean)).Sum();
    var spread = Math.Sqrt(a.Sum(x => (x - mean) * (x - mean)) * b.Sum(y => (y - mean) * (y - mean)));

    return covariance / spread;
}

// Average ranks for ties, which Spearman needs: fish appears twice at the same Holt value.
static double[] Ranks(double[] values)
{
    var order = Enumerable.Range(0, values.Length).OrderBy(i => values[i]).ToArray();
    var ranks = new double[values.Length];
    var position = 0;

    while (position < order.Length)
    {
        var last = position;
        while (last + 1 < order.Length && values[order[last + 1]] == values[order[position]])
        {
            last++;
        }

        var shared = (position + last) / 2.0 + 1;

        for (var index = position; index <= last; index++)
        {
            ranks[order[index]] = shared;
        }

        position = last + 1;
    }

    return ranks;
}

void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"── {title} ".PadRight(84, '─'));
}

static string Truncate(string text, int length) =>
    text.Length <= length ? text : text[..length];

static double Number(string cell) => double.Parse(cell, CultureInfo.InvariantCulture);

static string[][] ReadCsv(string path) =>
    [.. File.ReadAllLines(path)
        .Where(line => line.Length > 0 && !line.StartsWith('#'))
        .Skip(1)
        .Select(SplitRow)];

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

