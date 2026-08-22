using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sated.Scoring;

// Measures the distribution of the combined 0-100 score across FNDDS, to answer Story 1.7:
// whether the p20/p40/p60/p80 letter thresholds are far enough apart to mean anything, now
// that Story 1.6 normalises two of the three components by rank.
// Predictions P1-P6 were written before this ran — see 04_delivery/5.letter-threshold-report.

var breakpoints = File.ReadAllLines("../GradeDistributionQuery/percentiles.csv")
    .Skip(1)
    .Select(line => line.Split(','))
    .ToArray();

var satietyScale = new PercentileScale(
    [.. breakpoints.Select(row => double.Parse(row[1], CultureInfo.InvariantCulture))]);
var densityScale = new PercentileScale(
    [.. breakpoints.Select(row => double.Parse(row[2], CultureInfo.InvariantCulture))]);

var combiner = new ScoreCombiner(satietyScale, densityScale);

var json = File.ReadAllText("../UsdaCoverageQuery/data/surveyDownload.json");
var foods = JsonSerializer.Deserialize<SurveyFoodsFile>(json)!.Foods;

var lenses = new[] { Lens.WeightLoss, Lens.Fitness };
var scored = new List<Scored>();

foreach (var food in foods)
{
    var amounts = food.FoodNutrients
        .Where(entry => entry.Amount is not null)
        .ToDictionary(entry => entry.Nutrient.Number, entry => entry.Amount!.Value);

    if (!Codes.Required.All(amounts.ContainsKey))
    {
        continue;
    }

    var input = new FoodInput(
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
        Sodium: amounts["307"]);

    // FNDDS carries no amino acid data at all, so leucine is null for every food and every
    // score below is a Partial Grade. That is the measurement, not a flaw in it.
    var byLens = lenses.ToDictionary(
        lens => lens.Name,
        lens => combiner.Combine(input, 100, lens));

    scored.Add(new Scored(food.Description, byLens));
}

Console.WriteLine($"FNDDS: {foods.Count} alimente · punctate: {scored.Count}");
Console.WriteLine($"Partial Grade: {Share(scored.Count(f => f.ByLens.Values.All(s => s.IsPartial)))}");

var thresholds = new Dictionary<string, double[]>();

foreach (var lens in lenses)
{
    var values = scored.Select(food => food.ByLens[lens.Name].Value).ToArray();
    thresholds[lens.Name] = [.. new[] { 20, 40, 60, 80 }.Select(p => Percentile(values, p))];

    Section($"Distribuția sub {lens.Name}");
    Console.WriteLine($"{"p",5} {"scor",8}");
    foreach (var p in new[] { 0, 1, 5, 20, 25, 40, 50, 60, 75, 80, 95, 99, 100 })
    {
        Console.WriteLine($"{p,5} {Percentile(values, p),8:F2}");
    }

    var (p20, p40, p60, p80) = (
        Percentile(values, 20), Percentile(values, 40),
        Percentile(values, 60), Percentile(values, 80));

    Console.WriteLine();
    Console.WriteLine($"Praguri A-E: E<{p20:F2} · D<{p40:F2} · C<{p60:F2} · B<{p80:F2} · A≥{p80:F2}");
    Console.WriteLine($"P1 — IQR p25→p75: {Percentile(values, 75) - Percentile(values, 25):F2} puncte (prezis <25)");
    Console.WriteLine($"P2 — span p20→p80: {p80 - p20:F2} puncte (prezis <35)");
    Console.WriteLine($"Cea mai îngustă bandă de literă: {new[] { p40 - p20, p60 - p40, p80 - p60 }.Min():F2} puncte");
}

Section("P3 — cât de des cad cele două lentile pe aceeași literă");
var agree = scored.Count(food =>
    Letter(food.ByLens["Weight Loss"].Value, thresholds["Weight Loss"]) ==
    Letter(food.ByLens["Fitness"].Value, thresholds["Fitness"]));
Console.WriteLine($"Aceeași literă: {Share(agree)} (prezis >80%)");

Section("P4 — care lentilă întinde mai mult catalogul");
foreach (var lens in lenses)
{
    var values = scored.Select(food => food.ByLens[lens.Name].Value).ToArray();
    Console.WriteLine($"{lens.Name,-12} span p20→p80: {Percentile(values, 80) - Percentile(values, 20):F2}");
}
Console.WriteLine("Prezis: Weight Loss mai lat decât Fitness.");

Section("P5 / P6 — alimentele numite");
foreach (var needle in new[] { "Chicken breast", "Olive oil", "Butter, stick", "Spinach, raw", "Watermelon", "Wheat bran" })
{
    var match = scored.FirstOrDefault(food =>
        food.Description.StartsWith(needle, StringComparison.OrdinalIgnoreCase));

    if (match is null)
    {
        Console.WriteLine($"{needle,-24} negăsit");
        continue;
    }

    var line = string.Join(" · ", lenses.Select(lens =>
        $"{lens.Name}: {match.ByLens[lens.Name].Value:F1} " +
        $"{Letter(match.ByLens[lens.Name].Value, thresholds[lens.Name])}"));

    Console.WriteLine($"{Truncate(match.Description, 40),-42} {line}");
}

Section("Câte alimente stau la un punct de altă literă");
foreach (var lens in lenses)
{
    var cuts = thresholds[lens.Name];
    var borderline = scored.Count(food =>
        cuts.Any(cut => Math.Abs(food.ByLens[lens.Name].Value - cut) < 1));

    Console.WriteLine($"{lens.Name,-12} {Share(borderline)} din catalog");
}

string Share(int count) => $"{(double)count / scored.Count:P1}";

void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"── {title} ".PadRight(72, '─'));
}

static string Truncate(string text, int length) =>
    text.Length <= length ? text : text[..length];

static char Letter(double score, double[] thresholds) =>
    score < thresholds[0] ? 'E'
    : score < thresholds[1] ? 'D'
    : score < thresholds[2] ? 'C'
    : score < thresholds[3] ? 'B'
    : 'A';

static double Percentile(double[] values, double percentile)
{
    var sorted = values.Order().ToArray();
    var position = percentile / 100 * (sorted.Length - 1);
    var lower = (int)Math.Floor(position);
    var upper = (int)Math.Ceiling(position);

    return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
}

public record Scored(string Description, Dictionary<string, CombinedScore> ByLens);

public static class Codes
{
    public static readonly string[] Required =
        ["208", "203", "204", "291", "320", "401", "323", "301", "303", "304", "306", "606", "307"];
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
