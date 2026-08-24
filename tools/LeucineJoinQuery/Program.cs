using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

// Closes the oldest hole in the engine: every protein score ever produced has rested on a guessed
// leucine share, because FNDDS carries no amino acid data at all. SR Legacy does, for 5,083 foods,
// and FNDDS foods carry their own recipe — each ingredient with an ndbNumber and a weight. That is
// a real join, not a match on names.
//
// What is measured here is the leucine share OF PROTEIN, not leucine per 100 g. That is the exact
// quantity ProteinCompleteness guesses today as 8.8% animal / 7.1% plant, and it is the robust one:
// a share is a property of the protein, so a recipe that only partly resolves still yields a usable
// number, while an absolute amount would silently come out low.

const double CoverFloor = 0.5;      // reconstructed protein against the food's own stated protein
const double CoverCeiling = 1.5;    // outside this band the recipe did not resolve well enough
const int MinFoodsPerCategory = 3;  // below this a category median is one food wearing a disguise
const int MinFoodsPerGroup = 3;     // the same guard one level up, where it never actually bites

var sr = JsonSerializer.Deserialize<SrLegacyFile>(
    File.ReadAllText("../UsdaCoverageQuery/data/FoodData_Central_sr_legacy_food_json_2018-04.json"))!.Foods;

var ingredients = new Dictionary<int, Ingredient>();

foreach (var food in sr.Where(food => food.NdbNumber is not null))
{
    var amounts = food.FoodNutrients
        .Where(entry => entry.Amount is not null)
        .ToDictionary(entry => entry.Nutrient.Number, entry => entry.Amount!.Value);

    if (amounts.TryGetValue("504", out var leucine) && amounts.TryGetValue("203", out var protein))
    {
        ingredients[food.NdbNumber!.Value] = new Ingredient(protein, leucine);
    }
}

Console.WriteLine($"SR Legacy: {sr.Count} alimente · {ingredients.Count} cu leucină ȘI proteină.");

var survey = JsonSerializer.Deserialize<SurveyFoodsFile>(
    File.ReadAllText("../UsdaCoverageQuery/data/surveyDownload.json"))!.Foods;

Console.WriteLine($"FNDDS: {survey.Count} alimente · " +
    $"{survey.Count(food => food.InputFoods.Count > 0)} cu rețetă.");

var measured = new List<Measured>();

foreach (var food in survey)
{
    var stated = food.FoodNutrients
        .FirstOrDefault(entry => entry.Nutrient.Number == "203" && entry.Amount is not null)?.Amount;

    if (stated is null or <= 0)
    {
        continue;
    }

    var protein = 0.0;
    var leucine = 0.0;

    foreach (var part in food.InputFoods)
    {
        if (part.IngredientCode is not null &&
            ingredients.TryGetValue(part.IngredientCode.Value, out var ingredient))
        {
            // Weights are grams per 100 g of the finished food, so dividing by 100 turns a
            // per-100 g nutrient into the grams that ingredient contributes.
            protein += ingredient.Protein * part.IngredientWeight / 100;
            leucine += ingredient.Leucine * part.IngredientWeight / 100;
        }
    }

    if (protein <= 0)
    {
        continue;
    }

    measured.Add(new Measured(
        food.FdcId,
        food.Description,
        food.WweiaFoodCategory?.Description ?? "Not included in a food category",
        FoodGroup: MajorGroupOf(food.FoodCode),
        Share: leucine / protein,
        Cover: protein / stated.Value,
        StatedProtein: stated.Value));
}

var trusted = measured
    .Where(food => food.Cover >= CoverFloor && food.Cover <= CoverCeiling)
    .ToArray();

Section("Acoperirea");
Console.WriteLine($"cotă calculabilă            {measured.Count,5} " +
    $"{(double)measured.Count / survey.Count,7:P1}");
