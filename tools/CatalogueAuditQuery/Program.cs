using System.Text.Json;
using System.Text.Json.Serialization;
using Sated.Scoring;

// Sweeps every food in FNDDS on every lens and counts the outputs a user would call broken.
// Not a test of whether the formula is true — nobody can test that. A test of whether any grade
// the product will ever show is indefensible on its face. The catalogue is finite and frozen,
// so this question has an exact answer rather than a sample of one.
//
// Each audit below has to be able to fail on today's engine or pass on a broken one, or it is
// not a criterion. Every one of them was seen failing before it was written.

var shipped = Calibration.Load();
var combiner = new ScoreCombiner(
    new GeneralStrategies(shipped.SatietyScale, shipped.DensityScales, shipped.ReferenceMealGrams),
    shipped.Rules);

var foods = JsonSerializer.Deserialize<SurveyFoodsFile>(
    File.ReadAllText("../UsdaCoverageQuery/data/surveyDownload.json"))!.Foods;

var rows = new List<Row>();
var ruled = new Dictionary<string, bool>();

foreach (var food in foods)
{
    var amounts = food.FoodNutrients.Where(e => e.Amount is not null)
        .ToDictionary(e => e.Nutrient.Number, e => e.Amount!.Value);

    if (!Codes.Required.All(amounts.ContainsKey)) continue;

    var input = new FoodInput(
        Category: food.WweiaFoodCategory?.Description ?? "Not included in a food category",
        Calories: amounts["208"], Protein: amounts["203"], Fat: amounts["204"],
        Fiber: amounts["291"], VitaminA: amounts["320"], VitaminC: amounts["401"],
        VitaminE: amounts["323"], Calcium: amounts["301"], Iron: amounts["303"],
        Magnesium: amounts["304"], Potassium: amounts["306"], SaturatedFat: amounts["606"],
        Sodium: amounts["307"], VitaminD: amounts["328"], Thiamine: amounts["404"]);

    var combined = shipped.Lenses.Select(lens => combiner.Combine(input, lens)).ToArray();
    var scores = combined.Select(c => c.Value).ToArray();
    var grades = combined.Select((c, i) => shipped.GradeFor(c, shipped.Lenses[i])).ToArray();

    rows.Add(new Row(food.Description, input, scores, grades));
    ruled[food.Description] = shipped.Lenses.Any(lens => shipped.Rules.Has(input.Category, lens));
}

var lenses = shipped.Lenses;
var violations = 0;

Console.WriteLine($"Audit pe {rows.Count} alimente × {lenses.Length} lentile = " +
    $"{rows.Count * lenses.Length} note\n");

// ---------------------------------------------------------------- A1
// Every score in this engine is a quantity per calorie, so a food with no calories and no
// macronutrients has no answer to give. It must carry no letter at all. Any letter is a claim —
// A says eat this, E says avoid it — and for tap water every one of them is false.
var empty = rows.Where(r => ProfileRules.IsNutritionallyEmpty(r.Food)).ToArray();
var lettered = empty.SelectMany(r => r.Grades.Where(g => g is not null)).Count();

violations += lettered;
Console.WriteLine($"A1 · alimente fără calorii și fără macro: {empty.Length} în catalog, " +
    $"{lettered} cu literă");

// ---------------------------------------------------------------- A2
// The lighter version of a product must not score below the one it is the lighter version of.
// Narrower than it first read: "light" in a description is a marketing word, and a fat-free
// cheese really can be worse than the cheese — less protein, more salt. So the pair only counts
// when the claim is checkable on the numbers: fewer calories, no less protein, no more sodium.
// Everything softer than that is a judgement, and A3 already proves the cases that can be proved.
string[] reducedMarkers =
    ["diet", "sugar free", "sugar-free", "low calorie", "reduced sugar", "zero", "light",
     "unsweetened", "no sugar added", "nonfat", "fat free", "fat-free", "lowfat", "low fat"];

var graded = rows.Where(r => r.Grades.Any(g => g is not null)).ToArray();

var families = graded
    .GroupBy(r => Family(r.Description, reducedMarkers))
    .Where(g => g.Count() > 1)
    .ToArray();

var inversions = new List<(string Reduced, string Full, string Lens, double Rd, double Fl)>();

foreach (var family in families)
{
    var reduced = family.Where(r => IsReduced(r.Description, reducedMarkers)).ToArray();
    var full = family.Where(r => !IsReduced(r.Description, reducedMarkers)).ToArray();

    for (var l = 0; l < lenses.Length; l++)
    {
        foreach (var r in reduced)
        {
            foreach (var f in full)
            {
                var lighterOnTheNumbers =
                    r.Food.Calories < f.Food.Calories
                    && r.Food.Protein >= f.Food.Protein
                    && r.Food.Sodium <= f.Food.Sodium;

                // Same rule as A3: what ships is the letter. Two foods that differ by two points
                // inside one letter are the same letter to the reader.
                if (lighterOnTheNumbers
                    && r.Scores[l] < f.Scores[l]
                    && r.Grades[l] > f.Grades[l])
                {
                    inversions.Add((r.Description, f.Description, lenses[l].Name,
                        r.Scores[l], f.Scores[l]));
                }
            }
        }
    }
}

