using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sated.Scoring;

// The calibration the third lens needs, measured rather than chosen (FR-26, D5). Two numbers were
// missing and neither is a decision: the percentile scale NRF11.2 must be ranked against, and the
// four letter cutoffs. Both are measured the same way the first two lenses were — 101 percentiles
// for the scale (Story 1.6), p20/p40/p60/p80 for the cutoffs (Story 1.7).
//
// Both in one run, because they bootstrap each other: the cutoffs are percentiles of a combined
// score that cannot be computed until the density scale exists. Measuring the scale first, in
// memory, and combining against it is the same order the shipped lenses were calibrated in.
//
// The weights are the one thing this tool does not measure. SATED.md defines the GLP-1 lens by
// what its density counts, not by how it weighs the three components, so it takes the Weight Loss
// weighting and the difference between the two lenses is the nutrient set alone. That assumption
// is printed with its consequence: how many letters actually differ between them.

const string DataPath = "../UsdaCoverageQuery/data/surveyDownload.json";
const string LensName = "GLP-1";

var shipped = Calibration.Load();
var weightLoss = shipped.Lenses.First(lens => lens.Name == "Weight Loss");

var foods = JsonSerializer.Deserialize<SurveyFoodsFile>(File.ReadAllText(DataPath))!.Foods;
var catalogue = new List<(string Description, FoodInput Food)>();

foreach (var food in foods)
{
    var amounts = food.FoodNutrients
        .Where(entry => entry.Amount is not null)
        .ToDictionary(entry => entry.Nutrient.Number, entry => entry.Amount!.Value);

    if (!Codes.Required.All(amounts.ContainsKey))
    {
        continue;
    }

    catalogue.Add((food.Description, new FoodInput(
        Category: food.WweiaFoodCategory?.Description ?? "Not included in a food category",
        Calories: amounts["208"],
        Protein: amounts["203"],
        Fat: amounts["204"],
        Fiber: amounts["291"],
        VitaminA: amounts["320"],
        VitaminC: amounts["401"],
        VitaminE: amounts["323"],
        Calcium: amounts["301"],
        Iron: amounts["303"],
        Magnesium: amounts["304"],
        Potassium: amounts["306"],
        SaturatedFat: amounts["606"],
        Sodium: amounts["307"],
        VitaminD: amounts["328"],
        Thiamine: amounts["404"])));
}

Console.WriteLine($"Calibrare: {shipped.Catalogue}, măsurată {shipped.MeasuredOn}");
Console.WriteLine($"Catalog: {foods.Count} alimente · cu toți nutrienții: {catalogue.Count}");

// ── 1. Scara ────────────────────────────────────────────────────────────────────────────────────
// A zero-calorie food has no density under either set, so it contributes to neither scale. That is
// the same rule the NRF9.2 breakpoints were measured under.
var raw112 = catalogue
    .Select(entry => DensityScore.Calculate(ForDensity(entry.Food), DensityScore.Nrf112))
    .Where(value => value is not null)
    .Select(value => value!.Value)
    .ToArray();

var raw92 = catalogue
    .Select(entry => DensityScore.Calculate(ForDensity(entry.Food), DensityScore.Nrf92))
    .Where(value => value is not null)
    .Select(value => value!.Value)
    .ToArray();

var breakpoints = Enumerable.Range(0, 101).Select(p => Percentile(raw112, p)).ToArray();

Section("Distribuția brută: NRF11.2 față de NRF9.2");
Console.WriteLine($"{"percentilă",-12}{"NRF9.2",10}{"NRF11.2",10}{"diferență",12}");

foreach (var p in new[] { 0, 5, 25, 50, 75, 95, 100 })
{
    var (nrf92, nrf112) = (Percentile(raw92, p), Percentile(raw112, p));
    Console.WriteLine($"{p,-12}{nrf92,10:F1}{nrf112,10:F1}{nrf112 - nrf92,12:F1}");
}

Console.WriteLine();
Console.WriteLine($"media diferenței pe aliment: " +
    $"{raw112.Zip(raw92, (a, b) => a - b).Average():F2} puncte NRF · " +
    $"maxim {raw112.Zip(raw92, (a, b) => a - b).Max():F1}");

// ── 2. Motorul cu trei lentile ──────────────────────────────────────────────────────────────────
var glp1 = new Lens(LensName, weightLoss.Satiety, weightLoss.Density, weightLoss.ProteinQuality,
    DensityScore.Nrf112);

var scales = new Dictionary<string, PercentileScale>(StringComparer.OrdinalIgnoreCase)
{
    [DensityScore.Nrf92.Name] = shipped.DensityScale,
    [DensityScore.Nrf112.Name] = new PercentileScale(breakpoints)
};

// The rules are cloned from Weight Loss, not omitted. A lens with no category rules puts olive oil
// back at E — the Story 1.8 mistake — and cutoffs fitted on an engine missing them would be fitted
// on a product that never ships.
var rules = new CategoryRules([
    .. shipped.Rules.All,
    .. shipped.Rules.All
        .Where(rule => rule.LensName.Equals(weightLoss.Name, StringComparison.OrdinalIgnoreCase))
        .Select(rule => rule with { LensName = LensName })
]);

