using System.Text.Json;
using System.Text.Json.Serialization;
using Sated.Scoring;

// Asks whether the fat rule could key on the profile instead of the category. The category is
// only a proxy: the real condition is how much of a food's energy comes from fat, which an
// aggregate and a user-added food both have and neither has a WWEIA category for.
// The question that decides it: is there a clean gap in the distribution, or would the cutoff
// still be chosen? Predictions P1-P4 were written before this ran.

var json = File.ReadAllText("../UsdaCoverageQuery/data/surveyDownload.json");
var foods = JsonSerializer.Deserialize<SurveyFoodsFile>(json)!.Foods;

// Mirrors CategoryRules.Standard, whose list is private.
string[] fatCategories =
    ["Salad dressings and vegetable oils", "Butter and animal fats", "Margarine", "Mayonnaise"];

var scored = new List<Entry>();

foreach (var food in foods)
{
    var amounts = food.FoodNutrients
        .Where(entry => entry.Amount is not null)
        .ToDictionary(entry => entry.Nutrient.Number, entry => entry.Amount!.Value);

    if (!amounts.TryGetValue("208", out var calories) || calories <= 0
        || !amounts.TryGetValue("204", out var fat))
    {
        continue;
    }

    var category = food.WweiaFoodCategory?.Description ?? "Not included in a food category";

    scored.Add(new Entry(
        food.Description,
        category,
        Math.Clamp(fat * 9 / calories, 0, 1),
        fatCategories.Contains(category, StringComparer.OrdinalIgnoreCase)));
}

Console.WriteLine($"Alimente cu calorii > 0: {scored.Count}");

Section("P1 — cele patru categorii de grăsime");
Console.WriteLine($"{"categorie",-38} {"n",5} {"p10",7} {"mediană",8} {"p90",7}");
foreach (var category in fatCategories)
{
    var shares = scored
        .Where(e => string.Equals(e.Category, category, StringComparison.OrdinalIgnoreCase))
        .Select(e => e.FatShare).ToArray();

    Console.WriteLine($"{category,-38} {shares.Length,5} {Percentile(shares, 10),7:F3} " +
        $"{Percentile(shares, 50),8:F3} {Percentile(shares, 90),7:F3}");
}
Console.WriteLine("Prezis: mediană > 0,90 la toate; dressingurile cu p10 < 0,70.");

Section("P2 — alimente peste 0,90 care NU sunt într-o categorie de grăsime");
var outsiders = scored.Where(e => !e.IsFatCategory && e.FatShare > 0.90).ToArray();
Console.WriteLine($"{outsiders.Length} alimente (prezis > 50)");
foreach (var e in outsiders.OrderByDescending(e => e.FatShare).Take(12))
{
    Console.WriteLine($"  {Truncate(e.Description, 44),-46} {e.FatShare:F3}  {Truncate(e.Category, 28)}");
}

Section("P3 — există un gol în distribuție?");
Console.WriteLine($"{"bandă",14} {"toate",8} {"în cat. grăsime",16} {"în afara lor",13}");
foreach (var (low, high) in new[] { (0.70, 0.75), (0.75, 0.80), (0.80, 0.85), (0.85, 0.90), (0.90, 0.95), (0.95, 0.99), (0.99, 1.01) })
{
    var band = scored.Where(e => e.FatShare >= low && e.FatShare < high).ToArray();
    Console.WriteLine($"{low:F2}–{high:F2}".PadLeft(14) +
        $" {band.Length,8} {band.Count(e => e.IsFatCategory),16} {band.Count(e => !e.IsFatCategory),13}");
}
Console.WriteLine("Prezis: nicio bandă goală între 0,80 şi 0,95.");

Section("P4 — câte alimente ar schimba componenta de densitate");
foreach (var cutoff in new[] { 0.85, 0.90, 0.95, 0.99 })
{
    var gained = scored.Count(e => !e.IsFatCategory && e.FatShare >= cutoff);
    var lost = scored.Count(e => e.IsFatCategory && e.FatShare < cutoff);

    Console.WriteLine($"prag {cutoff:F2}: {gained,4} ar căpăta regula · {lost,4} ar pierde-o · " +
        $"total mişcat {gained + lost,4}");
}
Console.WriteLine("Prezis: peste 100 la pragul 0,90.");

Section("Alimentele numite");
foreach (var needle in new[] { "Olive oil", "Butter, stick", "Margarine, NFS", "Mayonnaise, regular", "Cream, heavy", "Bacon", "Walnuts", "Avocado, raw", "Cheese, Cheddar", "Salad dressing, ranch", "Salad dressing, italian, reduced" })
{
    var match = scored.FirstOrDefault(e =>
        e.Description.StartsWith(needle, StringComparison.OrdinalIgnoreCase));

    Console.WriteLine(match is null
        ? $"  {needle,-40} negăsit"
        : $"  {Truncate(match.Description, 40),-42} {match.FatShare:F3} {(match.IsFatCategory ? "[categorie grăsime]" : "")}");
}

void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"── {title} ".PadRight(88, '─'));
}

static string Truncate(string text, int length) =>
    text.Length <= length ? text : text[..length];

static double Percentile(double[] values, double percentile)
{
    var sorted = values.Order().ToArray();
    var position = percentile / 100 * (sorted.Length - 1);
    var lower = (int)Math.Floor(position);
    var upper = (int)Math.Ceiling(position);

    return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
}

public record Entry(string Description, string Category, double FatShare, bool IsFatCategory);

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
