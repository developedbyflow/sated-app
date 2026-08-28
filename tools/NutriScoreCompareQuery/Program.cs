using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using NutriScoreCompare;
using Sated.Scoring;

// Answers one question the engine has never been able to answer: is the density score any good?
//
// Satiety has Holt 1995. Density and protein quality have nothing at all, which means that for two
// of the four components the honest answer to "how do you know this is right" is "we do not". This
// tool does not fix that by asserting anything. It measures how often our density agrees with
// Nutri-Score, which is the one external nutrient-density scale that is public, official, and
// already computed over the same kind of food.
//
// Agreement is validation. Disagreement is the more interesting result, because every food the two
// scales rank differently is either a defect worth fixing or a claim worth making in public — and
// this tool cannot tell which, only where to look.
//
// NOTHING IN THE ENGINE CHANGES. This reads and reports.
//
// The comparison is restricted to Nutri-Score's GENERAL branch. Beverages and fats/oils/nuts are
// scored by different rules that this implementation deliberately does not reproduce; see
// NutriScore.cs. The excluded categories are printed, so the exclusion can be argued with.

const string SurveyPath = "../UsdaCoverageQuery/data/surveyDownload.json";
const string SrLegacyPath = "../UsdaCoverageQuery/data/FoodData_Central_sr_legacy_food_json_2018-04.json";

// The worked example whose answer was verified against Santé publique France's own calculator.
// If this implementation cannot reproduce it, every number below is noise and must not be printed.
const int CheddarFdcId = 2705709;
const int CheddarExpectedScore = 16;

// SR Legacy categories that count as fruit, vegetables, legumes, nuts and seeds for the
// produce term. Nut and seed products are in the published definition alongside the rest.
string[] produceCategories =
    ["Fruits and Fruit Juices", "Legumes and Legume Products",
     "Nut and Seed Products", "Vegetables and Vegetable Products"];

// Category families Nutri-Score scores under different rules. Matched on the WWEIA name, the same
// way the engine's own category rules are, and printed below so the list can be disputed.
// Singular AND plural: whole-word matching means "drinks" does not match "drink", and the
// catalogue names its beverage categories in the plural. Getting this wrong put sugar-free energy
// drinks back into a comparison whose whole point is that Nutri-Score scores them elsewhere.
string[] beverageWords =
    ["drink", "drinks", "beverage", "beverages", "water", "waters", "juice", "juices",
     "coffee", "tea", "teas", "beer", "wine", "wines", "liquor", "cocktails",
     "milk", "milks", "smoothie", "smoothies", "soda", "sodas"];

string[] addedFatWords =
    ["oil", "oils", "fat", "fats", "butter", "margarine", "nut", "nuts", "seed", "seeds",
     "dressing", "dressings", "mayonnaise"];

// Cheese is not excluded — it is a general food that takes one documented exception, and it is the
// only food in this catalogue whose Nutri-Score we have independently verified.
string[] cheeseWords = ["cheese"];

// Whole words, never substrings. Matching on substrings excluded "Doughnuts, sweet rolls,
// pastries" as a nut and "White potatoes, baked or boiled" as an oil.
static string[] WordsOf(string category) =>
    category.ToLowerInvariant().Split(
        [' ', ',', '-', '/', '(', ')', '\''], StringSplitOptions.RemoveEmptyEntries);

foreach (var path in new[] { SurveyPath, SrLegacyPath })
{
    if (!File.Exists(path))
    {
        Console.WriteLine($"Lipsește {path} — gitignored, se descarcă separat.");
        return 2;
    }
}

Console.WriteLine("Se citește SR Legacy (201 MB)…");

var srFoods = JsonSerializer.Deserialize<SrLegacyFile>(File.ReadAllText(SrLegacyPath))!.Foods;

var produceByNdb = new HashSet<int>();