var combiner = new ScoreCombiner(
    new GeneralStrategies(shipped.SatietyScale, scales, shipped.ReferenceMealGrams), rules);

var scores = catalogue
    .Select(entry => (entry.Description,
        Glp1: combiner.Combine(entry.Food, glp1),
        WeightLoss: combiner.Combine(entry.Food, weightLoss)))
    .ToArray();

// ── 3. Pragurile ────────────────────────────────────────────────────────────────────────────────
var combined = scores.Select(entry => entry.Glp1.Value).ToArray();

var (d, c, b, a) = (
    Percentile(combined, 20), Percentile(combined, 40),
    Percentile(combined, 60), Percentile(combined, 80));

Section("Pragurile de literă, fitate pe catalog");
Console.WriteLine($"{"lentilă",-13}{"D≥",9}{"C≥",9}{"B≥",9}{"A≥",9}");
Console.WriteLine($"{"Weight Loss",-13}{shipped.ThresholdsFor(weightLoss).DStartsAt,9:F2}" +
    $"{shipped.ThresholdsFor(weightLoss).CStartsAt,9:F2}" +
    $"{shipped.ThresholdsFor(weightLoss).BStartsAt,9:F2}" +
    $"{shipped.ThresholdsFor(weightLoss).AStartsAt,9:F2}");
Console.WriteLine($"{LensName,-13}{d,9:F2}{c,9:F2}{b,9:F2}{a,9:F2}");

// ── 4. Cât diferă de fapt cele două lentile ─────────────────────────────────────────────────────
var glp1Thresholds = new GradeThresholds(d, c, b, a);
var wlThresholds = shipped.ThresholdsFor(weightLoss);

var moved = 0;
var byMove = new Dictionary<string, int>();

foreach (var entry in scores)
{
    var here = GradeOf(entry.Glp1, glp1Thresholds);
    var there = GradeOf(entry.WeightLoss, wlThresholds);

    if (here == there)
    {
        continue;
    }

    moved++;
    var key = $"{there} → {here}";
    byMove[key] = byMove.GetValueOrDefault(key) + 1;
}

Section("Cât separă GLP-1 de Weight Loss, cu aceleași ponderi");
Console.WriteLine($"litere diferite: {moved} din {scores.Length} " +
    $"({(double)moved / scores.Length * 100:F1}% din catalog)");

foreach (var (move, count) in byMove.OrderByDescending(pair => pair.Value))
{
    Console.WriteLine($"  {move}  {count,5}");
}

Section("De lipit în calibration.json");
Console.WriteLine($"\"densityNutrients\": \"{DensityScore.Nrf112.Name}\",");
// InvariantCulture: this line is JSON, and a Romanian machine prints 31,19 for a decimal point.
Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
    "\"thresholds\": {{ \"dStartsAt\": {0:F2}, \"cStartsAt\": {1:F2}, " +
    "\"bStartsAt\": {2:F2}, \"aStartsAt\": {3:F2} }}", d, c, b, a));

var json = new StringBuilder();
json.AppendLine("      \"nrf11.2\": [");
json.AppendLine(string.Join(",\n",
    breakpoints.Select(value => $"        {value.ToString("F4", CultureInfo.InvariantCulture)}")));
json.AppendLine("      ]");
File.WriteAllText("nrf11.2-percentiles.json", json.ToString());
Console.WriteLine($"cele 101 puncte, în nrf11.2-percentiles.json");

Grade GradeOf(CombinedScore score, GradeThresholds thresholds) =>
    !score.CategoryIsRuled
    && score.Density is { IsEstimated: false } density
    && density.Score < shipped.DensityFloor
        ? Grade.E
        : thresholds.GradeForScoreAlone(score.Value);

// FoodInput.ForDensity is internal to the engine, so a tool derives the same input itself.
static DensityInput ForDensity(FoodInput food) => new(
    food.Calories, food.Protein, food.Fiber, food.VitaminA, food.VitaminC, food.VitaminE,
    food.Calcium, food.Iron, food.Magnesium, food.Potassium, food.SaturatedFat, food.Sodium,
    food.VitaminD, food.Thiamine);

void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"── {title} ".PadRight(94, '─'));
}

static double Percentile(double[] values, double percentile)
{
    var sorted = values.Order().ToArray();
    var position = percentile / 100 * (sorted.Length - 1);
    var lower = (int)Math.Floor(position);
    var upper = (int)Math.Ceiling(position);

    return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
}

public static class Codes
{
    // Vitamin D (328) and thiamine (404) join the thirteen: FNDDS carries both on 100% of foods.
    public static readonly string[] Required =
        ["208", "203", "204", "291", "320", "401", "323", "301", "303", "304", "306", "606", "307",
         "328", "404"];
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
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("foodNutrients")] List<FoodNutrient> FoodNutrients,
    [property: JsonPropertyName("wweiaFoodCategory")] WweiaCategory? WweiaFoodCategory
);

public record SurveyFoodsFile([property: JsonPropertyName("SurveyFoods")] List<FoodItem> Foods);
