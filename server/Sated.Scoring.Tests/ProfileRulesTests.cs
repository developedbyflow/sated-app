namespace Sated.Scoring.Tests;

public class ProfileRulesTests
{
    private static readonly double[] MeasuredSatiety =
        [0.5000, 1.9635, 2.3037, 2.8612, 4.6769];

    private static readonly double[] MeasuredDensity =
        [-884.5460, 8.7918, 18.8242, 37.0955, 535.6610];

    private static FoodInput Typed(string? category, double calories, double protein,
        double fat, double fiber) =>
        new(Category: category, Calories: calories, Protein: protein, Fat: fat, Fiber: fiber,
            VitaminA: null, VitaminC: null, VitaminE: null, Calcium: null, Iron: null,
            Magnesium: null, Potassium: null, SaturatedFat: fat / 5, Sodium: 10,
            VitaminD: null, Thiamine: null);

    private static readonly FoodInput OliveOil = Typed(null, calories: 884, protein: 0, fat: 100, fiber: 0);
    private static readonly FoodInput Cola = Typed(null, calories: 41, protein: 0, fat: 0, fiber: 0);
    private static readonly FoodInput Pecans = Typed(null, calories: 754, protein: 9.2, fat: 72, fiber: 9.6);
    private static readonly FoodInput Cheddar = Typed(null, calories: 403, protein: 23, fat: 33, fiber: 0);

    private static ScoreCombiner Combiner() =>
        new(new PercentileScale(MeasuredSatiety), new PercentileScale(MeasuredDensity),
            Frozen.ReferenceMealGrams);

    [Fact]
    public void Satiety_CaloricDrinkWithNoCategory_ScoresZero()
    {
        var satiety = ProfileRules.Satiety(Cola);

        Assert.Equal(0, satiety!.Score);
    }

    [Fact]
    public void Satiety_PureFatWithNoCategory_IsNoLongerTheProfilesBusiness()
    {
        // Fat quality is a component now, computed for every food. The profile no longer stands in
        // for it, so a pure fat takes the general satiety formula like anything else — and what
        // rescues olive oil is the weight fat quality takes, not a rule that skipped the formula.
        Assert.Null(ProfileRules.Satiety(OliveOil));
    }

    [Fact]
    public void Satiety_NutWithNoCategory_IsLeftToTheGeneralFormula()
    {
        var satiety = ProfileRules.Satiety(Pecans);

        Assert.Null(satiety);
    }

    [Fact]
    public void ShareOfSatietyWeight_Nut_HandsOverPartOfTheWeightButNotAllOfIt()
    {
        // Pecans are 86% fat by calories, so they hand over roughly two thirds. Only a food that
        // is nothing but fat hands over all of it, which is what keeps a fatty food from going
        // blind to its own calories — satiety is the only component that reads them.
        Assert.InRange(FatQuality.ShareOfSatietyWeight(Pecans), 0.6, 0.7);
    }

    [Fact]
    public void ShareOfSatietyWeight_OliveOil_HandsOverAllOfIt()
    {
        Assert.Equal(1, FatQuality.ShareOfSatietyWeight(OliveOil));
    }

    [Fact]
    public void ShareOfSatietyWeight_Cheese_HandsOverLessThanANut()
    {
        Assert.True(
            FatQuality.ShareOfSatietyWeight(Cheddar)
            < FatQuality.ShareOfSatietyWeight(Pecans));
    }

    [Fact]
    public void Combine_PureFat_IsGradedTheSameWhateverItsCategory()
    {
        // The property the old switch could not give: olive oil's grade no longer depends on
        // whether somebody wrote its category down. This is what closed 725 dominance violations.
        var named = OliveOil with { Category = "Some category nobody wrote a rule for" };

        Assert.Equal(
            Combiner().Combine(OliveOil, Frozen.WeightLoss).Value,
            Combiner().Combine(named, Frozen.WeightLoss).Value);
    }

    private static ScoreCombiner CombinerKnowing(params string[] categories) =>
        new(new GeneralStrategies(new PercentileScale(MeasuredSatiety),
                new PercentileScale(MeasuredDensity), Frozen.ReferenceMealGrams),
            new CategoryRules([], categories.ToHashSet(StringComparer.OrdinalIgnoreCase)));

    [Fact]
    public void Combine_CategoryFromAnotherCatalogue_IsGradedLikeTheSameFoodAtHome()
    {
        var combiner = CombinerKnowing("Salad dressings and vegetable oils");
        var imported = OliveOil with { Category = "Huiles d'olive" };

        Assert.Equal(
            combiner.Combine(OliveOil, Frozen.WeightLoss).Value,
            combiner.Combine(imported, Frozen.WeightLoss).Value);
    }

    [Fact]
    public void Combine_FatQuality_CarriesTheWholeSatietyWeightOfAPureFat()
    {
        var score = Combiner().Combine(OliveOil, Frozen.WeightLoss);

        Assert.Equal(1, FatQuality.ShareOfSatietyWeight(OliveOil));
        Assert.Equal(FatQuality.UnsaturatedShare(OliveOil)!.Score, score.FatQuality!.Score);
    }

    [Fact]
    public void Combine_FatQuality_CarriesNoneOfItForAFriedFood()
    {
        // A crisp is fried in vegetable oil, so its unsaturated share is excellent and means
        // nothing. Measured: a ramp that started at zero lifted crisps, nuggets, stuffed-crust
        // pizza and granola out of D/E and G0's bottom thirty fell to 25 of 30.
        var crisps = Typed(null, calories: 536, protein: 7, fat: 35, fiber: 4.4);

        Assert.Equal(0, FatQuality.ShareOfSatietyWeight(crisps));
    }
}