foreach (var food in srFoods)
{
    if (food.NdbNumber is { } ndb &&
        food.FoodCategory?.Description is { } category &&
        produceCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
    {
        produceByNdb.Add(ndb);
    }
}

Console.WriteLine($"SR Legacy: {srFoods.Count} alimente · " +
    $"{produceByNdb.Count} din categoriile de fructe/legume/leguminoase/nuci.");

Console.WriteLine("Se citește FNDDS (63 MB)…");

var survey = JsonSerializer.Deserialize<SurveyFoodsFile>(File.ReadAllText(SurveyPath))!.Foods;

var shipped = Calibration.Load();

Console.WriteLine($"FNDDS: {survey.Count} alimente · calibrare {shipped.Catalogue}, " +
    $"măsurată {shipped.MeasuredOn}");

var rows = new List<Row>();
var excludedCategories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
var skippedNoNutrients = 0;
var skippedExcluded = 0;
NutriScore.Result? cheddar = null;

foreach (var food in survey)
{
    var amounts = food.FoodNutrients
        .Where(entry => entry.Amount is not null)
        .ToDictionary(entry => entry.Nutrient.Number, entry => entry.Amount!.Value);

    if (!Codes.Required.All(amounts.ContainsKey))
    {
        skippedNoNutrients++;
        continue;
    }

    var category = food.WweiaFoodCategory?.Description ?? "Not included in a food category";
    var words = WordsOf(category);
    var isCheese = cheeseWords.Any(words.Contains);

    if (!isCheese && (beverageWords.Any(words.Contains) || addedFatWords.Any(words.Contains)))
    {
        excludedCategories.Add(category);
        skippedExcluded++;
        continue;
    }

    // Ingredient weights are grams per 100 g of the finished food, so the sum is already a
    // percentage. A food whose recipe does not resolve simply reads low, the same way the
    // leucine join treats a partial recipe.
    var produceShare = food.InputFoods
        .Where(part => part.IngredientCode is { } code && produceByNdb.Contains(code))
        .Sum(part => part.IngredientWeight);

    var theirs = NutriScore.Calculate(new NutriScore.Input(
        CaloriesPer100g: amounts["208"],
        SugarsG: amounts["269"],
        SaturatedFatG: amounts["606"],
        SodiumMg: amounts["307"],
        ProteinG: amounts["203"],
        FibreG: amounts["291"],
        ProduceSharePercent: produceShare), isCheese);

    var density = new DensityInput(
        amounts["208"], amounts["203"], amounts["291"], amounts["320"], amounts["401"],
        amounts["323"], amounts["301"], amounts["303"], amounts["304"], amounts["306"],
        amounts["606"], amounts["307"], amounts["328"], amounts["404"]);

    var raw = DensityScore.Calculate(density, DensityScore.Nrf92);

    if (raw is null)
    {
        continue;
    }

    rows.Add(new Row(
        food.FdcId,
        food.Description,
        category,
        shipped.DensityScale.Normalize(raw.Value),
        theirs.Score,
        theirs.Grade));
}

// The cheese categories are excluded from the comparison, so the check is run on its own.
var cheddarFood = survey.FirstOrDefault(food => food.FdcId == CheddarFdcId);

if (cheddarFood is not null)
{
    var amounts = cheddarFood.FoodNutrients
        .Where(entry => entry.Amount is not null)
        .ToDictionary(entry => entry.Nutrient.Number, entry => entry.Amount!.Value);

    cheddar = NutriScore.Calculate(new NutriScore.Input(
        amounts["208"], amounts["269"], amounts["606"], amounts["307"],
        amounts["203"], amounts["291"], 0), isCheese: true);
}

Section("Verificare — cheddarul cu răspuns cunoscut");

if (cheddar is null)
{
    Console.WriteLine($"Alimentul {CheddarFdcId} nu e în catalog. Verificarea nu s-a putut rula.");
    Console.WriteLine("Fără ea, cifrele de mai jos n-au acoperire. Mă opresc.");
    return 1;
}

Console.WriteLine($"FNDDS {CheddarFdcId} · aşteptat {CheddarExpectedScore} (D) · " +
    $"obţinut {cheddar.Score} ({cheddar.Grade})");
Console.WriteLine($"  {cheddar.Breakdown}");

// Exact, not within a tolerance. A check that passes by slack is a check that cannot fail for the
// right reason, and this one exists precisely so the numbers below are allowed to be printed.
if (cheddar.Score != CheddarExpectedScore)
{
    Console.WriteLine();
    Console.WriteLine("NU SE POTRIVEȘTE. Implementarea nu reproduce exemplul verificat oficial.");
    Console.WriteLine("Nu public cifre dintr-o formulă care nu trece propriul test.");
    return 1;
}

Console.WriteLine("  Exact. Cifrele de mai jos au acoperire.");

Section("Ce s-a comparat");

Console.WriteLine($"comparate: {rows.Count}");
Console.WriteLine($"excluse — altă ramură Nutri-Score: {skippedExcluded} " +
    $"în {excludedCategories.Count} categorii");
Console.WriteLine($"excluse — fără nutrienţii necesari: {skippedNoNutrients}");
Console.WriteLine();
Console.WriteLine("Categoriile excluse, ca să poată fi contestate:");

foreach (var category in excludedCategories)
{
    Console.WriteLine($"  {category}");
}

Section("Cât de des suntem de acord");

// Nutri-Score is lower-is-better and our density is higher-is-better, so a negative correlation
// means agreement. Negated on printing, because "0.7" reads and "-0.7" invites a misreading.
var correlation = -Spearman(
    rows.Select(row => row.OurDensity).ToArray(),
    rows.Select(row => (double)row.TheirScore).ToArray());

Console.WriteLine($"Corelaţie de rang (Spearman): {correlation:F3}");
Console.WriteLine();
Console.WriteLine("  1,0 = ordonăm alimentele identic · 0 = niciun acord · negativ = ordonăm invers");

var quintiles = rows.OrderBy(row => row.OurDensity).ToArray();
var letters = new[] { 'A', 'B', 'C', 'D', 'E' };
var matrix = new int[5, 5];

for (var index = 0; index < quintiles.Length; index++)
{
    var ourBand = Math.Min(4, index * 5 / quintiles.Length);
    var theirBand = Array.IndexOf(letters, quintiles[index].TheirGrade);
    matrix[ourBand, theirBand]++;
}

Console.WriteLine();
Console.WriteLine("Cincimea noastră de densitate (E jos … A sus) contra literei Nutri-Score:");
Console.WriteLine();
Console.WriteLine("            A      B      C      D      E");

for (var our = 4; our >= 0; our--)
{
    Console.Write($"  {letters[4 - our]} noi ");

    for (var their = 0; their < 5; their++)
    {
        Console.Write($"{matrix[our, their],6} ");
    }

    Console.WriteLine();
}

Section("Unde nu suntem de acord — de citit una câte una");

var ourRank = new Dictionary<int, double>();

for (var index = 0; index < quintiles.Length; index++)
{
    ourRank[quintiles[index].FdcId] = (double)index / quintiles.Length * 100;
}

var theirRanked = rows.OrderByDescending(row => row.TheirScore).ToArray();
var theirRank = new Dictionary<int, double>();

for (var index = 0; index < theirRanked.Length; index++)
{
    theirRank[theirRanked[index].FdcId] = (double)index / theirRanked.Length * 100;
}

var disagreements = rows
    .Select(row => (Row: row, Gap: ourRank[row.FdcId] - theirRank[row.FdcId]))
    .ToArray();

PrintGap("Noi le notăm MULT mai bine decât Nutri-Score",
    disagreements.OrderByDescending(entry => entry.Gap).Take(12));

PrintGap("Nutri-Score le notează MULT mai bine decât noi",
    disagreements.OrderBy(entry => entry.Gap).Take(12));

Section("Verdict");

var big = disagreements.Count(entry => Math.Abs(entry.Gap) > 40);

Console.WriteLine($"Corelaţie {correlation:F3} pe {rows.Count} alimente.");
Console.WriteLine($"Dezacorduri mari (peste 40 de puncte de rang): {big} " +
    $"({(double)big / rows.Count * 100:F1}%)");
Console.WriteLine();

if (correlation >= 0.7)
{
    Console.WriteLine("Densitatea e de acord cu Nutri-Score pe ansamblu.");
    Console.WriteLine("Asta e validarea externă care lipsea. Dezacordurile de mai sus sunt");
    Console.WriteLine("materialul: fiecare e ori un defect, ori un argument de produs.");
}
else if (correlation >= 0.4)
{
    Console.WriteLine("Acord parţial. Nu e o validare, dar nici o contradicţie.");
    Console.WriteLine("Dezacordurile de mai sus trebuie citite înainte de orice afirmaţie publică.");
}
else
{
    Console.WriteLine("ACORD SLAB. Densitatea ordonează catalogul altfel decât Nutri-Score.");
    Console.WriteLine("Nu afirma nimic public despre densitate până nu se înţelege de ce.");
}

return 0;

void PrintGap(string title, IEnumerable<(Row Row, double Gap)> entries)
{
    Console.WriteLine();
    Console.WriteLine($"  {title}:");

    foreach (var (row, gap) in entries)
    {
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"    {gap,6:F0}  noi {row.OurDensity,5:F1}  ei {row.TheirGrade}  " +
            $"{Truncate(row.Description, 58)}"));
    }
}

