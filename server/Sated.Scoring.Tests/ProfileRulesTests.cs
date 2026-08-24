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
    public void Satiety_PureFatWithNoCategory_ScoresItsUnsaturatedShare()
    {
        var satiety = ProfileRules.Satiety(OliveOil);

        Assert.Equal(FatQuality.UnsaturatedShare(OliveOil)!.Score, satiety!.Score);
    }

    [Fact]
    public void Satiety_NutWithNoCategory_IsLeftToTheGeneralFormula()
    {
        var satiety = ProfileRules.Satiety(Pecans);

        Assert.Null(satiety);
    }

    [Fact]
    public void Density_NutWithNoCategory_ScoresItsUnsaturatedShare()
    {
        var density = ProfileRules.Density(Pecans);

        Assert.Equal(FatQuality.UnsaturatedShare(Pecans)!.Score, density!.Score);
    }

    [Fact]
    public void Density_CheeseWithNoCategory_IsNotMistakenForANut()
    {
        var density = ProfileRules.Density(Cheddar);

        Assert.Null(density);
    }

    [Fact]
    public void Combine_PureFatCarryingACategory_IsLeftToTheCategoryTable()
    {
        var named = OliveOil with { Category = "Some category nobody wrote a rule for" };

        Assert.NotEqual(
            Combiner().Combine(OliveOil, Frozen.WeightLoss).Satiety.Score,
            Combiner().Combine(named, Frozen.WeightLoss).Satiety.Score);
    }

    private static ScoreCombiner CombinerKnowing(params string[] categories) =>
        new(new GeneralStrategies(new PercentileScale(MeasuredSatiety),
                new PercentileScale(MeasuredDensity), Frozen.ReferenceMealGrams),
            new CategoryRules([], categories.ToHashSet(StringComparer.OrdinalIgnoreCase)));

    [Fact]
    public void Combine_CategoryFromAnotherCatalogue_TakesTheProfileFallback()
    {
        var combiner = CombinerKnowing("Salad dressings and vegetable oils");
        var imported = OliveOil with { Category = "Huiles d'olive" };

        Assert.Equal(
            FatQuality.UnsaturatedShare(imported)!.Score,
            combiner.Combine(imported, Frozen.WeightLoss).Satiety.Score);
    }

    [Fact]
    public void Combine_CategoryTheCatalogueKnowsButNobodyRuled_IsLeftToTheGeneralFormula()
    {
        var combiner = CombinerKnowing("Cream cheese, sour cream, whipped cream");
        var known = OliveOil with { Category = "Cream cheese, sour cream, whipped cream" };

        Assert.NotEqual(
            FatQuality.UnsaturatedShare(known)!.Score,
            combiner.Combine(known, Frozen.WeightLoss).Satiety.Score);
    }
}
