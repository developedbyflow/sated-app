namespace Sated.Scoring;

/// <summary>
/// What replaces a component for a food that carries no category at all (FR-6). A person types in
/// a food from a package; nothing hands it a WWEIA name, so CategoryRules can never match it and it
/// falls to the general formula in silence. Measured on the gate's 68 foods, that silence puts
/// olive oil back at E and grades a cola C — the letter tap water gets.
/// </summary>
public static class ProfileRules
{
    // These are classification, not calibration: they do not scale a score, they decide which
    // formula applies, which is the job a WWEIA name does. Measured on the 68 benchmark foods, they
    // catch nine and seven of those already carry the matching rule under their own category — the
    // profile agrees with every assignment somebody made by hand. The two it catches that are not
    // ruled, whipped cream and avocado, keep their category and so are never asked. They recover
    // all six foods whose letter breaks without a category.

    // Olive oil is 100 g of fat with no protein and no fibre; pecans are 72 g of fat with 9.2 and
    // 9.6. The fat share alone cannot separate them, which is what killed the profile rule at P50.
    private const double PureFatCalorieShare = 0.80;
    private const double PureFatProteinAndFiber = 3.0;

    // Nuts carry their rule on density, not satiety, so they need their own test. Fat share alone
    // would take cheddar and bacon with them at 0.74 and 0.72; fibre is what tells them apart.
    private const double NutCalorieShare = 0.60;
    private const double NutProteinAndFiber = 5.0;
    private const double NutFiber = 3.0;

    // A caloric drink is sugar in water. The ceiling keeps sugar and honey out: the same profile at
    // four times the concentration, and nobody drinks them.
    private const double DrinkCalorieCeiling = 100;
    private const double DrinkProteinFatFiber = 0.5;

    // Atwater: a gram of fat carries nine calories. What matters is the share of the food's energy
    // that fat accounts for, not the grams, because grams cannot compare a dressing to an oil.
    private const double CaloriesPerGramOfFat = 9;

    /// <returns>The satiety replacement for this profile, or null to use the general formula.</returns>
    public static ComponentValue? Satiety(FoodInput food) =>
        IsCaloricDrink(food) ? LiquidCalories.NoSatiety(food)
        : IsPureFat(food) ? FatQuality.UnsaturatedShare(food)
        : null;

    /// <returns>The density replacement for this profile, or null to use the general formula.</returns>
    public static ComponentValue? Density(FoodInput food) =>
        IsNutOrSeed(food) ? FatQuality.UnsaturatedShare(food) : null;

    private static bool IsPureFat(FoodInput food) =>
        FatShareOfCalories(food) >= PureFatCalorieShare
        && food.Protein + food.Fiber <= PureFatProteinAndFiber;

    private static bool IsNutOrSeed(FoodInput food) =>
        FatShareOfCalories(food) >= NutCalorieShare
        && food.Protein + food.Fiber >= NutProteinAndFiber
        && food.Fiber >= NutFiber;

    private static bool IsCaloricDrink(FoodInput food) =>
        food.Calories > 0
        && food.Calories <= DrinkCalorieCeiling
        && food.Protein + food.Fat + food.Fiber <= DrinkProteinFatFiber;

    private static double FatShareOfCalories(FoodInput food) =>
        food.Calories <= 0 ? 0 : food.Fat * CaloriesPerGramOfFat / food.Calories;
}
