using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sated.Scoring;

// Asks what it would cost to hand the WWEIA category "Nuts and seeds" to the fat rule, which is
// the repair handoff §8 proposes for trap C3 (walnuts, required B/C, currently failing).
//
// The case for it is that the category list is inconsistent: it holds salad dressings, whose fat
// share of calories runs down to 0.167, and leaves out nuts at 0.859 (report 8).
//
// The case against it is the rule's own stated premise. FatQuality exists for foods "where NRF9.2
// carries no information: all nine encouraged nutrients sit near zero". Nuts are the opposite of
// that — vitamin E, magnesium, calcium, iron and fibre are exactly what they carry. Report 8
// already found the premise false for 38 of the 79 foods the rule catches today. Adding nuts
// would make that worse, not better, so the question is measured before it is decided.
//
// Predictions N1-N4, written before this ran:
//   N1  The premise fails for nuts: the general density of "Nuts and seeds" spans more than 40
//       percentile points, while each of the four fat categories spans less than 25. A category
//       whose foods spread out is a category NRF9.2 is still telling apart.
//   N2  C3 walnuts clears the C cutoff of 45.55 under Weight Loss, so the repair works.
//   N3  C2 almonds does not break: it stays B or better under Weight Loss.
//   N4  The rule collapses the distance between the two: almonds beat walnuts by more than 20
//       points of density today, and by less than 5 under the fat rule. The engine stops being
//       able to tell a nut from a nut.

const string calibration = "../../server/Sated.Calibration/";
const string nuts = "Nuts and seeds";

string[] required = ["208", "203", "204", "291", "320", "401", "323",
    "301", "303", "304", "306", "606", "307"];

var json = File.ReadAllText("../UsdaCoverageQuery/data/surveyDownload.json");
var foods = JsonSerializer.Deserialize<SurveyFoodsFile>(json)!.Foods;

var catalogue = new List<FoodInput>();

foreach (var food in foods)
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

// The shipped calibration, not percentiles.csv: since Story 1.12 the scales, the lenses, the
// rules and the reference meal all live in calibration.json, and a tool comparing a proposed
// table against today's must start from the table the engine actually runs.
var shipped = Calibration.Load();
var general = new GeneralStrategies(
    shipped.SatietyScale, shipped.DensityScales, shipped.ReferenceMealGrams);

// Density now depends on the lens, because a lens chooses which nutrients it counts (FR-26).
// This tool measures the general NRF9.2 formula, so it asks for the lens that carries that set.
var nrf92Lens = shipped.Lenses.First(lens => lens.DensityNutrients.Name == DensityScore.Nrf92.Name);

// The proposed table, built from the same shape the shipped one uses. Nothing in the
// engine changes: the tool hands the combiner a different table and reads what comes out.
var proposed = new CategoryRules([
    .. from category in new[]
       {
           "Salad dressings and vegetable oils", "Butter and animal fats",
           "Margarine", "Mayonnaise", nuts
       }
       from lens in shipped.Lenses
       select new CategoryRule(
           category, lens.Name, ScoreComponent.Density, FatQuality.UnsaturatedShare)]);

var before = new ScoreCombiner(general, shipped.Rules);
var after = new ScoreCombiner(general, proposed);

Console.WriteLine($"FNDDS: {catalogue.Count} alimente punctabile");

Section("N1 — mai are NRF9.2 ceva de spus despre categoria asta?");
Console.WriteLine($"{"categorie",-38} {"n",4} {"p10",6} {"p50",6} {"p90",6} {"spread",7}");

foreach (var category in new[]
{
    nuts, "Salad dressings and vegetable oils", "Butter and animal fats", "Margarine", "Mayonnaise"
})
{
    var densities = catalogue
        .Where(food => food.Category == category)
        .Select(food => general.Density(food, nrf92Lens)?.Score)
        .Where(score => score is not null)
        .Select(score => score!.Value)
        .ToArray();

    if (densities.Length == 0)
    {
        Console.WriteLine($"{Truncate(category, 38),-38} {0,4}  — nimic punctabil");
        continue;
    }

    Console.WriteLine($"{Truncate(category, 38),-38} {densities.Length,4} " +
        $"{Percentile(densities, 10),6:F1} {Percentile(densities, 50),6:F1} " +
        $"{Percentile(densities, 90),6:F1} " +
        $"{Percentile(densities, 90) - Percentile(densities, 10),7:F1}");
}

Console.WriteLine();
Console.WriteLine("N1 prezis: nucile peste 40, fiecare categorie de grăsime sub 25.");

Section("Cât mișcă regula întreaga categorie");
var nutFoods = catalogue.Where(food => food.Category == nuts).ToArray();
var generalDensities = nutFoods.Select(food => general.Density(food, nrf92Lens)!.Score).ToArray();
var ruleDensities = nutFoods.Select(food => FatQuality.UnsaturatedShare(food)?.Score)
    .Where(score => score is not null).Select(score => score!.Value).ToArray();

