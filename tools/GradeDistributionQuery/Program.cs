using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sated.Scoring;

// Measures the distribution of Satiety and Density across the whole of FNDDS, to answer
// Decision E: what replaces the invented -50..150 normalisation range, and whether the
// normalisation should be a linear range at all rather than a percentile rank.
// Predictions P1-P6 were written before this ran — see 04_delivery/4.grade-distribution-report.

var json = File.ReadAllText("../UsdaCoverageQuery/data/surveyDownload.json");
var foods = JsonSerializer.Deserialize<SurveyFoodsFile>(json)!.Foods;

Console.WriteLine($"FNDDS: {foods.Count} alimente încărcate.");

var scored = new List<Scored>();
var zeroCalorie = new List<string>();
var incomplete = new List<string>();

foreach (var food in foods)
{
    var amounts = food.FoodNutrients
        .Where(entry => entry.Amount is not null)
        .ToDictionary(entry => entry.Nutrient.Number, entry => entry.Amount!.Value);

    if (!Codes.Required.All(amounts.ContainsKey))
    {
        incomplete.Add(food.Description);
        continue;
    }

    if (amounts["208"] <= 0)
    {
        zeroCalorie.Add(food.Description);
        continue;
    }

    var satiety = SatietyScore.Calculate(new SatietyInput(
        Calories: amounts["208"],
        Protein: amounts["203"],
        Fat: amounts["204"],
        Fiber: amounts["291"]));

    var density = DensityScore.Calculate(new DensityInput(
        Calories: amounts["208"],
        Protein: amounts["203"],
        Fiber: amounts["291"],
        VitaminA: amounts["320"],
        VitaminC: amounts["401"],
        VitaminE: amounts["323"],
        Calcium: amounts["301"],
        Iron: amounts["303"],
        Magnesium: amounts["304"],
        Potassium: amounts["306"],
        SaturatedFat: amounts["606"],
        Sodium: amounts["307"]))!.Value;   // never null: zero-calorie foods are skipped above

    scored.Add(new Scored(food.Description, satiety, density, amounts));
}

Console.WriteLine($"Punctate: {scored.Count} · 0 kcal, sărite: {zeroCalorie.Count} · nutrienți lipsă: {incomplete.Count}");

var satieties = scored.Select(food => food.Satiety).ToArray();
var densities = scored.Select(food => food.Density).ToArray();

Section("Percentile");
Console.WriteLine($"{"p",4} {"Satiety",10} {"Density",10}");
foreach (var p in new[] { 0, 1, 5, 10, 20, 25, 30, 40, 50, 60, 70, 75, 80, 90, 95, 99, 100 })
{
    Console.WriteLine($"{p,4} {Percentile(satieties, p),10:F2} {Percentile(densities, p),10:F1}");
}

// The breakpoints Story 1.6 needs: rank normalisation interpolates between these instead of
// mapping linearly between two invented ends.
var breakpoints = string.Join("\n", Enumerable.Range(0, 101)
    .Select(p => FormattableString.Invariant(
        $"{p},{Percentile(satieties, p):F4},{Percentile(densities, p):F4}")));
File.WriteAllText("percentiles.csv", "percentile,satiety,density\n" + breakpoints + "\n");
Console.WriteLine("Praguri scrise în percentiles.csv (101 puncte per scor).");

Section("P1 — corelația Satiety / Density");
Console.WriteLine($"Spearman: {Spearman(satieties, densities):F3}");
Console.WriteLine("Confirmată dacă 0,2-0,6. Peste 0,8 = ponderile din FR-25 sunt teatru.");

Section("P2 — cât de des mușcă plafoanele din Satiety");
Report("calorii sub 30", scored.Count(food => food.Amounts["208"] < 30));
Report("proteină peste 30 g", scored.Count(food => food.Amounts["203"] > 30));
Report("fibră peste 12 g", scored.Count(food => food.Amounts["291"] > 12));
Report("grăsime peste 50 g", scored.Count(food => food.Amounts["204"] > 50));
Report("cel puțin unul dintre cele patru", scored.Count(food =>
    food.Amounts["208"] < 30 || food.Amounts["203"] > 30 ||
    food.Amounts["291"] > 12 || food.Amounts["204"] > 50));
Report("ieșirea plafonată la 5", scored.Count(food => food.Satiety >= 5));
Report("ieșirea plafonată la 0,5", scored.Count(food => food.Satiety <= 0.5));
Console.WriteLine("Confirmată dacă „cel puțin unul\" e peste 10%. Peste 40% = Satiety nu mai distinge.");

Section("P3 — asimetria Density");
var (d1, d50, d99) = (Percentile(densities, 1), Percentile(densities, 50), Percentile(densities, 99));
Console.WriteLine($"p1 {d1:F1} · mediană {d50:F1} · p99 {d99:F1}");
Console.WriteLine($"Raport coadă sus / coadă jos: {(d99 - d50) / (d50 - d1):F2}");
Console.WriteLine("Confirmată la 3,0 sau peste. Sub 3 = normalizarea prin interval supraviețuiește.");

