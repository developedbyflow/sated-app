using System.Text.Json;
using System.Text.Json.Serialization;
using Sated.Scoring;

// The last piece of Story 1.12 left undone: nothing checks that a category named in
// calibration.json still exists in the catalogue. The G0 report can only say "neatinsă", and a
// rule reads exactly the same way whether the category was misspelled or is simply absent from
// the 68 benchmark foods. Butter and mayonnaise are the second case today; a typo would be the
// first, and it would silently take a rule out of the engine.
//
// Reading the whole catalogue is what separates them, and that is why this cannot live in the
// gate: the FNDDS file is 63 MB and is not committed.

var calibration = Calibration.Load();

var survey = JsonSerializer.Deserialize<SurveyFoodsFile>(
    File.ReadAllText("../UsdaCoverageQuery/data/surveyDownload.json"))!.Foods;

var catalogue = survey
    .GroupBy(food => food.WweiaFoodCategory?.Description ?? "Not included in a food category")
    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

// The categories the gate's own 68 foods carry, so "the catalogue has it but G0 never sees it"
// stops looking like a problem.
var benchmark = File.ReadAllLines("../../server/Sated.Calibration/benchmark-nutrients.csv")
    .Where(line => line.Length > 0 && !line.StartsWith('#'))
    .Skip(1)
    .Select(SecondField)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

Console.WriteLine($"Calibrare: {calibration.Catalogue}, măsurată {calibration.MeasuredOn}");
Console.WriteLine($"Catalog: {survey.Count} alimente · {catalogue.Count} categorii distincte");
Console.WriteLine($"Setul etalon: {benchmark.Count} categorii distincte");

var lensNames = calibration.Lenses
    .Select(lens => lens.Name)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

var missingCategory = 0;
var missingLens = 0;

Section("Regulile de categorie");
Console.WriteLine($"{"categorie",-42}{"lentilă",-13}{"componentă",-13}{"catalog",9}  stare");

foreach (var rule in calibration.Rules.All
    .OrderBy(rule => rule.Category)
    .ThenBy(rule => rule.LensName)
    .ThenBy(rule => rule.Component.ToString()))
{
    var known = catalogue.TryGetValue(rule.Category, out var foods);

    var state = !known
        ? "LIPSEȘTE DIN CATALOG — nume greșit"
        : benchmark.Contains(rule.Category)
            ? "există · poarta o exercită"
            : "există · poarta n-o vede";

    if (!known)
    {
        missingCategory++;
    }

    if (!lensNames.Contains(rule.LensName))
    {
        missingLens++;
        state += " · LENTILĂ NECUNOSCUTĂ";
    }

    Console.WriteLine($"{Truncate(rule.Category, 42),-42}{Truncate(rule.LensName, 13),-13}" +
        $"{rule.Component,-13}{(known ? foods.ToString() : "—"),9}  {state}");
}

// A category that carries a rule under one lens but not the other is legal — Story 1.8 registers
// each pairing on purpose — but it is also how a half-finished edit looks, so it gets said out loud.
Section("Perechi incomplete");

var lopsided = calibration.Rules.All
    .GroupBy(rule => (rule.Category, rule.Component))
    .Where(group => group.Select(rule => rule.LensName.ToLowerInvariant()).Distinct().Count()
        < lensNames.Count)
    .ToArray();

Console.WriteLine(lopsided.Length == 0
    ? "niciuna: fiecare pereche categorie+componentă are o regulă sub fiecare lentilă"
    : $"{lopsided.Length} perechi acoperă doar o parte din lentile:");

foreach (var group in lopsided)
{
    Console.WriteLine($"  {group.Key.Category} · {group.Key.Component} · " +
        $"doar {string.Join(", ", group.Select(rule => rule.LensName))}");
}

// The six provenance notes in calibration.json are free text: nothing has ever checked them, and
// `required` only guarantees that notes exist, not that they say anything true. Their truth cannot
// be tested. What can be tested is coverage — every rule the file registers should be explained
// somewhere in the notes, because a rule nobody wrote a reason for is a rule nobody can review.
Section("Note de proveniență");

var unexplained = calibration.Rules.All
    .Select(rule => rule.Category)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .Where(category => !calibration.Notes.Any(note =>
        note.Contains(category, StringComparison.OrdinalIgnoreCase)))
    .ToArray();

Console.WriteLine($"note: {calibration.Notes.Count} · categorii cu regulă: " +
    $"{calibration.Rules.All.Select(rule => rule.Category).Distinct(StringComparer.OrdinalIgnoreCase).Count()}");

Console.WriteLine(unexplained.Length == 0
    ? "fiecare categorie cu regulă e numită în cel puțin o notă"
    : $"{unexplained.Length} categorii au regulă și nicio notă care să le numească:");

foreach (var category in unexplained)
{
    Console.WriteLine($"  {category}");
}

Section("Verdict");
Console.WriteLine($"categorii inexistente în catalog: {missingCategory}");
Console.WriteLine($"lentile necunoscute:              {missingLens}");
Console.WriteLine($"reguli fără notă:                 {unexplained.Length}");

if (missingCategory + missingLens + unexplained.Length > 0)
{
    Console.WriteLine();
    Console.WriteLine("PICĂ — o regulă scrisă pe un nume care nu există nu se aplică niciodată, iar una");
    Console.WriteLine("fără notă n-are motiv scris nicăieri. Nimic altceva din proiect nu le semnalează.");
    return 1;
}

Console.WriteLine();
Console.WriteLine("TRECE — fiecare regulă numește o categorie care există și o lentilă calibrată.");
return 0;

void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"── {title} ".PadRight(90, '─'));
}

static string Truncate(string text, int length) =>
    text.Length <= length ? text : text[..length];

// The category column is quoted because half the names carry a comma — "Milk, nonfat". Splitting
// on commas would hand back "Milk and every rule would read as a typo.
static string SecondField(string line)
{
    var afterId = line.AsSpan(line.IndexOf(',') + 1);

    return afterId[0] == '"'
        ? afterId.Slice(1, afterId[1..].IndexOf('"')).ToString()
        : afterId[..afterId.IndexOf(',')].ToString();
}

public record WweiaCategory(
    [property: JsonPropertyName("wweiaFoodCategoryDescription")] string Description
);

public record FoodItem(
    [property: JsonPropertyName("wweiaFoodCategory")] WweiaCategory? WweiaFoodCategory
);

public record SurveyFoodsFile([property: JsonPropertyName("SurveyFoods")] List<FoodItem> Foods);
