namespace Sated.Scoring.Tests;

public class FatQualityTests
{
    private static FoodInput Fat(string category, double calories, double fat,
        double saturatedFat, double sodium) =>
        new(Category: category, Calories: calories, Protein: 0, Fat: fat, Fiber: 0,
            VitaminA: 0, VitaminC: 0, VitaminE: 0, Calcium: 0, Iron: 0, Magnesium: 0,
            Potassium: 0, SaturatedFat: saturatedFat, Sodium: sodium);

    private static readonly FoodInput OliveOil = Fat(
        "Salad dressings and vegetable oils", calories: 884, fat: 100, saturatedFat: 15.5, sodium: 2);

    private static readonly FoodInput Butter = Fat(
        "Butter and animal fats", calories: 726, fat: 82.2, saturatedFat: 45.6, sodium: 576);

    private static readonly FoodInput Mayonnaise = Fat(
        "Mayonnaise", calories: 680, fat: 74.8, saturatedFat: 11.7, sodium: 635);

    [Fact]
    public void UnsaturatedShare_OliveOil_ScoresFarAboveButter()
    {
        Assert.True(
            FatQuality.UnsaturatedShare(OliveOil)!.Score >
            FatQuality.UnsaturatedShare(Butter)!.Score);
    }

    [Fact]
    public void UnsaturatedShare_OliveOil_ScoresAboveMayonnaiseOnSodiumAlone()
    {
        Assert.True(
            FatQuality.UnsaturatedShare(OliveOil)!.Score >
            FatQuality.UnsaturatedShare(Mayonnaise)!.Score);
    }

    [Fact]
    public void UnsaturatedShare_OliveOil_IsMostlyUnsaturated()
    {
        Assert.Equal(84.5, FatQuality.UnsaturatedShare(OliveOil)!.Score, tolerance: 0.1);
    }

    [Fact]
    public void UnsaturatedShare_FoodWithoutFat_HasNoValue()
    {
        Assert.Null(FatQuality.UnsaturatedShare(OliveOil with { Fat = 0 }));
    }

    [Fact]
    public void UnsaturatedShare_FoodWithoutCalories_HasNoValue()
    {
        Assert.Null(FatQuality.UnsaturatedShare(OliveOil with { Calories = 0 }));
    }




}
