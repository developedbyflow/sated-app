using System.Globalization;
using Sated.Scoring;

// What a food entered by a person scores, against the same food as USDA knows it. The engine was
// calibrated entirely on FNDDS, and a food typed in from a package label differs from it in two
// ways that have nothing to do with the food: it carries no WWEIA category, so no category rule
// can match it, and it carries only the nutrients a label prints, so every micronutrient reads
// zero. Both are silent today. This measures what each one costs, per food, in letters.

var shipped = Calibration.Load();

var combiner = new ScoreCombiner(
    new GeneralStrategies(
        shipped.SatietyScale, shipped.DensityScales, shipped.ReferenceMealGrams),
    shipped.Rules);

// The benchmark's own foods, so the comparison is against numbers the project already trusts.
var rows = File.ReadAllLines("../../server/Sated.Calibration/benchmark-nutrients.csv")
    .Where(line => line.Length > 0 && !line.StartsWith('#'))
    .Skip(1)
    .Select(Split)
    .ToArray();

// benchmark-nutrients.csv carries no description; benchmark.csv does, keyed by the same id.
var names = File.ReadAllLines("../../server/Sated.Calibration/benchmark.csv")
    .Where(line => line.Length > 0 && !line.StartsWith('#'))
    .Skip(1)
    .Select(Split)
    .GroupBy(row => row[2])
    .ToDictionary(group => group.Key, group => group.First()[3]);

Console.WriteLine($"Set etalon: {rows.Length} alimente · calibrare {shipped.Catalogue}");

// A US nutrition label prints these and nothing else: energy, fat, saturated fat, sodium,
// carbohydrate, fibre, sugars, protein, and only vitamin D, calcium, iron and potassium of the
// micronutrients. Vitamin A, C and E, magnesium and thiamine are not on it at all.
FoodInput FromLabel(FoodInput usda) => usda with
{
    VitaminA = 0,
    VitaminC = 0,
    VitaminE = 0,
    Magnesium = 0,
    Thiamine = 0
};

FoodInput WithoutCategory(FoodInput usda) => usda with { Category = "User entered" };

var lens = shipped.Lenses.First(candidate => candidate.Name == "Weight Loss");

Grade GradeOf(FoodInput food) => shipped.GradeFor(combiner.Combine(food, lens), lens);

var movedByLabel = new List<string>();
var movedByCategory = new List<string>();
var movedByBoth = new List<string>();

Console.WriteLine();
Console.WriteLine($"{"aliment",-44}{"USDA",6}{"fără categorie",16}{"doar eticheta",15}{"amândouă",11}");

foreach (var row in rows)
{
    var usda = Read(row);
    var truth = GradeOf(usda);
    var noCategory = GradeOf(WithoutCategory(usda));
    var label = GradeOf(FromLabel(usda));
    var both = GradeOf(WithoutCategory(FromLabel(usda)));

    if (noCategory != truth) movedByCategory.Add(row[1]);
    if (label != truth) movedByLabel.Add(row[1]);
    if (both != truth) movedByBoth.Add(row[1]);

    if (both != truth)
    {
        Console.WriteLine($"{Truncate(names.GetValueOrDefault(row[0], row[1]), 44),-44}{truth,6}{Mark(noCategory, truth),16}" +
            $"{Mark(label, truth),15}{Mark(both, truth),11}");
    }
}

Console.WriteLine();
Console.WriteLine($"din {rows.Length} alimente, litera se schimbă la:");
Console.WriteLine($"  fără categoria WWEIA   {movedByCategory.Count,3}  ({Percent(movedByCategory.Count)})");
Console.WriteLine($"  doar ce e pe etichetă  {movedByLabel.Count,3}  ({Percent(movedByLabel.Count)})");
Console.WriteLine($"  amândouă               {movedByBoth.Count,3}  ({Percent(movedByBoth.Count)})");

string Percent(int count) => $"{(double)count / rows.Length * 100:F1}%";

static string Mark(Grade actual, Grade truth) =>
    actual == truth ? $"{actual}" : $"{actual} ✗";

static string Truncate(string text, int length) =>
    text.Length <= length ? text : text[..length];

static FoodInput Read(string[] row) => new(
    Category: row[1],
    Calories: Number(row[2]), Protein: Number(row[3]), Fat: Number(row[4]),
    Fiber: Number(row[5]), VitaminA: Number(row[6]), VitaminC: Number(row[7]),
    VitaminE: Number(row[8]), Calcium: Number(row[9]), Iron: Number(row[10]),
    Magnesium: Number(row[11]), Potassium: Number(row[12]), SaturatedFat: Number(row[13]),
    Sodium: Number(row[14]), VitaminD: Number(row[15]), Thiamine: Number(row[16]));

static double Number(string cell) => double.Parse(cell, CultureInfo.InvariantCulture);

static string[] Split(string line)
{
    var cells = new List<string>();
    var cell = new System.Text.StringBuilder();
    var quoted = false;

    foreach (var character in line)
    {
        if (character == '"') quoted = !quoted;
        else if (character == ',' && !quoted) { cells.Add(cell.ToString()); cell.Clear(); }
        else cell.Append(character);
    }

    cells.Add(cell.ToString());
    return [.. cells];
}
