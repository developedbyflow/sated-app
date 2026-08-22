using System.Text.Json;
using System.Text.Json.Serialization;

// Story 1.5 asks for a "DIAAS component". No USDA dataset carries DIAAS, and FNDDS carries no
// amino acids at all. SR Legacy does carry the full essential amino acid profile, so protein
// completeness can be computed instead of looked up: the amino acid score (AAS) is the lowest
// ratio between a food's essential amino acids and the FAO reference pattern.
// AAS is the first half of DIAAS. The missing half is digestibility, which no public dataset
// covers at scale. This tool measures how far the computable half gets us.

var json = File.ReadAllText("../UsdaCoverageQuery/data/FoodData_Central_sr_legacy_food_json_2018-04.json");
var foods = JsonSerializer.Deserialize<SrLegacyFoodsFile>(json)!.Foods;
var byId = foods.ToDictionary(food => food.FdcId);

Console.WriteLine($"SR Legacy: {foods.Count} alimente încărcate.");

// FAO/WHO 2013 reference pattern for the older child (3-10 y), mg per g of protein.
// This is the pattern DIAAS uses for general-population food labelling.
// Sulphur amino acids (Met + Cys) and aromatic amino acids (Phe + Tyr) are scored as pairs,
// as the pattern defines them — not one by one.
var referencePattern = new Dictionary<string, double>
{
    ["Histidină"] = 16,
    ["Izoleucină"] = 30,
    ["Leucină"] = 61,
    ["Lizină"] = 48,
    ["AA cu sulf"] = 23,
    ["AA aromatici"] = 41,
    ["Treonină"] = 25,
    ["Triptofan"] = 6.6,
    ["Valină"] = 40,
};

const string Protein = "203";
const string Leucine = "504";

static Dictionary<string, double> Amounts(FoodItem food) =>
    (food.FoodNutrients ?? [])
        .Where(entry => entry.Amount is not null)
        .GroupBy(entry => entry.Nutrient.Number)
        .ToDictionary(group => group.Key, group => group.First().Amount!.Value);

// Amino acid amounts arrive in grams per 100 g of food. The pattern is milligrams per gram of
// protein, so each amount is divided by the food's own protein content.
static Dictionary<string, double> PerGramOfProtein(Dictionary<string, double> amounts)
{
    var protein = amounts[Protein];

    double Scaled(params string[] codes) => codes.Sum(code => amounts[code]) * 1000 / protein;

    return new Dictionary<string, double>
    {
        ["Histidină"] = Scaled("512"),
        ["Izoleucină"] = Scaled("503"),
        ["Leucină"] = Scaled("504"),
        ["Lizină"] = Scaled("505"),
        ["AA cu sulf"] = Scaled("506", "507"),
        ["AA aromatici"] = Scaled("508", "509"),
        ["Treonină"] = Scaled("502"),
        ["Triptofan"] = Scaled("501"),
        ["Valină"] = Scaled("510"),
    };
}

var aminoAcidCodes = new[] { "501", "502", "503", "504", "505", "506", "507", "508", "509", "510", "512" };

var scorable = foods.Count(food =>
{
    var amounts = Amounts(food);
    return amounts.ContainsKey(Protein) && amounts[Protein] > 0 && aminoAcidCodes.All(amounts.ContainsKey);
});

Console.WriteLine($"Cu profil complet de aminoacizi și proteină > 0: {scorable} ({(double)scorable / foods.Count * 100:F1}%)");
Console.WriteLine();

var set = new (string Label, int FdcId)[]
{
    ("Ou întreg",          171287),
    ("Piept de pui",       171477),
    ("Vită slabă",         169471),
    ("Somon",              175168),
    ("Lapte integral",     171265),
    ("Iaurt grecesc",      170903),
    ("Soia boabe",         174270),
    ("Tofu",               172475),
    ("Linte",              172420),
    ("Fasole neagră",      173734),
    ("Unt de arahide",     172470),
    ("Migdale",            170567),
    ("Quinoa",             168874),
    ("Ovăz",               169705),
    ("Pâine integrală",    172689),
    ("Croissant",          174987),
    ("Orez alb",           168877),
    ("Porumb dulce",       169998),
    ("Cartof",             170026),
    ("Gelatină",           169599),
};

Console.WriteLine($"{"Aliment",-18} {"Prot/100g",9} {"AAS",6}  {"Limitant",-14} {"Porție",7} {"Leu/porție",10}  Prag 2,5g");
Console.WriteLine(new string('-', 88));

foreach (var entry in set)
{
    var food = byId[entry.FdcId];
    var amounts = Amounts(food);
    var perGram = PerGramOfProtein(amounts);

    var limiting = perGram.MinBy(acid => acid.Value / referencePattern[acid.Key]);
    var score = limiting.Value / referencePattern[limiting.Key] * 100;

    var portion = (food.FoodPortions ?? []).FirstOrDefault(p => p.GramWeight > 0)?.GramWeight;
    var leucinePerPortion = portion is null ? (double?)null : amounts[Leucine] * portion.Value / 100;

    var portionText = portion is null ? "—" : $"{portion.Value:F0} g";
    var leucineText = leucinePerPortion is null ? "—" : $"{leucinePerPortion.Value:F2} g";
    var verdict = leucinePerPortion is null ? "?" : leucinePerPortion.Value >= 2.5 ? "DA" : "nu";

    Console.WriteLine(
        $"{entry.Label,-18} {amounts[Protein],9:F1} {score,5:F0}%  {limiting.Key,-14} " +
        $"{portionText,7} {leucineText,10}  {verdict,8}");
}

public record Nutrient(
    [property: JsonPropertyName("number")] string Number
);

public record FoodNutrient(
    [property: JsonPropertyName("nutrient")] Nutrient Nutrient,
    [property: JsonPropertyName("amount")] double? Amount
);

public record FoodPortion(
    [property: JsonPropertyName("gramWeight")] double GramWeight
);

public record FoodItem(
    [property: JsonPropertyName("fdcId")] int FdcId,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("foodNutrients")] List<FoodNutrient>? FoodNutrients,
    [property: JsonPropertyName("foodPortions")] List<FoodPortion>? FoodPortions
);

public record SrLegacyFoodsFile(
    [property: JsonPropertyName("SRLegacyFoods")] List<FoodItem> Foods
);