violations += inversions.Count;
Console.WriteLine($"\nA2 · varianta mai ușoară cu literă mai proastă: {inversions.Count} " +
    $"(din {families.Length} familii)");

foreach (var i in inversions.OrderBy(i => i.Rd - i.Fl).Take(8))
{
    Console.WriteLine($"   {i.Lens,-12} {i.Rd,5:F1} < {i.Fl,5:F1}  {Short(i.Reduced)}");
}

// ---------------------------------------------------------------- A3
// If a food is better or equal on every single number the engine reads, and strictly better on
// one, it cannot score lower. A violation means some component is not monotone in its own input —
// which is where a calorie floor or a ratio denominator inverts a food behind the formula's back.
//
// A drink and a solid are not compared, and that is a limit of the criterion rather than a hole
// in it. Liquidity is not a nutrient, so two foods with identical numbers really can differ, and
// the engine says so deliberately: liquid calories are poorly compensated, which is the whole of
// FR-6's drink rule. Counting that as an inversion would report a documented modelling decision
// as a defect. Within each class the test is unchanged, and it still catches what it was written
// for — honey mustard dip against regular mayonnaise, both solids.
var dominance = new List<(string Better, string Worse, string Lens, double Bs, double Ws)>();
var visible = new List<(string Better, string Worse, string Lens, Grade Bg, Grade Wg)>();

for (var a = 0; a < graded.Length; a++)
{
    for (var b = 0; b < graded.Length; b++)
    {
        if (a == b) continue;
        if (ruled[graded[a].Description] != ruled[graded[b].Description]) continue;
        if (!Dominates(graded[a].Food, graded[b].Food)) continue;

        for (var l = 0; l < lenses.Length; l++)
        {
            if (graded[a].Grades[l] is null || graded[b].Grades[l] is null) continue;

            if (graded[a].Scores[l] < graded[b].Scores[l] - 0.01)
            {
                dominance.Add((graded[a].Description, graded[b].Description, lenses[l].Name,
                    graded[a].Scores[l], graded[b].Scores[l]));

                // What ships is the letter. A score inversion inside one letter is a fact about
                // the engine's monotonicity; an inversion that crosses a cutoff is the thing a
                // reader can see and screenshot. They are not the same finding and counting them
                // together hid how small the second one is.
                if (graded[a].Grades[l] > graded[b].Grades[l])
                {
                    visible.Add((graded[a].Description, graded[b].Description, lenses[l].Name,
                        graded[a].Grades[l]!.Value, graded[b].Grades[l]!.Value));
                }
            }
        }
    }
}

violations += visible.Count;
Console.WriteLine($"\nA3 · dominanță încălcată: {dominance.Count} pe scor, " +
    $"din care {visible.Count} TRAVERSEAZĂ o literă");

foreach (var v in visible.Take(10))
{
    Console.WriteLine($"   {v.Lens,-12} {v.Bg} < {v.Wg}  {Short(v.Better)}");
    Console.WriteLine($"   {"",-12}        ↑ deși domină: {Short(v.Worse)}");
}

// ---------------------------------------------------------------- A4
// Every component and every combined score inside the range the contract promises.
var outOfRange = 0;

foreach (var row in rows)
{
    for (var l = 0; l < lenses.Length; l++)
    {
        if (row.Scores[l] < 0 || row.Scores[l] > 100) outOfRange++;
    }
}

violations += outOfRange;
Console.WriteLine($"\nA4 · scoruri în afara intervalului 0-100: {outOfRange}");

// ---------------------------------------------------------------- A5
// Reported, not counted, and the reason is written here rather than left to be rediscovered.
// A grade is a property of a food per 100 g and ComponentStrategy says so on purpose: how much
// was eaten cannot change a letter. Checked by hand, the arithmetic behind these is right —
// 100 g of cocoa powder really does carry that much fibre, magnesium and iron per calorie.
// What is wrong is the comparison a reader makes, between a powder whose FNDDS portion is 5 g
// and a fish whose portion is 200 g. That is the portion decision, D6, and it is already open.
// Forcing these to carry no letter would be worse, not better: somebody logging 5 g of cocoa in
// a recipe would get nothing back for a food the engine reads correctly.
string[] dryMarkers = ["not reconstituted", "dry mix", ", dry", "powder", "dehydrated", ", dried"];

