namespace Sated.Scoring.Tests;

public class DensityScoreTests
{
    private static readonly DensityInput MilkNfs = new(
        Calories: 52, Protein: 3.33, Fiber: 0,
        VitaminA: 58, VitaminC: 0.1, VitaminE: 0.03,
        Calcium: 125, Iron: 0, Magnesium: 12, Potassium: 156,
        SaturatedFat: 1.25, Sodium: 39,
        VitaminD: 1.1, Thiamine: 0.057);

    private static readonly DensityInput PassionFruit = new(
        Calories: 97, Protein: 2.2, Fiber: 10.4,
        VitaminA: 64, VitaminC: 30, VitaminE: 0.02,
        Calcium: 12, Iron: 1.6, Magnesium: 29, Potassium: 348,
        SaturatedFat: 0.059, Sodium: 28,
        VitaminD: 0, Thiamine: 0);

    [Fact]
    public void Calculate_MilkUnderTheGlp1Set_CountsTheTwoNutrientsNrf92LeavesOut()
    {
        Assert.True(
            DensityScore.Calculate(MilkNfs, DensityScore.Nrf112)
            > DensityScore.Calculate(MilkNfs, DensityScore.Nrf92));
    }

    [Fact]
    public void Calculate_AFoodCarryingNeitherNutrient_ScoresTheSameUnderBothSets()
    {
        Assert.Equal(
            DensityScore.Calculate(PassionFruit, DensityScore.Nrf92),
            DensityScore.Calculate(PassionFruit, DensityScore.Nrf112));
    }

    [Fact]
    public void Calculate_OliveOil_MatchesBenchmarkReport()
    {
        var oliveOil = new DensityInput(
            Calories: 900, Protein: 0, Fiber: 0,
            VitaminA: 0, VitaminC: 0, VitaminE: 20.9,
            Calcium: 1, Iron: 0.56, Magnesium: 0, Potassium: 1,
            SaturatedFat: 15.5, Sodium: 2
        );

        var score = DensityScore.Calculate(oliveOil);

        Assert.Equal(7.2, score!.Value, tolerance: 0.1);
    }

    [Fact]
    public void Calculate_Cheddar_MatchesBenchmarkReport()
    {
        var cheddar = new DensityInput(
            Calories: 409, Protein: 23.3, Fiber: 0,
            VitaminA: 316, VitaminC: 0, VitaminE: 0.75,
            Calcium: 707, Iron: 0.16, Magnesium: 27, Potassium: 77,
            SaturatedFat: 19.2, Sodium: 654
        );

        var score = DensityScore.Calculate(cheddar);

        Assert.Equal(6.3, score!.Value, tolerance: 0.1);
    }

    [Fact]
    public void Calculate_Almonds_ScalesToCaloriesBeforeCapping()
    {
        var almonds = new DensityInput(
            Calories: 607, Protein: 20.3, Fiber: 10.6,
            VitaminA: 0, VitaminC: 0, VitaminE: 23.8,
            Calcium: 260, Iron: 3.62, Magnesium: 271, Potassium: 692,
            SaturatedFat: 4.37, Sodium: 3
        );

        var score = DensityScore.Calculate(almonds);

        Assert.Equal(55.1, score!.Value, tolerance: 0.1);
    }

    [Fact]
    public void Calculate_BoiledPotato_ScoresAboveFrenchFries()
    {
        var boiledPotato = new DensityInput(
            Calories: 93, Protein: 1.95, Fiber: 1.5,
            VitaminA: 0, VitaminC: 12.7, VitaminE: 0.04,
            Calcium: 5, Iron: 0.35, Magnesium: 25, Potassium: 389,
            SaturatedFat: 0.026, Sodium: 159
        );
        var frenchFries = new DensityInput(
            Calories: 198, Protein: 1.93, Fiber: 1.6,
            VitaminA: 0, VitaminC: 9.7, VitaminE: 2.5,
            Calcium: 9, Iron: 0.64, Magnesium: 23, Potassium: 401,
            SaturatedFat: 1.76, Sodium: 141
        );

        Assert.True(DensityScore.Calculate(boiledPotato) > DensityScore.Calculate(frenchFries));
    }

    [Fact]
    public void Calculate_VitaminAAboveCap_ScoresSameAsAtCap()
    {
        var spinach = new DensityInput(
            Calories: 27, Protein: 2.85, Fiber: 1.6,
            VitaminA: 283, VitaminC: 26.5, VitaminE: 2.03,
            Calcium: 68, Iron: 1.26, Magnesium: 93, Potassium: 582,
            SaturatedFat: 0.063, Sodium: 111
        );
        var fortified = spinach with { VitaminA = 566 };

        Assert.Equal(DensityScore.Calculate(spinach), DensityScore.Calculate(fortified));
    }

