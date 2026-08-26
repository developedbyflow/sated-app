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

    // A drink is sugar in water, or water. The ceiling keeps sugar and honey out: the same profile
    // at four times the concentration, and nobody drinks them.
    // No calorie minimum, on purpose. It used to require calories above zero, which quietly meant
    // the rule judged Coke and refused to judge Coke Zero — and the diet drink then collected the
    // Fullness Factor's payment for carrying few calories per 100 g. Measured on the catalogue,
    // that put "Fruit flavored drink, diet" at A 76.1 while Powerade Zero read E 0.0, and in five
    // of six brand pairs the sugar-free version graded below the sugared one. See the audit, A1.
    private const double DrinkCalorieCeiling = 100;
    private const double DrinkProteinFatFiber = 0.5;

    /// <returns>
    /// True when this profile is one of the three these rules recognise. It exempts the food from
    /// the density floor for the same reason a ruled category is exempt: something measured has
    /// judged it, and a blanket floor must not overrule that.
    /// </returns>
    public static bool Judges(FoodInput food) => Satiety(food) is not null;

    /// <returns>The satiety replacement for this profile, or null to use the general formula.</returns>
    public static ComponentValue? Satiety(FoodInput food) =>
        IsDrink(food) ? LiquidCalories.NoSatiety(food) : null;

    /// <returns>
    /// True when the food is a drink by its nutrients alone. Unlike the rest of this class this
    /// one is asked of every food, catalogue or not: the category rules name three drink
    /// categories out of the catalogue's eleven, and which three was a list somebody wrote rather
    /// than a fact about the food. Tonic water is filed under "Flavored or carbonated water" and
    /// ginger ale under "Soft drinks"; they carry the same 34 kcal of sugar water, and the names
    /// graded them 49.4 and 4.5. A nutrient test cannot miss a category nobody thought of.
    /// </returns>
    public static bool IsDrink(FoodInput food) =>
        food.Calories <= DrinkCalorieCeiling
        && food.Protein + food.Fat + food.Fiber <= DrinkProteinFatFiber;

    // Five calories per 100 g is a rounding artefact, not a food's energy: FNDDS reports energy in
    // whole kcal, and the 89 foods under this line are waters, black coffee and tea, diet drinks
    // and table-top sweeteners. Half a gram of macronutrient is the same line ProfileRules already
    // draws for a drink.
    private const double EmptyCalories = 5;
    private const double EmptyMacros = 0.5;

    /// <returns>
    /// True when the food carries neither energy nor macronutrients, so no score defined per
    /// calorie has an answer for it. See <see cref="CombinedScore.IsNutritionallyEmpty"/>.
    /// </returns>
    public static bool IsNutritionallyEmpty(FoodInput food) =>
        food.Calories <= EmptyCalories
        && food.Protein + food.Fat + food.Fiber <= EmptyMacros;

}
