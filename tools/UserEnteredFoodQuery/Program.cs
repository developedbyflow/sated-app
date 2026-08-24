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
    VitaminA = null,
    VitaminC = null,
    VitaminE = null,
    Magnesium = null,
    Thiamine = null
};

FoodInput WithoutCategory(FoodInput usda) => usda with { Category = null };

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

// ── Candidatul: mediana catalogului în locul renormalizării ──────────────────────────────────────
// Rescaling assumes the nutrients a food does not report behave like the ones it does. Measured,
// that is too strong a claim: bacon reports decent protein, calcium, iron and vitamin D, so scaling
// over them lifts it from E to C. The weaker assumption is that an unreported nutrient is typical
// of the catalogue, which is what "we do not know" actually licenses.
Section("Mediana catalogului per nutrient, în %DV la 100 kcal");

var survey = System.Text.Json.JsonSerializer.Deserialize<SurveyFoodsFile>(
    File.ReadAllText("../UsdaCoverageQuery/data/surveyDownload.json"))!.Foods;

string[] codes = ["203", "291", "320", "401", "323", "301", "303", "304", "306", "328", "404"];
double[] dvs = [50, 28, 900, 90, 15, 1300, 18, 420, 4700, 20, 1.2];
string[] labels = ["proteină", "fibră", "vit. A", "vit. C", "vit. E", "calciu", "fier",
                   "magneziu", "potasiu", "vit. D", "tiamină"];

var shares = new List<double>[codes.Length];
for (var i = 0; i < codes.Length; i++) shares[i] = [];

foreach (var food in survey)
{
    var amounts = food.FoodNutrients
        .Where(entry => entry.Amount is not null)
        .ToDictionary(entry => entry.Nutrient.Number, entry => entry.Amount!.Value);

    if (!amounts.TryGetValue("208", out var kcal) || kcal <= 0) continue;

    var scale = 100 / Math.Max(10, kcal);

    for (var i = 0; i < codes.Length; i++)
    {
        if (amounts.TryGetValue(codes[i], out var amount))
        {
            shares[i].Add(Math.Min(100, amount * scale / dvs[i] * 100));
        }
    }
}

Console.WriteLine($"{"nutrient",-12}{"mediană %DV",13}{"medie",9}");
for (var i = 0; i < codes.Length; i++)
{
    var sorted = shares[i].Order().ToArray();
    Console.WriteLine($"{labels[i],-12}{sorted[sorted.Length / 2],12:F2}{shares[i].Average(),9:F2}");
}

// ── Alt catalog: categorie reală, dar cu alt nume ───────────────────────────────────────────────
// Open Food Facts calls olive oil "Huiles d'olive". That is a category — it is simply not one this
// engine's table knows. P68 fires the profile fallback only for a null category, on the grounds
// that a category which exists has been looked at by somebody. That reasoning holds inside FNDDS
// and breaks the moment the food comes from anywhere else.
Section("Categorie dintr-un catalog străin");

var foreign = 0;

Console.WriteLine($"{"aliment",-42}{"USDA",6}{"fără categorie",16}{"nume străin",14}");

foreach (var row in rows)
{
    var usda = Read(row);
    var truth = GradeOf(usda);
    var elsewhere = usda with { Category = $"Catégorie {row[1]}" };
    var grade = GradeOf(elsewhere);

    if (grade != truth)
    {
        foreign++;
        Console.WriteLine($"{Truncate(names.GetValueOrDefault(row[0], row[1]), 42),-42}{truth,6}" +
            $"{Mark(GradeOf(WithoutCategory(usda)), truth),16}{Mark(grade, truth),14}");
    }
}

Console.WriteLine();
Console.WriteLine($"cu nume de categorie străin: {foreign} din {rows.Length} greșite " +
    $"({(double)foreign / rows.Length * 100:F1}%)");

// ── Cele trei tratamente ale nutrientului absent, pe tot setul ──────────────────────────────────
Section("Zero · renormalizare (în motor azi) · mediana catalogului");

// Order in DensityScore.Nrf112: protein, fibre, A, C, E, calcium, iron, magnesium, potassium, D, B1.
int[] absentOnLabel = [2, 3, 4, 7, 10];
double[] medians = [7.32, 2.30, 1.18, 0.32, 2.35, 1.77, 3.50, 2.88, 2.24, 0.00, 5.38];

var typical = absentOnLabel.Sum(index => medians[index]);