Console.WriteLine($"{nutFoods.Length} alimente în categorie");
Console.WriteLine($"densitate mediană azi        {Percentile(generalDensities, 50),6:F1}");
Console.WriteLine($"densitate mediană cu regula  {Percentile(ruleDensities, 50),6:F1}");

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

Section("N4 — mai deosebește motorul migdala de nucă?");
var almond = nutrients[benchmark.First(row => row.Id == "C2").FdcId];
var walnut = nutrients[benchmark.First(row => row.Id == "C3").FdcId];

Console.WriteLine($"{"",-12} {"azi",8} {"cu regula",10}");
Console.WriteLine($"{"migdale",-12} {general.Density(almond, nrf92Lens)!.Score,8:F1} " +
    $"{FatQuality.UnsaturatedShare(almond)!.Score,10:F1}");
Console.WriteLine($"{"nucă",-12} {general.Density(walnut, nrf92Lens)!.Score,8:F1} " +
    $"{FatQuality.UnsaturatedShare(walnut)!.Score,10:F1}");
Console.WriteLine($"{"distanţă",-12} " +
    $"{general.Density(almond, nrf92Lens)!.Score - general.Density(walnut, nrf92Lens)!.Score,8:F1} " +
    $"{FatQuality.UnsaturatedShare(almond)!.Score - FatQuality.UnsaturatedShare(walnut)!.Score,10:F1}");
Console.WriteLine();
Console.WriteLine("N4 prezis: peste 20 azi, sub 5 cu regula.");

foreach (var lens in shipped.Lenses)
{
    var thresholds = shipped.ThresholdsFor(lens);

    Section($"N2 / N3 — capcanele · {lens.Name}");
    Console.WriteLine($"{"#",-4} {"aliment",-32} {"cerut",6} " +
        $"{"azi",6} {"",3} {"cu regula",9} {"",3}");

    foreach (var row in benchmark.Where(row => row.Id.StartsWith('C')))
    {
        var food = nutrients[row.FdcId];
        var old = before.Combine(food, lens).Value;
        var fresh = after.Combine(food, lens).Value;
        var oldGrade = thresholds.GradeForScoreAlone(old);
        var freshGrade = thresholds.GradeForScoreAlone(fresh);
        var accepted = Accepted(row.Required);

        Console.WriteLine($"{row.Id,-4} {Truncate(row.Description, 32),-32} {row.Required,6} " +
            $"{old,6:F1} {Mark(oldGrade, accepted),3} {fresh,9:F1} {Mark(freshGrade, accepted),3}");
    }

    var passedBefore = benchmark.Where(row => row.Id.StartsWith('C')).Count(row =>
        Accepted(row.Required).Contains(
            thresholds.GradeForScoreAlone(before.Combine(nutrients[row.FdcId], lens).Value)));
    var passedAfter = benchmark.Where(row => row.Id.StartsWith('C')).Count(row =>
        Accepted(row.Required).Contains(
            thresholds.GradeForScoreAlone(after.Combine(nutrients[row.FdcId], lens).Value)));

    Console.WriteLine();
    Console.WriteLine($"capcane trecute: {passedBefore}/8 azi → {passedAfter}/8 cu regula" +
        $"{(lens.Name == "Weight Loss" ? " · prag 6" : " · nu se numără (P35)")}");

    var moved = benchmark
        .Where(row => thresholds.GradeForScoreAlone(before.Combine(nutrients[row.FdcId], lens).Value)
            != thresholds.GradeForScoreAlone(after.Combine(nutrients[row.FdcId], lens).Value))
        .ToArray();

    Console.WriteLine($"rânduri din {benchmark.Length} care schimbă litera: {moved.Length}" +
        $"{(moved.Length == 0 ? "" : " — " + string.Join(", ", moved.Select(row => row.Id)))}");
}

Section("Cere schimbarea o recalibrare a pragurilor de literă?");
Console.WriteLine($"{"lentilă",-12} {"prag",6} {"azi",8} {"cu regula",10} {"dif",7}");

foreach (var lens in shipped.Lenses)
{
    var old = catalogue.Select(food => before.Combine(food, lens).Value).ToArray();
    var fresh = catalogue.Select(food => after.Combine(food, lens).Value).ToArray();

    foreach (var p in new[] { 20, 40, 60, 80 })
    {
        Console.WriteLine($"{lens.Name,-12} {p,6} {Percentile(old, p),8:F2} " +
            $"{Percentile(fresh, p),10:F2} {Percentile(fresh, p) - Percentile(old, p),7:F2}");
    }
}

static HashSet<Grade> Accepted(string required) =>
    [.. required.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<Grade>)];

static string Mark(Grade grade, HashSet<Grade> accepted) =>
    $"{grade}{(accepted.Contains(grade) ? "" : "!")}";

static double Percentile(double[] values, double percentile)
{
    var sorted = values.Order().ToArray();
    var position = percentile / 100 * (sorted.Length - 1);
    var lower = (int)Math.Floor(position);
    var upper = (int)Math.Ceiling(position);

    return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
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
