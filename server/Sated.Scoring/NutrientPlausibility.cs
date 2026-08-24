namespace Sated.Scoring;

/// <summary>What is wrong with a food's numbers, if anything.</summary>
public enum NutrientCheck
{
    /// <summary>Nothing is obviously wrong. Not a claim that the numbers are right.</summary>
    Plausible,

    /// <summary>More energy than 100 g of any food can carry. Almost always kilojoules.</summary>
    EnergyTooHighForAnyFood,

    /// <summary>The energy does not follow from the macronutrients, in either direction.</summary>
    EnergyDisagreesWithTheMacronutrients
}

/// <summary>
/// Whether a food's numbers can belong to a real food, for the boundary where a person or an
/// importer types them in. The engine grades whatever it is handed; this is what an API calls
/// before handing it anything.
/// </summary>
/// <remarks>
/// It catches unit mistakes, which is the failure that matters when food comes from more than one
/// country. European data reports energy in kilojoules — 4.184 times the number this engine wants —
/// and that value passes every other check in the engine and simply grades wrong.
/// What it cannot catch: European labels print salt in grams where this engine wants sodium in
/// milligrams, and 1.2 g of salt is 480 mg of sodium. Writing 1.2 into Sodium gives a perfectly
/// plausible number and a wrong grade, and sodium is a limiter, so the damage is direct. That
/// conversion is the importer's job and no arithmetic here can second-guess it.
/// </remarks>
public static class NutrientPlausibility
{
    // Atwater: 4 kcal per gram of protein and of carbohydrate, 9 for fat, 7 for alcohol. Measured
    // across the 5,403 FNDDS foods that report all four, the energy a food declares divided by the
    // energy its macronutrients imply has a median of 0.999 — the identity holds almost exactly.
    private const double CaloriesPerGramOfProtein = 4;
    private const double CaloriesPerGramOfCarbohydrate = 4;
    private const double CaloriesPerGramOfFat = 9;
    private const double CaloriesPerGramOfAlcohol = 7;

    // Wide, because this is a unit check and not a data-quality one: a kilojoule value reads 4.18
    // and every real food sits between these. Measured over the catalogue above the floor below,
    // this band rejects zero of 5,157 foods. Narrowing it would start rejecting real food to catch
    // nothing extra — the mistake it exists for is off by a factor, not by a few per cent.
    private const double LowestPlausibleRatio = 0.5;
    private const double HighestPlausibleRatio = 2.0;

    // Below this the numbers are rounding, not measurement: vinegar declares 21 kcal against 4
    // implied, and a sugar-free energy drink 1 against 5. It is the floor SatietyScore already
    // uses, for the same reason — under it a per-100 g figure stops carrying information.
    private const double LowestCheckableCalories = 30;

    // Nothing carries more energy per gram than fat, so 100 g of anything tops out near 900. Lard,
    // the densest food in FNDDS, reads 902 and nothing in the catalogue exceeds it.
    private const double MaxCaloriesPer100g = 950;

    /// <param name="calories">Kilocalories per 100 g. Not kilojoules — that is what this catches.</param>
    /// <param name="alcohol">Grams of ethanol per 100 g. Zero for everything that is not a drink.</param>
    public static NutrientCheck Check(
        double calories, double protein, double fat, double carbohydrate, double alcohol = 0)
    {
        if (calories > MaxCaloriesPer100g)
        {
            return NutrientCheck.EnergyTooHighForAnyFood;
        }

        if (calories < LowestCheckableCalories)
        {
            return NutrientCheck.Plausible;
        }

        var implied = CaloriesPerGramOfProtein * protein
            + CaloriesPerGramOfCarbohydrate * carbohydrate
            + CaloriesPerGramOfFat * fat
            + CaloriesPerGramOfAlcohol * alcohol;

        // A food above the floor whose macronutrients imply no energy at all has had them left out,
        // which the ratio below cannot express: it would divide by zero.
        if (implied <= 0)
        {
            return NutrientCheck.EnergyDisagreesWithTheMacronutrients;
        }

        var ratio = calories / implied;

        return ratio < LowestPlausibleRatio || ratio > HighestPlausibleRatio
            ? NutrientCheck.EnergyDisagreesWithTheMacronutrients
            : NutrientCheck.Plausible;
    }
}