var known = new DensityNutrients("known",
    [.. Enumerable.Range(0, DensityScore.Nrf112.Encouraged.Count)
        .Where(index => !absentOnLabel.Contains(index))
        .Select(index => DensityScore.Nrf112.Encouraged[index])], DensityScore.Nrf112.Limited);

Console.WriteLine($"suma medianelor absente: {typical:F2} puncte %DV");
Console.WriteLine();
Console.WriteLine($"{"aliment",-42}{"USDA",6}{"zero",8}{"renormalizat",14}{"mediană",10}{"prudent",10}");

var wrongZero = 0;
var wrongRescale = 0;
var wrongMedian = 0;
var wrongCautious = 0;

foreach (var row in rows)
{
    var usda = Read(row);
    var truth = GradeOf(usda);
    var label = FromLabel(usda);

    // The six-nutrient set counts exactly what a label reports, so its own rescale factor is one:
    // this is the zero-fill behaviour, measured against the engine's rescale over eleven.
    var zero = DensityScore.Calculate(ForDensity(label), known);
    var median = zero + typical;

    // The food's own average across what it did report, which is what rescaling assumes each
    // missing nutrient looks like. Taking the smaller of that and the catalogue median per
    // nutrient never claims more than both the food and the population support.
    var ownAverage = zero is null ? 0 : Math.Max(0, zero.Value) / known.Encouraged.Count;
    var cautious = zero + absentOnLabel.Sum(index => Math.Min(ownAverage, medians[index]));

    var gradeZero = zero is null ? GradeOf(label)
        : GradeFromDensity(label, shipped.DensityScales["nrf11.2"].Normalize(zero.Value));
    var gradeRescale = GradeOf(label);
    var gradeMedian = median is null ? GradeOf(label)
        : GradeFromDensity(label, shipped.DensityScales["nrf11.2"].Normalize(median.Value));
    var gradeCautious = cautious is null ? GradeOf(label)
        : GradeFromDensity(label, shipped.DensityScales["nrf11.2"].Normalize(cautious.Value));

    if (gradeZero != truth) wrongZero++;
    if (gradeRescale != truth) wrongRescale++;
    if (gradeMedian != truth) wrongMedian++;
    if (gradeCautious != truth) wrongCautious++;

    if (gradeZero != truth || gradeRescale != truth || gradeMedian != truth
        || gradeCautious != truth)
    {
        Console.WriteLine($"{Truncate(names.GetValueOrDefault(row[0], row[1]), 42),-42}{truth,6}" +
            $"{Mark(gradeZero, truth),8}{Mark(gradeRescale, truth),14}{Mark(gradeMedian, truth),10}{Mark(gradeCautious, truth),10}");
    }
}

Console.WriteLine();
Console.WriteLine($"greșite din {rows.Length}:  zero {wrongZero}  ·  renormalizat {wrongRescale}"
    + $"  ·  mediană {wrongMedian}  ·  prudent {wrongCautious}");

Grade GradeFromDensity(FoodInput food, double densityScore)
{
    var original = combiner.Combine(food, lens);

    var value = (lens.Satiety * original.Satiety.Score
        + lens.Density * densityScore
        + lens.ProteinQuality * original.ProteinQuality!.Score) / 100;

    return shipped.ThresholdsFor(lens).GradeForScoreAlone(value);
}

static DensityInput ForDensity(FoodInput food) => new(
    food.Calories, food.Protein, food.Fiber, food.VitaminA, food.VitaminC, food.VitaminE,
    food.Calcium, food.Iron, food.Magnesium, food.Potassium, food.SaturatedFat, food.Sodium,
    food.VitaminD, food.Thiamine);

void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"── {title} ".PadRight(94, '─'));
}

static string Mark(Grade actual, Grade truth) =>
    actual == truth ? $"{actual}" : $"{actual} \u2717";

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

public record Nutrient([property: System.Text.Json.Serialization.JsonPropertyName("number")] string Number);

public record FoodNutrient(
    [property: System.Text.Json.Serialization.JsonPropertyName("nutrient")] Nutrient Nutrient,
    [property: System.Text.Json.Serialization.JsonPropertyName("amount")] double? Amount
);

public record FoodItem(
    [property: System.Text.Json.Serialization.JsonPropertyName("foodNutrients")] List<FoodNutrient> FoodNutrients
);

public record SurveyFoodsFile(
    [property: System.Text.Json.Serialization.JsonPropertyName("SurveyFoods")] List<FoodItem> Foods);
