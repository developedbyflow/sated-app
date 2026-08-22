namespace Sated.Scoring.Tests;

public class ProteinQualityScoreTests
{
    [Fact]
    public void Calculate_ChickenBreast_MatchesConceptExample()
    {
        var score = ProteinQualityScore.Calculate(leucinePer100g: 2.33, grams: 150);

        Assert.Equal(100, score);
    }

    [Fact]
    public void Calculate_Croissant_ScoresFarBelowThreshold()
    {
        var score = ProteinQualityScore.Calculate(leucinePer100g: 0.623, grams: 70);

        Assert.Equal(14.5, score, tolerance: 0.1);
    }

    [Fact]
    public void Calculate_SmallServing_ScoresBelowMealSizedServing()
    {
        var snack = ProteinQualityScore.Calculate(leucinePer100g: 2.33, grams: 30);
        var meal = ProteinQualityScore.Calculate(leucinePer100g: 2.33, grams: 150);

        Assert.True(snack < meal);
    }

    [Fact]
    public void Calculate_LeucineAboveThreshold_ScoresSameAsAtThreshold()
    {
        var atThreshold = ProteinQualityScore.Calculate(leucinePer100g: 2.33, grams: 150);
        var wellAbove = ProteinQualityScore.Calculate(leucinePer100g: 2.33, grams: 300);

        Assert.Equal(atThreshold, wellAbove);
    }

    [Fact]
    public void Calculate_FoodWithoutLeucineData_ReturnsNull()
    {
        var score = ProteinQualityScore.Calculate(leucinePer100g: null, grams: 150);

        Assert.Null(score);
    }

    [Fact]
    public void Calculate_ZeroGrams_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ProteinQualityScore.Calculate(leucinePer100g: 2.33, grams: 0));
    }
}