// D6's data, so this reports a fact rather than an impression. Generated by
// tools/TypicalPortionQuery from FNDDS's own Quantity-not-specified amounts.
var portionFile = "../TypicalPortionQuery/typical-portions.csv";
var portions = File.Exists(portionFile)
    ? File.ReadAllLines(portionFile).Where(l => !l.StartsWith('#')).Skip(1)
        .Select(l => l.Split(',')).Where(c => c.Length >= 4)
        .GroupBy(c => string.Join(',', c.Skip(3)).Trim('"'))
        .ToDictionary(g => g.Key, g => double.Parse(g.First()[1],
            System.Globalization.CultureInfo.InvariantCulture))
    : [];

var dryTop = rows.Where(r =>
    dryMarkers.Any(m => r.Description.Contains(m, StringComparison.OrdinalIgnoreCase)) &&
    r.Grades[0] == Grade.A).ToArray();

Console.WriteLine($"\nA5 · pudre notate A pe {lenses[0].Name}: {dryTop.Length} " +
    $"— RAPORTAT, nu numărat: aritmetica e corectă per 100 g (D6)");
Console.WriteLine($"   {"",5}  {"porție",7}");

foreach (var d in dryTop.OrderByDescending(d => d.Scores[0]).Take(6))
{
    var grams = portions.TryGetValue(d.Description, out var g) ? $"{g,5:F0} g" : "     ?";
    Console.WriteLine($"   {d.Scores[0],5:F1}  {grams,7}  {Short(d.Description)}");
}

// ----------------------------------------------------------------
Console.WriteLine($"\n{new string('─', 62)}");
Console.WriteLine(violations == 0
    ? "AUDIT CURAT · nicio notă indefensabilă"
    : $"AUDIT PICAT · {violations} note indefensabile");

return violations == 0 ? 0 : 1;

static string Short(string s) => s.Length <= 52 ? s : s[..52];

static bool IsReduced(string description, string[] markers) =>
    markers.Any(m => description.Contains(m, StringComparison.OrdinalIgnoreCase));

// The product minus the word that marks it as the reduced version. Two descriptions that collapse
// to the same string are the same product at two sugar or fat levels.
static string Family(string description, string[] markers)
{
    var s = description.ToLowerInvariant();

    foreach (var m in markers)
    {
        s = s.Replace(m, " ");
    }

    return string.Join(' ', s.Split([' ', ',', '(', ')', '.'],
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

// Better or equal on every number the engine reads, strictly better on at least one.
static bool Dominates(FoodInput a, FoodInput b)
{
    var strict = false;

    bool Up(double x, double y)
    {
        if (x < y) return false;
        if (x > y) strict = true;
        return true;
    }

    bool Down(double x, double y)
    {
        if (x > y) return false;
        if (x < y) strict = true;
        return true;
    }

    if (!Down(a.Calories, b.Calories)) return false;
    if (!Down(a.Fat, b.Fat)) return false;
    if (!Down(a.SaturatedFat, b.SaturatedFat)) return false;
    if (!Down(a.Sodium, b.Sodium)) return false;
    if (!Up(a.Protein, b.Protein)) return false;
    if (!Up(a.Fiber, b.Fiber)) return false;
    if (!Up(a.VitaminA!.Value, b.VitaminA!.Value)) return false;
    if (!Up(a.VitaminC!.Value, b.VitaminC!.Value)) return false;
    if (!Up(a.VitaminE!.Value, b.VitaminE!.Value)) return false;
    if (!Up(a.Calcium!.Value, b.Calcium!.Value)) return false;
    if (!Up(a.Iron!.Value, b.Iron!.Value)) return false;
    if (!Up(a.Magnesium!.Value, b.Magnesium!.Value)) return false;
    if (!Up(a.Potassium!.Value, b.Potassium!.Value)) return false;
    if (!Up(a.VitaminD!.Value, b.VitaminD!.Value)) return false;
    if (!Up(a.Thiamine!.Value, b.Thiamine!.Value)) return false;

    return strict;
}

public record Row(string Description, FoodInput Food, double[] Scores, Grade?[] Grades);

public static class Codes
{
    public static readonly string[] Required =
        ["208","203","204","291","320","401","323","301","303","304","306","606","307","328","404"];
}
public record Nutrient([property: JsonPropertyName("number")] string Number);
public record FoodNutrient([property: JsonPropertyName("nutrient")] Nutrient Nutrient,
    [property: JsonPropertyName("amount")] double? Amount);
public record WweiaCategory([property: JsonPropertyName("wweiaFoodCategoryDescription")] string Description);
public record FoodItem(
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("foodNutrients")] FoodNutrient[] FoodNutrients,
    [property: JsonPropertyName("wweiaFoodCategory")] WweiaCategory? WweiaFoodCategory);
public record SurveyFoodsFile(
    [property: JsonPropertyName("SurveyFoods")] FoodItem[] Foods);
