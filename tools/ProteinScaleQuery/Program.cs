using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sated.Scoring;

// Measures how the protein component and the letters behave as the reference meal changes,
// to answer the decision left open after Story 1.9: the component occupies only the bottom
// fifth of the scale, so under Fitness — where it carries half the weight — it contributes
// about 20 of the 50 points it claims, and the letters bunch.
//
// The knob is one number: the reference meal R. The note asks "if the whole meal were this
// food, would it reach the leucine threshold?", so the score stays per-100 g and works the
// same at Food, Recipe, Meal and Day. R = 100 is today's formula, kept as the baseline.
//
// R used to be applied by passing it as `grams` to the engine. That parameter is gone — no
// component reads a portion any more — so the sweep registers its own protein strategy per
// (category, lens) instead, which is what CategoryRules is for. The library's own reference
// meal stays frozen: this tool explores candidates, it does not change the engine.
//
// Predictions P1-P7 were written before this ran — see 04_delivery/7.protein-scale-report.

var breakpoints = File.ReadAllLines("../GradeDistributionQuery/percentiles.csv")
    .Skip(1)
    .Select(line => line.Split(','))
    .ToArray();

var satietyScale = new PercentileScale(
    [.. breakpoints.Select(row => double.Parse(row[1], CultureInfo.InvariantCulture))]);
var densityScale = new PercentileScale(
    [.. breakpoints.Select(row => double.Parse(row[2], CultureInfo.InvariantCulture))]);

var general = new GeneralStrategies(satietyScale, densityScale);

var json = File.ReadAllText("../UsdaCoverageQuery/data/surveyDownload.json");
var foods = JsonSerializer.Deserialize<SurveyFoodsFile>(json)!.Foods;

var inputs = new List<(string Description, FoodInput Input)>();

foreach (var food in foods)
{
    var amounts = food.FoodNutrients
        .Where(entry => entry.Amount is not null)
        .ToDictionary(entry => entry.Nutrient.Number, entry => entry.Amount!.Value);

    if (!Codes.Required.All(amounts.ContainsKey))
    {
        continue;
    }

    inputs.Add((food.Description, new FoodInput(
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
        Sodium: amounts["307"])));
}

var lenses = new[] { Lens.WeightLoss, Lens.Fitness };
var references = new double[] { 100, 125, 150, 175, 200, 250, 300, 400 };

Console.WriteLine($"FNDDS: {foods.Count} alimente · punctate: {inputs.Count}");

// Mirrors the four categories of CategoryRules.Standard, whose list is private. Without them
// the fat foods would fall back to the general formula and the sweep would stop describing the
// engine it claims to measure.
string[] fatCategories =
    ["Salad dressings and vegetable oils", "Butter and animal fats", "Margarine", "Mayonnaise"];

var categories = inputs.Select(food => food.Input.Category).ToHashSet();

var runs = references.ToDictionary(
    reference => reference,
    reference =>
    {
        var combiner = new ScoreCombiner(general, new CategoryRules([
            .. from category in fatCategories
               from lens in lenses
               select new CategoryRule(
                   category, lens.Name, ScoreComponent.Density, FatQuality.UnsaturatedShare),
            .. from category in categories
               from lens in lenses
               select new CategoryRule(
                   category, lens.Name, ScoreComponent.ProteinQuality, ProteinAt(reference))]));

        return inputs
            .Select(food => new Scored(
                food.Description,
                lenses.ToDictionary(
                    lens => lens.Name,
                    lens => combiner.Combine(food.Input, lens))))
            .ToArray();
    });

// The general formula with one number swapped: the reference meal the score is read against.
static ComponentStrategy ProteinAt(double reference) => food =>
{
    var measured = ProteinQualityScore.Calculate(food.LeucinePer100g, reference);

    if (measured is not null)
    {
        return ComponentValue.Measured(measured.Value);
    }

    return ComponentValue.Estimated(ProteinQualityScore.Calculate(
        ProteinCompleteness.EstimateLeucinePer100g(food.Protein, food.Category), reference));
};

Section("Componenta de proteină, pe măsura de referință");
Console.WriteLine($"{"R",5} {"p10",7} {"p25",7} {"mediană",8} {"p75",7} {"p90",7} " +
    $"{"sub 20",8} {"la 100",8}");

foreach (var reference in references)
{
    var protein = runs[reference]
        .Select(food => food.ByLens["Fitness"].ProteinQuality!.Score)
        .ToArray();

    Console.WriteLine($"{reference,5:F0} " +
        $"{Percentile(protein, 10),7:F1} {Percentile(protein, 25),7:F1} " +
        $"{Percentile(protein, 50),8:F1} {Percentile(protein, 75),7:F1} " +
        $"{Percentile(protein, 90),7:F1} " +
        $"{Share(protein.Count(value => value < 20), protein.Length),8} " +
        $"{Share(protein.Count(value => value >= 100), protein.Length),8}");
}

Console.WriteLine();
Console.WriteLine("P1 — la R=100 se aşteaptă mediană 17,2 şi 55,8% sub 20.");
Console.WriteLine("P2 — la R=300 se aşteaptă peste 25% la plafon.");
Console.WriteLine("P3 — se aşteaptă niciun R cu sub 10% în bază ŞI sub 10% la plafon.");