    [Fact]
    public void Calculate_SodiumAboveDailyValue_KeepsSubtracting()
    {
        var soySauce = new DensityInput(
            Calories: 53, Protein: 8.14, Fiber: 0.8,
            VitaminA: 0, VitaminC: 0, VitaminE: 0,
            Calcium: 33, Iron: 1.45, Magnesium: 74, Potassium: 435,
            SaturatedFat: 0.073, Sodium: 5490
        );
        var saltier = soySauce with { Sodium = 10980 };

        Assert.True(DensityScore.Calculate(saltier) < DensityScore.Calculate(soySauce));
    }

    [Fact]
    public void Calculate_ZeroCalorieFood_ReturnsNull()
    {
        var blackCoffee = new DensityInput(
            Calories: 0, Protein: 0.1, Fiber: 0,
            VitaminA: 0, VitaminC: 0, VitaminE: 0,
            Calcium: 2, Iron: 0.05, Magnesium: 5, Potassium: 54,
            SaturatedFat: 0.002, Sodium: 2
        );

        Assert.Null(DensityScore.Calculate(blackCoffee));
    }

    [Fact]
    public void Calculate_NegativeCalories_Throws()
    {
        var corrupt = new DensityInput(
            Calories: -100, Protein: 0, Fiber: 0,
            VitaminA: 0, VitaminC: 0, VitaminE: 0,
            Calcium: 0, Iron: 0, Magnesium: 0, Potassium: 0,
            SaturatedFat: 0, Sodium: 0
        );

        Assert.Throws<ArgumentOutOfRangeException>(() => DensityScore.Calculate(corrupt));
    }

    [Fact]
    public void Calculate_BelowTheCalorieFloor_ScalesFromTheFloor()
    {
        var dietCola = new DensityInput(
            Calories: 2, Protein: 0.11, Fiber: 0,
            VitaminA: 0, VitaminC: 0, VitaminE: 0,
            Calcium: 3, Iron: 0.11, Magnesium: 1, Potassium: 8,
            SaturatedFat: 0, Sodium: 8
        );

        Assert.Equal(
            DensityScore.Calculate(dietCola with { Calories = 10 }),
            DensityScore.Calculate(dietCola));
    }

    [Fact]
    public void Calculate_AboveTheCalorieFloor_UsesTheCaloriesItWasGiven()
    {
        var dietCola = new DensityInput(
            Calories: 2, Protein: 0.11, Fiber: 0,
            VitaminA: 0, VitaminC: 0, VitaminE: 0,
            Calcium: 3, Iron: 0.11, Magnesium: 1, Potassium: 8,
            SaturatedFat: 0, Sodium: 8
        );

        Assert.NotEqual(
            DensityScore.Calculate(dietCola with { Calories = 10 }),
            DensityScore.Calculate(dietCola with { Calories = 20 }));
    }

    [Fact]
    public void Calculate_FoodMissingMicronutrients_ScoresAboveTheSameFoodReportingThemAsZero()
    {
        var reported = new DensityInput(
            Calories: 100, Protein: 10, Fiber: 2, VitaminA: 0, VitaminC: 0, VitaminE: 0,
            Calcium: 0, Iron: 0, Magnesium: 0, Potassium: 0, SaturatedFat: 1, Sodium: 50);

        var unknown = reported with { VitaminA = null, VitaminC = null, VitaminE = null };

        Assert.True(DensityScore.Calculate(unknown) > DensityScore.Calculate(reported));
    }

    [Fact]
    public void Calculate_FoodMissingEveryEncouragedNutrient_HasNoValue()
    {
        var nothing = new DensityInput(
            Calories: 100, Protein: 0, Fiber: 0, VitaminA: null, VitaminC: null, VitaminE: null,
            Calcium: null, Iron: null, Magnesium: null, Potassium: null,
            SaturatedFat: 1, Sodium: 50);

        Assert.Null(DensityScore.Calculate(nothing, new DensityNutrients(
            "none", [], DensityScore.Nrf92.Limited)));
    }

    [Fact]
    public void IsComplete_FoodMissingOneNutrient_IsFalse()
    {
        var complete = new DensityInput(
            Calories: 100, Protein: 10, Fiber: 2, VitaminA: 1, VitaminC: 1, VitaminE: 1,
            Calcium: 1, Iron: 1, Magnesium: 1, Potassium: 1, SaturatedFat: 1, Sodium: 50);

        Assert.True(DensityScore.IsComplete(complete, DensityScore.Nrf92));
        Assert.False(DensityScore.IsComplete(complete with { Magnesium = null },
            DensityScore.Nrf92));
    }
}
