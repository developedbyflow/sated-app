using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sated.Scoring;

// Asks whether crediting unsaturated fat inside the density score — as a term, not as the
// wholesale replacement the category rule performs — moves traps C1, C4 and C3 together.
//
// The three fail for one reason, not three: NRF9.2 counts nutrients per calorie, and fat is
// calories. Olive oil, avocado and walnuts are all foods whose merit is the quality of the fat
// they carry, which the measure cannot see. P24 rejected the direction for want of a reference
// value; FatQuality answered that with a share, which needs none, and has been running on 79
// foods ever since. Refused as a term, accepted as a replacement.
//
// The term, chosen so that no Daily Value has to be invented:
//     unsaturated calorie share = 900 * (Fat - SaturatedFat) / Calories
// which is the percentage of a food's energy that comes from unsaturated fat. A plain share of
// fat would hand lettuce a full credit for the 0.15 g it carries; this one cannot.
// It inherits FatQuality's known flaw: FNDDS reports no trans fat, so "unsaturated" means "not
// saturated" and margarine is flattered.
//
// A weight has to be chosen for it. That is the cost, and the test is built around it: the
// question is not "does it work" but "does ONE weight move all three traps without breaking the
// bottom 30". One weight is a calibration. Three weights are fitting on three foods.
//
// Predictions W1-W4, written before this ran:
//   W1  C1 olive oil is out of reach through density at any weight: its ceiling, with density
//       forced to 100, lands near 30 against a C cutoff of 45.55. Satiety and protein are both
//       zero for it, and density carries only 30% of the lens.
//   W2  C4 avocado is reachable: its ceiling clears 58.64.
//   W3  The weight that lifts C3 walnuts over 45.55 is at least 1.0 — it needs its density
//       percentile to go from 32.3 to about 82.7.
//   W4  At any weight that passes both C3 and C4, the bottom 30 falls under its 27 threshold.
//       Fried and fatty snacks carry large unsaturated calorie shares — potato chips are near
//       54 — so the term cannot lift whole fatty foods without lifting fatty junk with them.

const string calibration = "../../server/Sated.Calibration/";

string[] required = ["208", "203", "204", "291", "320", "401", "323",
    "301", "303", "304", "306", "606", "307"];

var json = File.ReadAllText("../UsdaCoverageQuery/data/surveyDownload.json");
var catalogue = new List<FoodInput>();

foreach (var food in JsonSerializer.Deserialize<SurveyFoodsFile>(json)!.Foods)
{
    var amounts = food.FoodNutrients
        .Where(entry => entry.Amount is not null)
        .ToDictionary(entry => entry.Nutrient.Number, entry => entry.Amount!.Value);

    if (!required.All(amounts.ContainsKey) || amounts["208"] <= 0)
    {
        continue;
    }

    catalogue.Add(new FoodInput(
        Category: food.WweiaFoodCategory?.Description ?? "Not included in a food category",
        Calories: amounts["208"], Protein: amounts["203"], Fat: amounts["204"],
        Fiber: amounts["291"], VitaminA: amounts["320"], VitaminC: amounts["401"],
        VitaminE: amounts["323"], Calcium: amounts["301"], Iron: amounts["303"],
        Magnesium: amounts["304"], Potassium: amounts["306"],
        SaturatedFat: amounts["606"], Sodium: amounts["307"]));
}

var breakpoints = ReadCsv("../GradeDistributionQuery/percentiles.csv");
var satietyScale = new PercentileScale([.. breakpoints.Select(row => Number(row[1]))]);
var densityScale = new PercentileScale([.. breakpoints.Select(row => Number(row[2]))]);
// The shipped calibration, not the tool's own copy: since Story 1.12 the lenses, the rules, the
// cutoffs and the reference meal all live in calibration.json, and a term proposed against a
// different set of numbers would answer a question nobody asked.
var shipped = Calibration.Load();
var weightLoss = shipped.Lenses.Single(lens => lens.Name == "Weight Loss");
var fitness = shipped.Lenses.Single(lens => lens.Name == "Fitness");

var general = new GeneralStrategies(satietyScale, densityScale, shipped.ReferenceMealGrams);
var engine = new ScoreCombiner(general, shipped.Rules);

Console.WriteLine($"FNDDS: {catalogue.Count} alimente punctabile");

static double Share(FoodInput food) => 900 * (food.Fat - food.SaturatedFat) / food.Calories;

static double? Nrf(FoodInput food) => DensityScore.Calculate(new DensityInput(
    food.Calories, food.Protein, food.Fiber, food.VitaminA, food.VitaminC, food.VitaminE,
    food.Calcium, food.Iron, food.Magnesium, food.Potassium, food.SaturatedFat, food.Sodium));

var nutrients = ReadCsv(calibration + "benchmark-nutrients.csv").ToDictionary(
    row => row[0],
    row => new FoodInput(
        Category: row[1], Calories: Number(row[2]), Protein: Number(row[3]),
        Fat: Number(row[4]), Fiber: Number(row[5]), VitaminA: Number(row[6]),
        VitaminC: Number(row[7]), VitaminE: Number(row[8]), Calcium: Number(row[9]),
        Iron: Number(row[10]), Magnesium: Number(row[11]), Potassium: Number(row[12]),
        SaturatedFat: Number(row[13]), Sodium: Number(row[14])));

