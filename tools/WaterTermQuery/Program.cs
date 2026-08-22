using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sated.Scoring;

// Asks whether water (FNDDS nutrient 255) belongs in the satiety formula.
// Holt et al. 1995 reported that satiety tracks water content and food volume; the formula
// reads calories, protein, fat and fibre, and never water. C5 boiled potato is the trap that
// stands between G0 and 6/8, and it needs a satiety the formula cannot currently give it.
//
// The test is designed as a kill test, on purpose. Holt is the only independent validation the
// project has: choosing a coefficient that makes the Holt correlation rise would be fitting on
// the validation set, and 21 foods leave nothing to hold out. A negative result is therefore
// worth more here than a positive one — it costs no chosen number.
//
// Predictions P1-P5 were written before this ran.

const string calibration = "../../server/Sated.Calibration/";

// The same nutrients GradeDistributionQuery requires before it scores a food, so the catalogue
// measured here is the catalogue the percentile breakpoints were built from.
string[] required = ["208", "203", "204", "291", "320", "401", "323",
    "301", "303", "304", "306", "606", "307"];

var json = File.ReadAllText("../UsdaCoverageQuery/data/surveyDownload.json");
var foods = JsonSerializer.Deserialize<SurveyFoodsFile>(json)!.Foods;

var catalogue = new List<Entry>();
var skipped = 0;
var missingWater = 0;

foreach (var food in foods)
{
    var amounts = food.FoodNutrients
        .Where(entry => entry.Amount is not null)
        .ToDictionary(entry => entry.Nutrient.Number, entry => entry.Amount!.Value);

    if (!required.All(amounts.ContainsKey) || amounts["208"] <= 0)
    {
        skipped++;
        continue;
    }

    if (!amounts.ContainsKey("255"))
    {
        missingWater++;
        continue;
    }

    catalogue.Add(new Entry(
        food.FdcId, food.Description,
        Calories: amounts["208"], Protein: amounts["203"],
        Fat: amounts["204"], Fiber: amounts["291"], Water: amounts["255"]));
}

Console.WriteLine($"FNDDS: {foods.Count} încărcate · {catalogue.Count} punctabile · " +
    $"{skipped} sărite · {missingWater} fără apă");

// The candidate formula is written out here rather than called from the engine, because the
// engine does not have it. The k=0 check below is what keeps that honest: at zero coefficient
// the candidate must reproduce SatietyScore.Calculate bit for bit, or the tool is measuring a
// second, subtly different formula and every number after it is about the wrong thing.
static double Candidate(Entry food, double k) => Math.Clamp(
    41.7 / Math.Pow(Math.Max(30, food.Calories), 0.7)
        + 0.05 * Math.Min(30, food.Protein)
        + 0.000617 * Math.Pow(Math.Min(12, food.Fiber), 3)
        - 0.00000725 * Math.Pow(Math.Min(50, food.Fat), 3)
        + 0.617
        + k * food.Water / 100,
    0.5, 5);

static double Engine(Entry food) => SatietyScore.Calculate(
    new SatietyInput(food.Calories, food.Protein, food.Fat, food.Fiber));

var drift = catalogue.Max(food => Math.Abs(Candidate(food, 0) - Engine(food)));
Console.WriteLine($"Verificare: candidatul la k=0 vs. motor, abaterea maximă {drift:E2}" +
    $" {(drift == 0 ? "· identic" : "· DIFERĂ, restul raportului nu e valid")}");

var byId = catalogue.ToDictionary(food => food.FdcId);

var benchmark = ReadCsv(calibration + "benchmark.csv")
    .ToDictionary(row => row[0], row => (FdcId: int.Parse(row[2]), Description: row[3]));

var holt = ReadCsv(calibration + "holt.csv")
    .Select(row => (Id: row[0], Value: Number(row[1]), Food: byId[benchmark[row[0]].FdcId]))
    .ToArray();

var holtValues = holt.Select(row => row.Value).ToArray();

