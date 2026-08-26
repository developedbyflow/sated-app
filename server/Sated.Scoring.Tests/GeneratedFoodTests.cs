using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Sated.Scoring.Tests;

/// <summary>
/// The same kind of relation as EnginePropertyTests, but over foods nobody has ever eaten. The 399
/// in the sample are all plausible; these are deliberately not — zero calories, a food that is
/// nothing but salt, every micronutrient missing at once. Every defect this engine has shipped was
/// in a corner, and a catalogue has no corners.
/// </summary>
[Properties(Arbitrary = [typeof(AnyFood)], MaxTest = 500)]
public class GeneratedFoodTests
{
    private static readonly Calibration Shipped = Calibration.Load();

    private static readonly ScoreCombiner Engine = new(
        new GeneralStrategies(
            Shipped.SatietyScale, Shipped.DensityScales, Shipped.ReferenceMealGrams),
        Shipped.Rules);

    [Property]
    public bool Combine_AnyFood_KeepsEveryComponentInsideZeroToOneHundred(FoodInput food)
    {
        foreach (var lens in Shipped.Lenses)
        {
            var score = Engine.Combine(food, lens);

            if (Outside(score.Satiety) || Outside(score.Density)
                || Outside(score.ProteinQuality) || score.Value is < 0 or > 100)
            {
                return false;
            }
        }

        return true;

        static bool Outside(ComponentValue? value) =>
            value is not null && value.Score is < 0 or > 100;
    }

    [Property]
    public bool GradeFor_AnyFood_ReturnsALetterThatExistsOrNoLetterAtAll(FoodInput food) =>
        Shipped.Lenses.All(lens =>
            Shipped.GradeFor(Engine.Combine(food, lens), lens) is not { } grade
            || Enum.IsDefined(grade));

    [Property]
    public bool GradeFor_AnyFoodWithNoEnergyAndNoMacros_HasNoLetter(FoodInput food) =>
        !ProfileRules.IsNutritionallyEmpty(food)
        || Shipped.Lenses.All(lens =>
            Shipped.GradeFor(Engine.Combine(food, lens), lens) is null);

    [Property]
    public bool Calculate_AnyFoodWithMoreSodium_NeverScoresHigherOnDensity(FoodInput food)
    {
        var before = DensityScore.Calculate(AnyFood.ForDensity(food));
        var after = DensityScore.Calculate(AnyFood.ForDensity(food with { Sodium = food.Sodium + 50 }));

        return before is null || after <= before;
    }

    [Property]
    public bool Calculate_AnyFoodWithMoreProtein_NeverScoresLowerOnProteinQuality(FoodInput food)
    {
        var before = ProteinQualityScore.Calculate(food.LeucinePer100g ?? food.Protein * 0.0752, 300);
        var after = ProteinQualityScore.Calculate(
            food.LeucinePer100g ?? (food.Protein + 5) * 0.0752, 300);

        return after >= before;
    }

    [Property]
    public bool Aggregate_OnePortionOfAnyFood_CarriesEveryNutrientOfThatFood(FoodInput food)
    {
        var plate = PortionAggregate.Aggregate([new Portion(food, 100)]);

        return Same(food.Calories, plate.Calories) && Same(food.Protein, plate.Protein)
            && Same(food.Fat, plate.Fat) && Same(food.Fiber, plate.Fiber)
            && Same(food.VitaminA, plate.VitaminA) && Same(food.VitaminC, plate.VitaminC)
            && Same(food.VitaminE, plate.VitaminE) && Same(food.Calcium, plate.Calcium)
            && Same(food.Iron, plate.Iron) && Same(food.Magnesium, plate.Magnesium)
            && Same(food.Potassium, plate.Potassium) && Same(food.SaturatedFat, plate.SaturatedFat)
            && Same(food.Sodium, plate.Sodium) && Same(food.VitaminD, plate.VitaminD)
            && Same(food.Thiamine, plate.Thiamine);

        static bool Same(double? left, double? right) =>
            left is null == right is null && Math.Abs((left ?? 0) - (right ?? 0)) < 1e-9;
    }

    /// <summary>
    /// A plate of one food is that food however much of it is on the plate — the profile is stated
    /// per 100 g, so the weight cancels. This is the property PortionAggregate's own comment claims
    /// about "the two hundreds cancel", asserted instead of asserted-in-prose.
    /// </summary>
    [Property]
    public bool Aggregate_TheSameFoodAtAnyWeight_GivesTheSameProfile(FoodInput food)
    {
        var light = PortionAggregate.Aggregate([new Portion(food, 25)]);
        var heavy = PortionAggregate.Aggregate([new Portion(food, 400)]);

        return Math.Abs(light.Calories - heavy.Calories) < 1e-9
            && Math.Abs(light.Sodium - heavy.Sodium) < 1e-9;
    }

    [Property]
    public bool GradeFor_AnyFoodGradedTwice_GivesTheSameLetter(FoodInput food) =>
        Shipped.Lenses.All(lens =>
            Shipped.GradeFor(Engine.Combine(food, lens), lens)
            == Shipped.GradeFor(Engine.Combine(food, lens), lens));
}

/// <summary>Foods the engine must survive, not foods anybody would eat.</summary>
public static class AnyFood
{
    // Energy is capped where the engine caps it: past 950 kcal per 100 g the input is rejected on
    // purpose, and generating those would only re-test the guard.
    public static Arbitrary<FoodInput> Foods() =>
        (from calories in Gen.Choose(0, 950)
         from protein in Gen.Choose(0, 80)
         from fat in Gen.Choose(0, 100)
         from fiber in Gen.Choose(0, 45)
         from satFat in Gen.Choose(0, 100)
         from sodium in Gen.Choose(0, 8000)
         from a in Maybe(2000)
         from c in Maybe(500)
         from e in Maybe(50)
         from calcium in Maybe(1500)
         from iron in Maybe(40)
         from magnesium in Maybe(500)
         from potassium in Maybe(5000)
         from d in Maybe(30)
         from b1 in Maybe(5)
         from category in Categories()
         select new FoodInput(
             Category: category,
             Calories: calories, Protein: protein, Fat: fat, Fiber: fiber,
             VitaminA: a, VitaminC: c, VitaminE: e, Calcium: calcium, Iron: iron,
             Magnesium: magnesium, Potassium: potassium,
             SaturatedFat: Math.Min(satFat, fat), Sodium: sodium,
             VitaminD: d, Thiamine: b1)).ToArbitrary();

    // One in six is missing, so a food with none of them turns up eventually — that is the shape
    // a hand-entered food has, and the shape that cost 1,005 letters when it read as zero.
    private static Gen<double?> Maybe(int max) =>
        from present in Gen.Choose(0, 5)
        from value in Gen.Choose(0, max)
        select present == 0 ? (double?)null : value;

    // Null, a category the catalogue owns, one it does not, and one that carries a rule.
    private static Gen<string?> Categories() =>
        Gen.Elements<string?>([
            null, "Salad dressings and vegetable oils", "Soft drinks", "Nuts and seeds",
            "Milk, whole", "Huiles d'olive", "Mixed portions"]);

    public static DensityInput ForDensity(FoodInput food) => new(
        food.Calories, food.Protein, food.Fiber, food.VitaminA, food.VitaminC, food.VitaminE,
        food.Calcium, food.Iron, food.Magnesium, food.Potassium, food.SaturatedFat, food.Sodium,
        food.VitaminD, food.Thiamine);
}