var benchmark = ReadCsv(calibration + "benchmark.csv")
    .Select(row => (Id: row[0], Required: row[1], FdcId: row[2], Description: row[3]))
    .ToArray();

var pairs = ReadCsv(calibration + "benchmark-pairs.csv");

Section("Plafonul — cât ar da capcana dacă densitatea ar fi 100");
Console.WriteLine($"{"#",-4} {"aliment",-30} {"cerut",5} {"sat",6} {"den",6} {"prot",6} " +
    $"{"acum",6} {"plafon",7} {"prag",6}  ");

foreach (var row in benchmark.Where(row => row.Id.StartsWith('C')))
{
    var food = nutrients[row.FdcId];
    var score = engine.Combine(food, weightLoss);
    var lens = weightLoss;

    // The same arithmetic Combine uses, with density pinned at its maximum: the weight of a
    // missing component is left out of the divisor rather than counted as a zero.
    var weighted = lens.Satiety * score.Satiety.Score + lens.Density * 100;
    var used = lens.Satiety + lens.Density;

    if (score.ProteinQuality is not null)
    {
        weighted += lens.ProteinQuality * score.ProteinQuality.Score;
        used += lens.ProteinQuality;
    }

    var ceiling = weighted / used;
    var cutoff = Cutoff(row.Required);

    Console.WriteLine($"{row.Id,-4} {Truncate(row.Description, 30),-30} {row.Required,5} " +
        $"{score.Satiety.Score,6:F1} {Cell(score.Density),6} {Cell(score.ProteinQuality),6} " +
        $"{score.Value,6:F1} {ceiling,7:F1} {cutoff,6:F2}  " +
        $"{(ceiling >= cutoff ? "atinge" : "IMPOSIBIL prin densitate")}");
}

Console.WriteLine();
Console.WriteLine("W1 prezis: C1 imposibil, plafon lângă 30. W2 prezis: C4 atinge.");

Section("Baleiajul — un singur număr, toate criteriile deodată");
Console.WriteLine($"{"w",5} {"sus",7} {"jos",7} {"capcane",8} {"perechi WL",11} {"perechi Fit",11}" +
    $"  {"C3",6} {"C4",6}");

foreach (var step in Enumerable.Range(0, 41))
{
    var w = step * 0.1;
    var report = Evaluate(w);

    if (step % 2 == 0 || report.Traps >= 5)
    {
        Console.WriteLine($"{w,5:F1} {report.Top + "/30",7} {report.Bottom + "/30",7} " +
            $"{report.Traps + "/8",8} {report.PairsWeightLoss + "/7",11} " +
            $"{report.PairsFitness + "/7",11}  {report.C3,6:F1} {report.C4,6:F1}");
    }
}

Section("Detaliu la w = 1,0 — care sunt cele 5 capcane trecute");
Detail(1.0);

Console.WriteLine();
Console.WriteLine("Criterii: sus >= 27 · jos >= 27 · capcane >= 6 · perechile 7/7 pe ambele.");
Console.WriteLine("W3 prezis: C3 trece de 45,55 abia de la w >= 1,0.");
Console.WriteLine("W4 prezis: la orice w care trece C3 şi C4, cele 30 de jos cad sub 27.");

