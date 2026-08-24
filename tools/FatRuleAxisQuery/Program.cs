using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sated.Scoring;

// Asks whether the fat rule is attached to the wrong component.
//
// FatQuality exists for foods "where NRF9.2 carries no information: all nine encouraged
// nutrients sit near zero". Report 10 measured that premise and it does not hold: within the
// four fat categories the general density spreads by 67.2 points for margarine and 25.3 for
// dressings. NRF9.2 is still telling those foods apart.
//
// Satiety is the component the premise actually describes. Olive oil scores 0.0 on it, and so
// do butter, margarine and mayonnaise: the Fullness Factor floor catches every food that is
// almost entirely fat, so the axis cannot separate one from another. And satiety carries 50% of
// the Weight Loss lens against density's 30% — which is why olive oil is stuck at 25.3 with a
// density of 84.5 already handed to it by the rule.
//
// This is a correction with its own justification: the rule replaces the component that carries
// no information, and the measurement says which one that is. It is not chosen for the traps it
// happens to fix — S1 below is the test that decides it, and it is asked of the catalogue, not
// of the benchmark.
//
// Predictions S1-S4, written before this ran:
//   S1  Inside the four fat categories, satiety spreads by less than 5 percentile points while
//       density spreads by more than 25. The rule is on the wrong axis.
//   S2  Moving the rule to satiety lifts C1 olive oil over 45.55, from 25.3.
//   S3  The bottom 30 does not break: the four categories hold oils and spreads, and the bottom
//       30 is fried snacks, biscuits and sweets, which are in none of them.
//   S4  The gate reaches 6/8 under the best of the configurations tried.

const string calibration = "../../server/Sated.Calibration/";

string[] required = ["208", "203", "204", "291", "320", "401", "323",
    "301", "303", "304", "306", "606", "307"];

string[] fatCategories =
    ["Salad dressings and vegetable oils", "Butter and animal fats", "Margarine", "Mayonnaise"];

var json = File.ReadAllText("../UsdaCoverageQuery/data/surveyDownload.json");
var catalogue = new List<FoodInput>();
var named = new List<(string Description, FoodInput Food)>();

foreach (var food in JsonSerializer.Deserialize<SurveyFoodsFile>(json)!.Foods)
{
    var amounts = food.FoodNutrients
        .Where(entry => entry.Amount is not null)
        .ToDictionary(entry => entry.Nutrient.Number, entry => entry.Amount!.Value);

    if (!required.All(amounts.ContainsKey) || amounts["208"] <= 0)
    {
        continue;
    }

    var input = new FoodInput(
        Category: food.WweiaFoodCategory?.Description ?? "Not included in a food category",
        Calories: amounts["208"], Protein: amounts["203"], Fat: amounts["204"],
        Fiber: amounts["291"], VitaminA: amounts["320"], VitaminC: amounts["401"],
        VitaminE: amounts["323"], Calcium: amounts["301"], Iron: amounts["303"],
        Magnesium: amounts["304"], Potassium: amounts["306"],
        SaturatedFat: amounts["606"], Sodium: amounts["307"]);

    catalogue.Add(input);
    named.Add((food.Description, input));
}

// The shipped calibration, not percentiles.csv: since Story 1.12 the scales, the lenses and the
// reference meal live in calibration.json, and a tool that measures which axis a rule belongs on
// has to start from the numbers the engine runs.
var shipped = Calibration.Load();
var weightLoss = shipped.Lenses.Single(lens => lens.Name == "Weight Loss");
var fitness = shipped.Lenses.Single(lens => lens.Name == "Fitness");

var general = new GeneralStrategies(
    shipped.SatietyScale, shipped.DensityScale, shipped.ReferenceMealGrams);

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

Section("S1 — care componentă chiar nu spune nimic despre grăsimi?");
Console.WriteLine($"{"categorie",-38} {"n",4} {"sat p10",8} {"sat p90",8} {"spread",7} " +
    $"{"den spread",11}");

foreach (var category in fatCategories)
{
    var group = catalogue.Where(food => food.Category == category).ToArray();
    var satiety = group.Select(food => general.Satiety(food)!.Score).ToArray();
    var density = group.Select(food => general.Density(food)!.Score).ToArray();

    Console.WriteLine($"{Truncate(category, 38),-38} {group.Length,4} " +
        $"{Percentile(satiety, 10),8:F1} {Percentile(satiety, 90),8:F1} " +
        $"{Percentile(satiety, 90) - Percentile(satiety, 10),7:F1} " +
        $"{Percentile(density, 90) - Percentile(density, 10),11:F1}");
}