void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"── {title} ".PadRight(100, '─'));
}

static string Truncate(string text, int length) =>
    text.Length <= length ? text : text[..length];

// Ranks with ties averaged, then Pearson on the ranks. Ties matter here: Nutri-Score is an integer
// score, so hundreds of foods share one value, and ranking them arbitrarily would invent an order.
static double Spearman(double[] left, double[] right)
{
    var a = Ranks(left);
    var b = Ranks(right);

    var meanA = a.Average();
    var meanB = b.Average();

    var covariance = 0.0;
    var varianceA = 0.0;
    var varianceB = 0.0;

    for (var index = 0; index < a.Length; index++)
    {
        var da = a[index] - meanA;
        var db = b[index] - meanB;

        covariance += da * db;
        varianceA += da * da;
        varianceB += db * db;
    }

    return covariance / Math.Sqrt(varianceA * varianceB);
}

static double[] Ranks(double[] values)
{
    var order = values
        .Select((value, index) => (value, index))
        .OrderBy(entry => entry.value)
        .ToArray();

    var ranks = new double[values.Length];
    var position = 0;

    while (position < order.Length)
    {
        var last = position;

        while (last + 1 < order.Length && order[last + 1].value == order[position].value)
        {
            last++;
        }

        var average = (position + last) / 2.0;

        for (var index = position; index <= last; index++)
        {
            ranks[order[index].index] = average;
        }

        position = last + 1;
    }

    return ranks;
}