Section("Literele, pe măsura de referință");
Console.WriteLine($"{"R",5} {"lentilă",-12} {"bandă min",10} {"span p20-p80",13} " +
    $"{"la 1 punct",11} {"aceeaşi literă",15}");

foreach (var reference in references)
{
    var scored = runs[reference];

    var thresholds = lenses.ToDictionary(
        lens => lens.Name,
        lens => new[] { 20, 40, 60, 80 }
            .Select(p => Percentile(
                [.. scored.Select(food => food.ByLens[lens.Name].Value)], p))
            .ToArray());

    var agree = scored.Count(food =>
        Letter(food.ByLens["Weight Loss"].Value, thresholds["Weight Loss"]) ==
        Letter(food.ByLens["Fitness"].Value, thresholds["Fitness"]));

    foreach (var lens in lenses)
    {
        var cuts = thresholds[lens.Name];
        var narrowest = new[] { cuts[1] - cuts[0], cuts[2] - cuts[1], cuts[3] - cuts[2] }.Min();
        var borderline = scored.Count(food =>
            cuts.Any(cut => Math.Abs(food.ByLens[lens.Name].Value - cut) < 1));

        Console.WriteLine($"{reference,5:F0} {lens.Name,-12} {narrowest,10:F2} " +
            $"{cuts[3] - cuts[0],13:F2} {Share(borderline, scored.Length),11} " +
            $"{(lens.Name == "Fitness" ? Share(agree, scored.Length) : ""),15}");
    }
}

Console.WriteLine();
Console.WriteLine("P4 — se aşteaptă banda minimă sub Fitness maximă la R între 200 şi 250.");
Console.WriteLine("P5 — se aşteaptă aceeaşi literă sub 55,1% la R=200.");

Section("Alimentele numite — componenta de proteină");
var watched = new[]
{
    "Chicken breast, NS", "Cheese, Cheddar", "Spinach, raw", "Watermelon",
    "Egg, whole, cooked", "Rice, white, cooked", "Bread, white", "Olive oil"
};

Console.WriteLine($"{"aliment",-26} " + string.Join(" ", references.Select(r => $"{r,6:F0}")));

foreach (var needle in watched)
{
    var found = references
        .Select(reference => runs[reference]
            .FirstOrDefault(food =>
                food.Description.StartsWith(needle, StringComparison.OrdinalIgnoreCase)))
        .ToArray();

    if (found[0] is null)
    {
        Console.WriteLine($"{needle,-26} negăsit");
        continue;
    }

    Console.WriteLine($"{Truncate(found[0]!.Description, 24),-26} " +
        string.Join(" ", found.Select(food =>
            $"{food!.ByLens["Fitness"].ProteinQuality!.Score,6:F1}")));
}

Console.WriteLine();
Console.WriteLine("P6 — se aşteaptă spanacul sub 30 pentru orice R pana la 300.");

Section("P7 / Story 8.3 — litera sub ambele lentile (Weight Loss / Fitness)");
Console.WriteLine($"{"aliment",-26} " + string.Join(" ", references.Select(r => $"{r,9:F0}")));

foreach (var needle in new[]
    { "Cheese, Cheddar", "Chicken breast, NS", "Watermelon", "Spinach, raw", "Olive oil" })
{
    var cells = references.Select(reference =>
    {
        var scored = runs[reference];

        var cuts = lenses.ToDictionary(
            lens => lens.Name,
            lens => new[] { 20, 40, 60, 80 }
                .Select(p => Percentile(
                    [.. scored.Select(food => food.ByLens[lens.Name].Value)], p))
                .ToArray());

        var match = scored.First(food =>
            food.Description.StartsWith(needle, StringComparison.OrdinalIgnoreCase));

        var weightLoss = Letter(match.ByLens["Weight Loss"].Value, cuts["Weight Loss"]);
        var fitness = Letter(match.ByLens["Fitness"].Value, cuts["Fitness"]);

        return $"{weightLoss}/{fitness}".PadLeft(9);
    });

    Console.WriteLine($"{needle,-26} " + string.Join(" ", cells));
}

Section("Praguri recalibrate la R=300 — de copiat în GradeThresholds");
foreach (var lens in lenses)
{
    var values = runs[300].Select(food => food.ByLens[lens.Name].Value).ToArray();
    var cuts = new[] { 20, 40, 60, 80 }.Select(p => Percentile(values, p)).ToArray();

    Console.WriteLine($"{lens.Name,-12} dStartsAt: {cuts[0]:F2}, cStartsAt: {cuts[1]:F2}, " +
        $"bStartsAt: {cuts[2]:F2}, aStartsAt: {cuts[3]:F2}");
}

Console.WriteLine();
Console.WriteLine("P7 — se aşteaptă Cheddar la plafon de la R≈145 şi urcând spre A.");
Console.WriteLine("Story 8.3 cere pepenele A/C. La R=100 iese A/B.");

static string Share(int count, int total) => $"{(double)count / total:P1}";

void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"── {title} ".PadRight(96, '─'));
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