Console.WriteLine();
Console.WriteLine("S1 prezis: sațietatea sub 5, densitatea peste 25.");

Console.WriteLine();
Console.WriteLine($"Categoria lui C4 avocado: \"{nutrients[benchmark.First(r => r.Id == "C4").FdcId].Category}\"");
Console.WriteLine($"Categoria lui C5 cartof:  \"{nutrients[benchmark.First(r => r.Id == "C5").FdcId].Category}\"");

// Satiety is the one component ScoreCombiner refuses to let go missing — a lens with no weight
// left to divide by would produce NaN in silence. UnsaturatedShare returns null for a food with
// no fat, and fat-free mayonnaise is exactly that, so a rule on this axis needs a fallback that
// a rule on density does not. That is a real cost of the move, not a detail of the tool.
// A rule is a replacement, not a removal: when UnsaturatedShare cannot answer — fat-free
// mayonnaise has no fat to judge — the food goes back to the general formula rather than losing
// the component. On satiety this is not optional, since ScoreCombiner refuses to let that one
// go missing; on density it is the better answer anyway, because dropping a component is for
// data the catalogue lacks, not for a strategy that does not apply.
ComponentStrategy ForAxis(ScoreComponent component) => component == ScoreComponent.Satiety
    ? food => FatQuality.UnsaturatedShare(food) ?? general.Satiety(food)
    : food => FatQuality.UnsaturatedShare(food) ?? general.Density(food);

CategoryRule[] Build(ScoreComponent component, params string[] categories) =>
    [.. from category in categories
        from lens in shipped.Lenses
        select new CategoryRule(category, lens.Name, component, ForAxis(component))];

string[] withNuts = [.. fatCategories, "Nuts and seeds"];

var configurations = new (string Name, CategoryRules Rules)[]
{
    ("azi — densitate", new CategoryRules(Build(ScoreComponent.Density, fatCategories))),
    ("sațietate", new CategoryRules(Build(ScoreComponent.Satiety, fatCategories))),
    ("sațietate + nuci", new CategoryRules(Build(ScoreComponent.Satiety, withNuts))),
    ("ambele", new CategoryRules([
        .. Build(ScoreComponent.Density, fatCategories),
        .. Build(ScoreComponent.Satiety, fatCategories)])),
    ("ambele + nuci", new CategoryRules([
        .. Build(ScoreComponent.Density, withNuts),
        .. Build(ScoreComponent.Satiety, withNuts)])),
    ("grăsimi→sat, nuci→den", new CategoryRules([
        .. Build(ScoreComponent.Satiety, fatCategories),
        .. Build(ScoreComponent.Density, "Nuts and seeds")])),
    ("+ densitate grăsimi", new CategoryRules([
        .. Build(ScoreComponent.Satiety, fatCategories),
        .. Build(ScoreComponent.Density, [.. fatCategories, "Nuts and seeds"])])),
};

Section("Poarta, sub fiecare configuraţie");
Console.WriteLine($"{"configuraţie",-20} {"sus",7} {"jos",7} {"capcane",8} {"per WL",7} " +
    $"{"per Fit",8}  verdict");

foreach (var (name, rules) in configurations)
{
    var report = Evaluate(rules);
    var passes = report.Top >= 27 && report.Bottom >= 27 && report.Traps >= 6
        && report.PairsWeightLoss == 7 && report.PairsFitness == 7;

    Console.WriteLine($"{name,-20} {report.Top + "/30",7} {report.Bottom + "/30",7} " +
        $"{report.Traps + "/8",8} {report.PairsWeightLoss + "/7",7} " +
        $"{report.PairsFitness + "/7",8}  {(passes ? "TRECE" : "pică")}");
}