Console.WriteLine($"din care bine reconstruite  {trusted.Length,5} " +
    $"{(double)trusted.Length / survey.Count,7:P1}   (proteina refăcută e " +
    $"{CoverFloor:P0}-{CoverCeiling:P0} din cea raportată)");

// The engine never sees an fdcId, only a category, so a food outside the catalogue build still
// needs an answer. A category's own measured median is that answer — and unlike the two constants
// it replaces, it was read off the data rather than chosen.
var byCategory = trusted
    .GroupBy(food => food.Category)
    .Where(group => group.Count() >= MinFoodsPerCategory)
    .Select(group => (group.Key, Share: Median([.. group.Select(food => food.Share)])))
    // A median of exactly zero says every measured member reported protein and no leucine, which
    // the source contradicts rather than asserts. Beer is the only category this drops.
    .Where(entry => entry.Share > 0)
    .ToDictionary(entry => entry.Key, entry => entry.Share);

// One level up from the category, for the 524 foods whose category never reached three. The first
// digit of an FNDDS food code is USDA's major food group — 1 milk, 2 meat, 3 eggs, 4 legumes and
// nuts, 5 grains, 6 fruit, 7 vegetables, 8 fats, 9 sugars and beverages — so this is a published
// taxonomy, not a bucket invented here. It is coarse, and it is still an estimate; what it is not
// is one number for every food the categories missed. Whey and casein put dairy at 9.5% while fruit
// sits at 4.6%, and the retired constant handed both 7.52%.
var byGroup = trusted
    .Where(food => food.FoodGroup.Length > 0)
    .GroupBy(food => food.FoodGroup)
    .Where(group => group.Count() >= MinFoodsPerGroup)
    .Select(group => (group.Key, Share: Median([.. group.Select(food => food.Share)])))
    .Where(entry => entry.Share > 0)
    .ToDictionary(entry => entry.Key, entry => entry.Share);

// Counted over the whole catalogue, not over the foods whose recipe happened to resolve: a food
// with no resolvable ingredients still stops being a pure guess once its category has a median.
var trustedIds = trusted.Select(food => food.FdcId).ToHashSet();

var covered = survey.Count(food =>
    trustedIds.Contains(food.FdcId) ||
    byCategory.ContainsKey(food.WweiaFoodCategory?.Description ?? "Not included in a food category"));

var coveredWithGroup = survey.Count(food =>
    trustedIds.Contains(food.FdcId) ||
    byCategory.ContainsKey(food.WweiaFoodCategory?.Description ?? "Not included in a food category") ||
    byGroup.ContainsKey(MajorGroupOf(food.FoodCode)));

Console.WriteLine($"categorii cu ≥{MinFoodsPerCategory} măsurate    {byCategory.Count,5} " +
    $"din {measured.Select(food => food.Category).Distinct().Count()}");
Console.WriteLine($"acoperite, direct sau prin categorie {covered,5} " +
    $"{(double)covered / survey.Count,7:P1}");
Console.WriteLine($"plus rezerva pe grupă majoră         {coveredWithGroup,5} " +
    $"{(double)coveredWithGroup / survey.Count,7:P1}   ← restul cad pe constanta din cod");

// Predictions written before this ran: these five have published amino acid profiles, so if the
// join is sound they land where the literature puts them. If they do not, nothing below is worth
// reading — the recipe reconstruction would be joining the wrong foods.
Section("Validare — alimente cu răspuns cunoscut din literatură");
Console.WriteLine($"{"aliment",-44}{"măsurat",9}{"literatura",12}{"acoperire",11}");

foreach (var (name, expected) in new[]
    {
        ("Chicken breast, NS as to cooking method, skin not eaten", "~8,4%"),
        ("Egg, whole, boiled or poached", "~8,6%"),
        ("Cheese, cottage, low fat", "~10%"),
        ("Lentils, from dried, no added fat", "~7,2%"),
        ("Fish, tuna, canned", "~7,8%")
    })
{
    var food = measured.FirstOrDefault(candidate => candidate.Description == name);

    Console.WriteLine(food is null
        ? $"{Truncate(name, 44),-44}{"lipsă",9}{expected,12}{"—",11}"
        : $"{Truncate(name, 44),-44}{food.Share,9:P2}{expected,12}{food.Cover,11:F2}");
}