void Detail(double w)
{
    var raw = catalogue.Select(food => Nrf(food)!.Value + w * Share(food)).ToArray();
    var scale = new PercentileScale([.. Enumerable.Range(0, 101).Select(p => Percentile(raw, p))]);

    double Combined(FoodInput food)
    {
        var rule = shipped.Rules.Find(
            food.Category, weightLoss, ScoreComponent.Density);
        var density = rule is not null
            ? rule(food)?.Score
            : scale.Normalize(Nrf(food)!.Value + w * Share(food));

        var protein = general.ProteinQuality(food);
        var weighted = weightLoss.Satiety * general.Satiety(food)!.Score;
        var used = weightLoss.Satiety;

        if (density is not null)
        {
            weighted += weightLoss.Density * density.Value;
            used += weightLoss.Density;
        }

        if (protein is not null)
        {
            weighted += weightLoss.ProteinQuality * protein.Score;
            used += weightLoss.ProteinQuality;
        }

        return weighted / used;
    }

    var all = catalogue.Select(Combined).ToArray();
    var rebuilt = new GradeThresholds(
        Percentile(all, 20), Percentile(all, 40), Percentile(all, 60), Percentile(all, 80));

    Console.WriteLine($"praguri recalibrate: {Percentile(all, 20):F2} / {Percentile(all, 40):F2}" +
        $" / {Percentile(all, 60):F2} / {Percentile(all, 80):F2}");
    Console.WriteLine($"{"#",-4} {"aliment",-30} {"cerut",5} {"scor",6} {"dat",4}");

    foreach (var row in benchmark.Where(row => row.Id.StartsWith('C')))
    {
        var score = Combined(nutrients[row.FdcId]);
        var grade = rebuilt.GradeFor(score);
        var ok = Accepted(row.Required, []).Contains(grade);

        Console.WriteLine($"{row.Id,-4} {Truncate(row.Description, 30),-30} {row.Required,5} " +
            $"{score,6:F1} {grade,4} {(ok ? "trece" : "")}");
    }
}

// One evaluation of the whole gate at one weight. The density percentile scale and the four
// letter cutoffs are both rebuilt from the catalogue at that weight: a food's letter is its
// place among the others, so reading new scores against frozen breakpoints would grade them
// against a catalogue that no longer exists.
Report Evaluate(double w)
{
    var raw = catalogue.Select(food => Nrf(food)!.Value + w * Share(food)).ToArray();
    var scale = new PercentileScale([.. Enumerable.Range(0, 101)
        .Select(p => Percentile(raw, p))]);

    double Combined(FoodInput food, Lens lens)
    {
        var satiety = general.Satiety(food)!.Score;
        var protein = general.ProteinQuality(food);

        var rule = shipped.Rules.Find(food.Category, lens, ScoreComponent.Density);
        var density = rule is not null
            ? rule(food)?.Score
            : scale.Normalize(Nrf(food)!.Value + w * Share(food));

        var weighted = lens.Satiety * satiety;
        var used = lens.Satiety;

        if (density is not null)
        {
            weighted += lens.Density * density.Value;
            used += lens.Density;
        }

        if (protein is not null)
        {
            weighted += lens.ProteinQuality * protein.Score;
            used += lens.ProteinQuality;
        }

        return weighted / used;
    }

    var scores = new Dictionary<(string, string), double>();

    foreach (var lens in shipped.Lenses)
    {
        foreach (var row in benchmark)
        {
            scores[(lens.Name, row.Id)] = Combined(nutrients[row.FdcId], lens);
        }
    }

    var thresholds = shipped.ThresholdsFor(weightLoss);
    var catalogueScores = catalogue.Select(food => Combined(food, weightLoss)).ToArray();
    var rebuilt = new GradeThresholds(
        Percentile(catalogueScores, 20), Percentile(catalogueScores, 40),
        Percentile(catalogueScores, 60), Percentile(catalogueScores, 80));

    int Passing(Func<string, bool> pick, Grade[] band) => benchmark
        .Where(row => pick(row.Id))
        .Count(row => Accepted(row.Required, band)
            .Contains(rebuilt.GradeFor(scores[(weightLoss.Name, row.Id)])));

    int Holding(Lens lens) => pairs.Count(pair =>
        scores[(lens.Name, pair[0])] > scores[(lens.Name, pair[1])]);

    return new Report(
        Passing(id => Numbered(id, 1, 30), [Grade.A, Grade.B]),
        Passing(id => Numbered(id, 31, 60), [Grade.D, Grade.E]),
        Passing(id => id.StartsWith('C'), []),
        Holding(weightLoss),
        Holding(fitness),
        scores[(weightLoss.Name, "C3")],
        scores[(weightLoss.Name, "C4")]);
}

// The Weight Loss cutoffs, spelled out because GradeThresholds keeps its four numbers private and
// only answers which letter a score falls into. A dead local read them until now and nothing used
// it. If calibration.json is refitted, these four go stale and the tool has to be told.
static double Cutoff(string required)
{
    var lowest = Accepted(required, []).Max();

    return lowest switch
    {
        Grade.A => 71.77,
        Grade.B => 58.64,
        Grade.C => 45.55,
        Grade.D => 31.81,
        _ => 0
    };
}

static HashSet<Grade> Accepted(string required, Grade[] band) =>
    [.. band, .. required.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<Grade>)];

static bool Numbered(string id, int from, int to) =>
    int.TryParse(id, out var number) && number >= from && number <= to;

static string Cell(ComponentValue? value) => value is null ? "—" : $"{value.Score:F1}";

static double Percentile(double[] values, double percentile)
{
    var sorted = values.Order().ToArray();
    var position = percentile / 100 * (sorted.Length - 1);

    return sorted[(int)Math.Floor(position)]
        + (sorted[(int)Math.Ceiling(position)] - sorted[(int)Math.Floor(position)])
        * (position - Math.Floor(position));
}

void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"── {title} ".PadRight(92, '─'));
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

public record Report(
    int Top, int Bottom, int Traps, int PairsWeightLoss, int PairsFitness, double C3, double C4);

public record Nutrient([property: JsonPropertyName("number")] string Number);

public record FoodNutrient(
    [property: JsonPropertyName("nutrient")] Nutrient Nutrient,
    [property: JsonPropertyName("amount")] double? Amount
);

public record WweiaCategory(
    [property: JsonPropertyName("wweiaFoodCategoryDescription")] string Description
);

public record FoodItem(
    [property: JsonPropertyName("fdcId")] int FdcId,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("foodNutrients")] List<FoodNutrient> FoodNutrients,
    [property: JsonPropertyName("wweiaFoodCategory")] WweiaCategory? WweiaFoodCategory
);

public record SurveyFoodsFile([property: JsonPropertyName("SurveyFoods")] List<FoodItem> Foods);
