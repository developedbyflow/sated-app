using System.Text.Json;
using System.Text.Json.Serialization;

var json = File.ReadAllText("../UsdaCoverageQuery/data/surveyDownload.json");
var foods = JsonSerializer.Deserialize<SurveyFoodsFile>(json)!.Foods.ToDictionary(food => food.FdcId);

Console.WriteLine($"FNDDS: {foods.Count} alimente încărcate.");

// FDA Daily Values, 2016 label rules, 2,000 kcal reference diet.
var encouraged = new Dictionary<string, double>
{
    ["203"] = 50,     // Protein, g
    ["291"] = 28,     // Fiber, g
    ["320"] = 900,    // Vitamin A, µg RAE
    ["401"] = 90,     // Vitamin C, mg
    ["323"] = 15,     // Vitamin E, mg
    ["301"] = 1300,   // Calcium, mg
    ["303"] = 18,     // Iron, mg
    ["304"] = 420,    // Magnesium, mg
    ["306"] = 4700,   // Potassium, mg
};

var limited = new Dictionary<string, double>
{
    ["606"] = 20,     // Saturated fat, g
    ["307"] = 2300,   // Sodium, mg
};

// No FDA Daily Value exists for MUFA or PUFA. 21 g is Drewnowski 2009 (2,000 kcal diet);
// 12 g is 11 g omega-6 plus 1.1 g omega-3 from the AU/NZ NRVs, rounded.
var encouragedWithFats = new Dictionary<string, double>(encouraged)
{
    ["645"] = 21,     // MUFA, g
    ["646"] = 12,     // PUFA, g
};

static double Nrf(FoodItem food, Dictionary<string, double> encouraged, Dictionary<string, double> limited, Basis basis)
{
    var amounts = food.FoodNutrients
        .Where(entry => entry.Amount is not null)
        .ToDictionary(entry => entry.Nutrient.Number, entry => entry.Amount!.Value);

    // USDA reports per 100 g. The per-100-kcal basis is that rescaled by the food's own energy.
    var scale = basis == Basis.Per100Kcal ? 100 / amounts["208"] : 1;

    var positive = encouraged.Sum(dv => Math.Min(100, amounts[dv.Key] * scale / dv.Value * 100));
    var negative = limited.Sum(dv => amounts[dv.Key] * scale / dv.Value * 100);

    return positive - negative;
}

var traps = new (string Label, int FdcId)[]
{
    ("C1 Ulei de măsline", 2710186),
    ("C2 Migdale",         2707489),
    ("C3 Nuci",            2707531),
    ("C4 Avocado",         2709223),
    ("C5 Cartof fiert",    2709388),
    ("C6 Somon",           2706286),
    ("C7 Popcorn simplu",  2708219),
    ("C8 Cheddar",         2705709),
};

Console.WriteLine();
Console.WriteLine($"{"Capcană",-20} {"100 g",8} {"+grăs.",8} {"100 kcal",9} {"+grăs.",8}");

foreach (var trap in traps)
{
    var food = foods[trap.FdcId];

    Console.WriteLine(
        $"{trap.Label,-20} " +
        $"{Nrf(food, encouraged, limited, Basis.Per100Grams),8:F1} " +
        $"{Nrf(food, encouragedWithFats, limited, Basis.Per100Grams),8:F1} " +
        $"{Nrf(food, encouraged, limited, Basis.Per100Kcal),9:F1} " +
        $"{Nrf(food, encouragedWithFats, limited, Basis.Per100Kcal),8:F1}");
}

var pairs = new (string Better, int BetterId, string Worse, int WorseId)[]
{
    ("Pâine integrală", 2707709, "Pâine albă",        2707598),
    ("Cartof fiert",    2709388, "Cartofi prăjiți",   2709458),
    ("Piept de pui",    2705956, "Nuggets",           2706096),
    ("Vită slabă",      2705830, "Cereale glazurate", 2708474),
    ("Ulei de măsline", 2710186, "Unt",               2710155),
    ("Popcorn simplu",  2708219, "Popcorn cu unt",    2708220),
    ("Iaurt simplu",    2705423, "Iaurt cu fructe",   2705431),
};

Console.WriteLine();
Console.WriteLine($"{"Perechea",-40} {"100 g",6} {"+grăs.",7} {"100 kcal",9} {"+grăs.",7}");

foreach (var pair in pairs)
{
    var better = foods[pair.BetterId];
    var worse = foods[pair.WorseId];

    string Verdict(Dictionary<string, double> enc, Basis basis) =>
        Nrf(better, enc, limited, basis) > Nrf(worse, enc, limited, basis) ? "OK" : "PICĂ";

    var label = $"{pair.Better} > {pair.Worse}";
    Console.WriteLine(
        $"{label,-40} " +
        $"{Verdict(encouraged, Basis.Per100Grams),6} " +
        $"{Verdict(encouragedWithFats, Basis.Per100Grams),7} " +
        $"{Verdict(encouraged, Basis.Per100Kcal),9} " +
        $"{Verdict(encouragedWithFats, Basis.Per100Kcal),7}");
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

public enum Basis
{
    Per100Grams,
    Per100Kcal,
}