Section("Cele 21 de alimente măsurate de Holt");
Console.WriteLine($"{"#",-4} {"aliment",-40} {"Holt",5} {"kcal",5} {"apă",6} {"FF",5}");

foreach (var row in holt.OrderByDescending(row => row.Value))
{
    Console.WriteLine($"{row.Id,-4} {Truncate(row.Food.Description, 40),-40} {row.Value,5:F0} " +
        $"{row.Food.Calories,5:F0} {row.Food.Water,6:F1} {Candidate(row.Food, 0),5:F2}");
}

var baseline = Spearman(holtValues, [.. holt.Select(row => Candidate(row.Food, 0))]);

Section("P1 — apa singură, față de formula întreagă");
var waterAlone = Spearman(holtValues, [.. holt.Select(row => row.Food.Water)]);
Console.WriteLine($"Spearman(Holt, apă)      {waterAlone:F3}   (prezis 0,60 - 0,80)");
Console.WriteLine($"Spearman(Holt, formula)  {baseline:F3}   (cunoscut 0,853)");

Section("P2 — cât de redundantă e apa cu caloriile");
var onTwentyOne = Spearman(
    [.. holt.Select(row => row.Food.Water)], [.. holt.Select(row => row.Food.Calories)]);
var onCatalogue = Spearman(
    [.. catalogue.Select(food => food.Water)], [.. catalogue.Select(food => food.Calories)]);
Console.WriteLine($"Spearman(apă, calorii) pe cele 21          {onTwentyOne:F3}   (prezis < -0,85)");
Console.WriteLine($"Spearman(apă, calorii) pe tot catalogul    {onCatalogue:F3}");

Section("P3 — cel mai bun coeficient posibil");
Console.WriteLine($"{"k",6} {"Spearman",9} {"câştig",8}");

var best = (K: 0.0, Value: baseline);

foreach (var step in Enumerable.Range(0, 201))
{
    var k = step * 0.01;
    var value = Spearman(holtValues, [.. holt.Select(row => Candidate(row.Food, k))]);

    if (value > best.Value)
    {
        best = (k, value);
    }

    if (step % 10 == 0)
    {
        Console.WriteLine($"{k,6:F2} {value,9:F3} {value - baseline,8:F3}");
    }
}

Console.WriteLine();
Console.WriteLine($"Maxim: k = {best.K:F2} · Spearman {best.Value:F3} · " +
    $"câştig {best.Value - baseline:F3}   (prezis <= 0,90)");

Section("P4 — cartoful în distribuţia de apă a catalogului");
var potato = byId[benchmark["C5"].FdcId];
var wetter = catalogue.Count(food => food.Water > potato.Water);
Console.WriteLine($"Cartof fiert: {potato.Water:F1} g apă / 100 g · {potato.Calories:F0} kcal");
Console.WriteLine($"Mai umede: {wetter} din {catalogue.Count} = " +
    $"{(double)wetter / catalogue.Count:P1}   (prezis > 25%)");

// P5 has no coefficient to be read at, because the sweep chose zero. Probing at k=1 answers
// what the term would have done to the grades had it won, which is the half of the question
// the correlation never covers: a change can raise Holt agreement and still ruin the product.
var probe = best.K > 0 ? best.K : 1.00;

Section($"P5 — ce ar face termenul notelor, la k = {probe:F2}");
Console.WriteLine($"{"aliment",-40} {"acum",8} {"cu apă",8} {"dif",7}");

var atZero = Scale(catalogue, 0);
var atProbe = Scale(catalogue, probe);

var watched = new List<Entry> { potato };
watched.AddRange(catalogue
    .Where(food => food.Description.StartsWith("Soft drink", StringComparison.OrdinalIgnoreCase)
        || food.Description.StartsWith("Energy drink", StringComparison.OrdinalIgnoreCase)
        || food.Description.StartsWith("Water", StringComparison.OrdinalIgnoreCase)
        || food.Description.StartsWith("Lettuce", StringComparison.OrdinalIgnoreCase))
    .OrderBy(food => food.Description.Length)
    .Take(6));

foreach (var food in watched)
{
    var before = atZero.Normalize(Candidate(food, 0));
    var after = atProbe.Normalize(Candidate(food, probe));

    Console.WriteLine($"{Truncate(food.Description, 40),-40} {before,8:F1} {after,8:F1} " +
        $"{after - before,7:F1}");
}

Console.WriteLine();
Console.WriteLine("Prezis: caloriile lichide urcă. C5 are nevoie de saţietate 88,1.");

// Rebuilds the percentile scale for a given k. Every food's raw score moves when the formula
// changes, so a grade read against the old breakpoints would be a grade against a catalogue
// that no longer exists.
static PercentileScale Scale(List<Entry> catalogue, double k)
{
    var sorted = catalogue.Select(food => Candidate(food, k)).Order().ToArray();

    return new PercentileScale([.. Enumerable.Range(0, 101).Select(p =>
    {
        var position = p / 100.0 * (sorted.Length - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);

        return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    })]);
}

static double Spearman(double[] first, double[] second)
{
    var a = Ranks(first);
    var b = Ranks(second);
    var mean = (a.Length + 1) / 2.0;

    var covariance = a.Zip(b, (x, y) => (x - mean) * (y - mean)).Sum();
    var spread = Math.Sqrt(a.Sum(x => (x - mean) * (x - mean)) * b.Sum(y => (y - mean) * (y - mean)));

    return covariance / spread;
}

// Average ranks for ties, which Spearman needs: fish appears twice at the same Holt value,
// and the clamp at 5 ties every food that reaches the top of the scale.
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

public record Entry(
    int FdcId, string Description,
    double Calories, double Protein, double Fat, double Fiber, double Water);

public record Nutrient([property: JsonPropertyName("number")] string Number);

public record FoodNutrient(
    [property: JsonPropertyName("nutrient")] Nutrient Nutrient,
    [property: JsonPropertyName("amount")] double? Amount
);

public record FoodItem(
    [property: JsonPropertyName("fdcId")] int FdcId,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("foodNutrients")] List<FoodNutrient> FoodNutrients
);

public record SurveyFoodsFile([property: JsonPropertyName("SurveyFoods")] List<FoodItem> Foods);