record Row(int FdcId, string Description, string Category, double OurDensity, int TheirScore, char TheirGrade);

public static class Codes
{
    public static readonly string[] Required =
        ["208", "203", "204", "291", "269", "320", "401", "323", "301", "303", "304", "306",
         "606", "307", "328", "404"];
}

public record Nutrient([property: JsonPropertyName("number")] string Number);

public record FoodNutrient(
    [property: JsonPropertyName("nutrient")] Nutrient Nutrient,
    [property: JsonPropertyName("amount")] double? Amount);

public record WweiaCategory(
    [property: JsonPropertyName("wweiaFoodCategoryDescription")] string Description);

public record InputFood(
    [property: JsonPropertyName("ingredientCode")] int? IngredientCode,
    [property: JsonPropertyName("ingredientWeight")] double IngredientWeight);

public record FoodItem(
    [property: JsonPropertyName("fdcId")] int FdcId,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("foodNutrients")] List<FoodNutrient> FoodNutrients,
    [property: JsonPropertyName("wweiaFoodCategory")] WweiaCategory? WweiaFoodCategory,
    [property: JsonPropertyName("inputFoods")] List<InputFood> InputFoods);

public record SurveyFoodsFile([property: JsonPropertyName("SurveyFoods")] List<FoodItem> Foods);

public record SrCategory([property: JsonPropertyName("description")] string Description);

public record SrFood(
    [property: JsonPropertyName("ndbNumber")] int? NdbNumber,
    [property: JsonPropertyName("foodCategory")] SrCategory? FoodCategory);

public record SrLegacyFile([property: JsonPropertyName("SRLegacyFoods")] List<SrFood> Foods);