// Corn is the headline: its protein really is leucine-rich, at nearly twice the share the retired
// guess gave it, which is why popcorn and corn cereals rise once the measurement replaces it.
// Vegetables and fruit go the other way — citrus lands at a third of what was assumed.
Section("Cotele măsurate pe categorie");
Console.WriteLine($"{"categorie",-52}{"măsurat",9}{"alimente",10}");

foreach (var (category, share) in byCategory.OrderByDescending(entry => entry.Value))
{
    Console.WriteLine($"{Truncate(category, 52),-52}{share,9:P2}" +
        $"{trusted.Count(food => food.Category == category),10}");
}

// The share left for a food whose category never got measured. It replaces the animal/plant split
// of Gorissen 2018, which puts the two 1.7 points apart. Measured here through the recipes they sit
// 0.31 points apart — 7.59% animal against 7.28% plant — so the split was not doing the work it
// claimed to, and one number carries the same information. See P46.
Section("Cotele măsurate pe grupă majoră");
Console.WriteLine($"{"grupă",-34}{"măsurat",9}{"alimente",10}");

foreach (var (group, share) in byGroup.OrderBy(entry => entry.Key))
{
    Console.WriteLine($"{group + " " + GroupName(group),-34}{share,9:P2}" +
        $"{trusted.Count(food => food.FoodGroup == group),10}");
}

File.WriteAllLines("group-leucine-shares.csv",
    new[] { "# Measured leucine share of protein, per USDA major food group — the first digit of the",
            "# FNDDS food code. The fallback for a category with fewer than three measured foods.",
            "# Generated by tools/LeucineJoinQuery. Do not edit by hand.",
            "group,name,share,foods" }
        .Concat(byGroup.OrderBy(entry => entry.Key).Select(entry => FormattableString.Invariant(
            $"{entry.Key},\"{GroupName(entry.Key)}\",{entry.Value:F5},{trusted.Count(food => food.FoodGroup == entry.Key)}"))));

Section("Constanta de ultimă instanță");
Console.WriteLine($"mediana pe toate cele {trusted.Length} alimente bine reconstruite: " +
    $"{Median([.. trusted.Select(food => food.Share)]),0:P2}");

File.WriteAllLines("category-leucine-shares.csv",
    new[] { "# Measured leucine share of protein, per WWEIA category, from the SR Legacy amino acid",
            "# data reached through each FNDDS food's own recipe. Replaces the guessed 8.8%/7.1%.",
            "# Generated by tools/LeucineJoinQuery. Do not edit by hand.",
            "category,share,foods" }
        .Concat(byCategory.OrderBy(entry => entry.Key).Select(entry => FormattableString.Invariant(
            $"\"{entry.Key}\",{entry.Value:F5},{trusted.Count(food => food.Category == entry.Key)}"))));

// The gate needs leucine on its 68 foods, and it must be the food's own number where one exists —
// a category median is the fallback for the catalogue, not a substitute for a measurement.
var wanted = File.ReadAllLines("../../server/Sated.Calibration/benchmark.csv")
    .Where(line => line.Length > 0 && !line.StartsWith('#'))
    .Skip(1)
    .Select(line => line.Split(',')[2])
    .ToHashSet();

var rows = new List<string>
{
    "# Measured leucine per 100 g for the benchmark foods, joined from SR Legacy through each",
    "# food's recipe. Source says whether the food's own recipe resolved or its category stood in.",
    "# Generated by tools/LeucineJoinQuery. Do not edit by hand.",
    "FdcId,LeucinePer100g,Source"
};