Section("Verificarea de credibilitate — ce păţesc grăsimile din catalog");
{
    var today = new ScoreCombiner(general, configurations[0].Rules);
    var proposed = new ScoreCombiner(general, configurations[5].Rules);

    GradeThresholds Cut(ScoreCombiner c)
    {
        var all = catalogue.Select(food => c.Combine(food, weightLoss).Value).ToArray();

        return new GradeThresholds(Percentile(all, 20), Percentile(all, 40),
            Percentile(all, 60), Percentile(all, 80));
    }

    var oldCut = Cut(today);
    var newCut = Cut(proposed);

    Console.WriteLine($"{"aliment",-46} {"azi",6} {"",2} {"propus",7} {"",2}");

    var watched = named
        .Where(entry => entry.Food.Category is "Salad dressings and vegetable oils"
            or "Butter and animal fats" or "Margarine" or "Mayonnaise" or "Nuts and seeds")
        .Where(entry => new[]
        {
            "Butter", "Butter, whipped", "Margarine", "Olive oil", "Vegetable oil",
            "Mayonnaise", "Mayonnaise, light", "Lard", "Coconut oil", "Shortening",
            "Italian dressing", "Italian dressing, light", "Ranch dressing",
            "Ranch dressing, light", "Walnuts, excluding honey roasted", "Peanut butter"
        }.Contains(entry.Description))
        .OrderBy(entry => entry.Description);

    foreach (var (description, food) in watched)
    {
        var before = today.Combine(food, weightLoss).Value;
        var after = proposed.Combine(food, weightLoss).Value;

        Console.WriteLine($"{Truncate(description, 46),-46} {before,6:F1} " +
            $"{oldCut.GradeForScoreAlone(before),2} {after,7:F1} {newCut.GradeForScoreAlone(after),2}");
    }
}

foreach (var (name, rules) in configurations.Where(c => c.Name != "azi — densitate"))
{
    Section($"Capcanele · {name}");
    Detail(rules);
}

void Detail(CategoryRules rules)
{
    var combiner = new ScoreCombiner(general, rules);
    var all = catalogue.Select(food => combiner.Combine(food, weightLoss).Value).ToArray();
    var thresholds = new GradeThresholds(
        Percentile(all, 20), Percentile(all, 40), Percentile(all, 60), Percentile(all, 80));

    Console.WriteLine($"praguri: {Percentile(all, 20):F2} / {Percentile(all, 40):F2} / " +
        $"{Percentile(all, 60):F2} / {Percentile(all, 80):F2}");

    foreach (var row in benchmark.Where(row => row.Id.StartsWith('C')))
    {
        var score = combiner.Combine(nutrients[row.FdcId], weightLoss);
        var grade = thresholds.GradeForScoreAlone(score.Value);

        Console.WriteLine($"{row.Id,-4} {Truncate(row.Description, 30),-30} {row.Required,5} " +
            $"{score.Value,6:F1} {grade,4} {(Accepted(row.Required, []).Contains(grade) ? "trece" : "")}" +
            $"   sat {score.Satiety.Score,5:F1} den {Cell(score.Density),5} " +
            $"prot {Cell(score.ProteinQuality),5}");
    }
}

Report Evaluate(CategoryRules rules)
{
    var combiner = new ScoreCombiner(general, rules);
    var all = catalogue.Select(food => combiner.Combine(food, weightLoss).Value).ToArray();
    var thresholds = new GradeThresholds(
        Percentile(all, 20), Percentile(all, 40), Percentile(all, 60), Percentile(all, 80));

    var scores = new Dictionary<(string, string), double>();

    foreach (var lens in shipped.Lenses)
    {
        foreach (var row in benchmark)
        {
            scores[(lens.Name, row.Id)] = combiner.Combine(nutrients[row.FdcId], lens).Value;
        }
    }

    int Passing(Func<string, bool> pick, Grade[] band) => benchmark
        .Where(row => pick(row.Id))
        .Count(row => Accepted(row.Required, band)
            .Contains(thresholds.GradeForScoreAlone(scores[(weightLoss.Name, row.Id)])));

    int Holding(Lens lens) => pairs.Count(pair =>
        scores[(lens.Name, pair[0])] > scores[(lens.Name, pair[1])]);

    return new Report(
        Passing(id => Numbered(id, 1, 30), [Grade.A, Grade.B]),
        Passing(id => Numbered(id, 31, 60), [Grade.D, Grade.E]),
        Passing(id => id.StartsWith('C'), []),
        Holding(weightLoss), Holding(fitness));
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

public record Report(int Top, int Bottom, int Traps, int PairsWeightLoss, int PairsFitness);

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
