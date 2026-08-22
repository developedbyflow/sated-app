namespace Sated.Scoring.Tests;

public class DensityScoreTests
{
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

        Assert.Equal(7.2, score, tolerance: 0.1);
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

        Assert.Equal(6.3, score, tolerance: 0.1);
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

        Assert.Equal(55.1, score, tolerance: 0.1);
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
    public void Calculate_ZeroCalorieFood_Throws()
    {
        var blackCoffee = new DensityInput(
            Calories: 0, Protein: 0.1, Fiber: 0,
            VitaminA: 0, VitaminC: 0, VitaminE: 0,
            Calcium: 2, Iron: 0.05, Magnesium: 5, Potassium: 54,
            SaturatedFat: 0.002, Sodium: 2
        );

        Assert.Throws<ArgumentOutOfRangeException>(() => DensityScore.Calculate(blackCoffee));
    }
}