foreach (var fdcId in wanted.OrderBy(id => id))
{
    var food = measured.FirstOrDefault(candidate =>
        candidate.FdcId.ToString(CultureInfo.InvariantCulture) == fdcId);

    if (food is null)
    {
        continue;
    }

    var direct = food.Cover >= CoverFloor && food.Cover <= CoverCeiling;

    var (share, source) = direct
        ? (food.Share, "recipe")
        : byCategory.TryGetValue(food.Category, out var categoryShare)
            ? (categoryShare, "category")
            : (byGroup.GetValueOrDefault(food.FoodGroup), "group");

    if (share <= 0)
    {
        continue;
    }

    rows.Add(FormattableString.Invariant(
        $"{fdcId},{share * food.StatedProtein:F4},{source}"));
}

File.WriteAllLines("../../server/Sated.Calibration/benchmark-leucine.csv", rows);

Section("Scris");
Console.WriteLine($"category-leucine-shares.csv   {byCategory.Count} categorii");
Console.WriteLine($"group-leucine-shares.csv      {byGroup.Count} grupe");
Console.WriteLine($"server/Sated.Calibration/benchmark-leucine.csv  {rows.Count - 4} din {wanted.Count} alimente ale porții");

void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"── {title} ".PadRight(80, '─'));
}

static double Median(double[] values)
{
    var sorted = values.Order().ToArray();
    var middle = sorted.Length / 2;

    return sorted.Length % 2 == 1
        ? sorted[middle]
        : (sorted[middle - 1] + sorted[middle]) / 2;
}

// The first digit of an FNDDS food code. A food with no code — none in this catalogue, but the
// field is optional in the schema — returns "", which matches no group and falls through.
static string MajorGroupOf(string? foodCode) =>
    string.IsNullOrEmpty(foodCode) ? "" : foodCode[..1];

static string GroupName(string group) => group switch
{
    "1" => "lapte și lactate",
    "2" => "carne, pește, ouă",
    "3" => "ouă",
    "4" => "leguminoase, nuci, semințe",
    "5" => "cereale",
    "6" => "fructe",
    "7" => "legume",
    "8" => "grăsimi și uleiuri",
    "9" => "zahăr, dulciuri, băuturi",
    _ => "necunoscută"
};

static string Truncate(string text, int length) =>
    text.Length <= length ? text : text[..length];

record Ingredient(double Protein, double Leucine);

record Measured(
    int FdcId, string Description, string Category, string FoodGroup,
    double Share, double Cover, double StatedProtein);

public record Nutrient([property: JsonPropertyName("number")] string Number);

public record FoodNutrient(
    [property: JsonPropertyName("nutrient")] Nutrient Nutrient,
    [property: JsonPropertyName("amount")] double? Amount
);

public record WweiaCategory(
    [property: JsonPropertyName("wweiaFoodCategoryDescription")] string Description
);

public record InputFood(
    [property: JsonPropertyName("ingredientCode")] int? IngredientCode,
    [property: JsonPropertyName("ingredientWeight")] double IngredientWeight
);

public record FoodItem(
    [property: JsonPropertyName("fdcId")] int FdcId,
    [property: JsonPropertyName("foodCode")] string? FoodCode,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("foodNutrients")] List<FoodNutrient> FoodNutrients,
    [property: JsonPropertyName("wweiaFoodCategory")] WweiaCategory? WweiaFoodCategory,
    [property: JsonPropertyName("inputFoods")] List<InputFood> InputFoods
);

public record SurveyFoodsFile([property: JsonPropertyName("SurveyFoods")] List<FoodItem> Foods);

public record SrFoodItem(
    [property: JsonPropertyName("ndbNumber")] int? NdbNumber,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("foodNutrients")] List<FoodNutrient> FoodNutrients
);

public record SrLegacyFile([property: JsonPropertyName("SRLegacyFoods")] List<SrFoodItem> Foods);