Section("P4 — îngrămădirea Satiety");
var (bandShare, bandLow) = DensestBand(satieties, width: 1.0);
Console.WriteLine($"Cea mai încărcată bandă de 1,0: [{bandLow:F2} · {bandLow + 1:F2}] ține {bandShare:P1}");
Console.WriteLine("Confirmată peste 60%.");

Section("P5 — capătul de sus al lui -50..150");
Console.WriteLine($"p99 Density: {d99:F1} · maxim: {densities.Max():F1}");
Console.WriteLine($"Peste 150: {Share(densities.Count(value => value > 150))}");
Console.WriteLine("Confirmată dacă p99 e sub 100.");

Section("P6 — capătul de jos");
Console.WriteLine($"Minim: {densities.Min():F1} · p1: {d1:F1}");
Console.WriteLine($"Sub 0: {Share(densities.Count(value => value < 0))}");
Console.WriteLine($"Sub -50: {Share(densities.Count(value => value < -50))}");
Console.WriteLine("Confirmată dacă minimul trece sub -50 și sub 5% din catalog e negativ.");
Console.WriteLine();
Console.WriteLine("Cele mai negative 10:");
foreach (var food in scored.OrderBy(food => food.Density).Take(10))
{
    Console.WriteLine($"  {food.Density,8:F1}  {food.Description}");
}
Console.WriteLine();
Console.WriteLine("Cele mai pozitive 10:");
foreach (var food in scored.OrderByDescending(food => food.Density).Take(10))
{
    Console.WriteLine($"  {food.Density,8:F1}  {food.Description}");
}

if (zeroCalorie.Count > 0)
{
    Section("Alimentele cu 0 kcal — sărite, ele sunt cazul FR-7");
    foreach (var description in zeroCalorie.Take(30))
    {
        Console.WriteLine($"  {description}");
    }
}

void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"── {title} ".PadRight(70, '─'));
}

void Report(string label, int count) => Console.WriteLine($"{label,-36} {count,6} {Share(count),8}");

string Share(int count) => $"{(double)count / scored.Count:P1}";

// Linear interpolation between the two neighbouring ranks — the same definition Excel and
// numpy use by default, so the numbers in the report can be checked against either.
static double Percentile(double[] values, double percentile)
{
    var sorted = values.Order().ToArray();
    var position = percentile / 100 * (sorted.Length - 1);
    var lower = (int)Math.Floor(position);
    var upper = (int)Math.Ceiling(position);

    return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
}

// Spearman is Pearson applied to ranks, so it measures whether the two scores order foods
// the same way without assuming the relationship is a straight line.
static double Spearman(double[] first, double[] second)
{
    var firstRanks = Ranks(first);
    var secondRanks = Ranks(second);
    var mean = (first.Length - 1) / 2.0;

    var covariance = firstRanks.Zip(secondRanks, (a, b) => (a - mean) * (b - mean)).Sum();
    var firstSpread = Math.Sqrt(firstRanks.Sum(rank => Math.Pow(rank - mean, 2)));
    var secondSpread = Math.Sqrt(secondRanks.Sum(rank => Math.Pow(rank - mean, 2)));

    return covariance / (firstSpread * secondSpread);
}

// Tied values share the average of the ranks they span; otherwise a food's rank would
// depend on the order it happened to be read from the file.
static double[] Ranks(double[] values)
{
    var order = Enumerable.Range(0, values.Length).OrderBy(index => values[index]).ToArray();
    var ranks = new double[values.Length];

    for (var start = 0; start < order.Length;)
    {
        var end = start;
        while (end + 1 < order.Length && values[order[end + 1]] == values[order[start]])
        {
            end++;
        }

        var shared = (start + end) / 2.0;
        for (var index = start; index <= end; index++)
        {
            ranks[order[index]] = shared;
        }

        start = end + 1;
    }

    return ranks;
}

// Slides a fixed-width window over the sorted values and keeps the fullest position, which
// answers "how much of the catalogue sits inside one band" without picking the band by hand.
static (double Share, double Low) DensestBand(double[] values, double width)
{
    var sorted = values.Order().ToArray();
    var (bestCount, bestLow, end) = (0, sorted[0], 0);

    for (var start = 0; start < sorted.Length; start++)
    {
        while (end + 1 < sorted.Length && sorted[end + 1] <= sorted[start] + width)
        {
            end++;
        }

        if (end - start + 1 > bestCount)
        {
            (bestCount, bestLow) = (end - start + 1, sorted[start]);
        }
    }

    return ((double)bestCount / sorted.Length, bestLow);
}

public record Scored(string Description, double Satiety, double Density, Dictionary<string, double> Amounts);

public static class Codes
{
    public static readonly string[] Required =
        ["208", "203", "204", "291", "320", "401", "323", "301", "303", "304", "306", "606", "307"];
}

public record Nutrient(
    [property: JsonPropertyName("number")] string Number
);

public record FoodNutrient(
    [property: JsonPropertyName("nutrient")] Nutrient Nutrient,
    [property: JsonPropertyName("amount")] double? Amount
);

public record FoodItem(
    [property: JsonPropertyName("fdcId")] int FdcId,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("foodNutrients")] List<FoodNutrient> FoodNutrients
);

public record SurveyFoodsFile(
    [property: JsonPropertyName("SurveyFoods")] List<FoodItem> Foods
);
