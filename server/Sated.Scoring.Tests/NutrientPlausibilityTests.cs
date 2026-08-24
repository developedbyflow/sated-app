namespace Sated.Scoring.Tests;

public class NutrientPlausibilityTests
{
    [Fact]
    public void Check_ChickenBreast_IsPlausible()
    {
        var result = NutrientPlausibility.Check(
            calories: 165, protein: 31, fat: 3.6, carbohydrate: 0);

        Assert.Equal(NutrientCheck.Plausible, result);
    }

    [Fact]
    public void Check_TheSameFoodInKilojoules_DisagreesWithItsMacronutrients()
    {
        var result = NutrientPlausibility.Check(
            calories: 165 * 4.184, protein: 31, fat: 3.6, carbohydrate: 0);

        Assert.Equal(NutrientCheck.EnergyDisagreesWithTheMacronutrients, result);
    }

    [Fact]
    public void Check_LardInKilojoules_IsTooHighForAnyFood()
    {
        var result = NutrientPlausibility.Check(
            calories: 902 * 4.184, protein: 0, fat: 100, carbohydrate: 0);

        Assert.Equal(NutrientCheck.EnergyTooHighForAnyFood, result);
    }

    [Fact]
    public void Check_TheDensestFoodInTheCatalogue_IsPlausible()
    {
        var result = NutrientPlausibility.Check(
            calories: 902, protein: 0, fat: 100, carbohydrate: 0);

        Assert.Equal(NutrientCheck.Plausible, result);
    }

    [Fact]
    public void Check_Wine_IsPlausibleOnlyWhenItsAlcoholIsCounted()
    {
        Assert.Equal(
            NutrientCheck.EnergyDisagreesWithTheMacronutrients,
            NutrientPlausibility.Check(calories: 83, protein: 0.1, fat: 0, carbohydrate: 2.6));

        Assert.Equal(
            NutrientCheck.Plausible,
            NutrientPlausibility.Check(
                calories: 83, protein: 0.1, fat: 0, carbohydrate: 2.6, alcohol: 10.6));
    }

    [Fact]
    public void Check_VinegarBelowTheFloor_IsNotJudged()
    {
        var result = NutrientPlausibility.Check(
            calories: 21, protein: 0, fat: 0, carbohydrate: 0.96);

        Assert.Equal(NutrientCheck.Plausible, result);
    }
}
