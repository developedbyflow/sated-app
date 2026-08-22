namespace Sated.Scoring.Tests;

public class ScoreCombinerTests
{
    // Breakpoints 0, 25, 50, 75 and 100 measured on FNDDS — every 25th of the 101 in
    // tools/GradeDistributionQuery/percentiles.csv.
    private static readonly double[] MeasuredSatiety =
        [0.5000, 1.9635, 2.3037, 2.8612, 4.6769];

    private static readonly double[] MeasuredDensity =
        [-884.5460, 8.7918, 18.8242, 37.0955, 535.6610];

    private static readonly SatietyInput ChickenSatiety =
        new(Calories: 165, Protein: 31, Fat: 3.6, Fiber: 0);

    private static readonly DensityInput ChickenDensity = new(
        Calories: 165, Protein: 31, Fiber: 0, VitaminA: 9, VitaminC: 0, VitaminE: 0.27,
        Calcium: 15, Iron: 1, Magnesium: 29, Potassium: 256, SaturatedFat: 1, Sodium: 74);

    private static readonly SatietyInput SparklingWaterSatiety =
        new(Calories: 0, Protein: 0, Fat: 0, Fiber: 0);

    private static readonly DensityInput SparklingWaterDensity = new(
        Calories: 0, Protein: 0, Fiber: 0, VitaminA: 0, VitaminC: 0, VitaminE: 0,
        Calcium: 0, Iron: 0, Magnesium: 0, Potassium: 0, SaturatedFat: 0, Sodium: 4);

    private static ScoreCombiner Combiner() =>
        new(new PercentileScale(MeasuredSatiety), new PercentileScale(MeasuredDensity));

    [Fact]
    public void Combine_ChickenBreastUnderWeightLoss_WeightsAllThreeComponents()
    {
        var score = Combiner().Combine(ChickenSatiety, ChickenDensity, 2.3, 100, Lens.WeightLoss);

        Assert.Equal(78.3886, score.Value, tolerance: 0.0001);
    }

    [Fact]
    public void Combine_ChickenBreastUnderFitness_ScoresDifferentlyFromWeightLoss()
    {
        var score = Combiner().Combine(ChickenSatiety, ChickenDensity, 2.3, 100, Lens.Fitness);

        Assert.Equal(77.4049, score.Value, tolerance: 0.0001);
    }

    [Fact]
    public void Combine_WithoutLeucineData_RedistributesFiftyThirtyToSixtyTwoFiveThirtySevenFive()
    {
        var score = Combiner().Combine(ChickenSatiety, ChickenDensity, null, 100, Lens.WeightLoss);

        Assert.Equal(
            (62.5 * score.Satiety + 37.5 * score.Density!.Value) / 100,
            score.Value);
    }

    [Fact]
    public void Combine_WithoutLeucineData_ScoresHigherThanWithZeroLeucine()
    {
        var combiner = Combiner();

        Assert.True(
            combiner.Combine(ChickenSatiety, ChickenDensity, null, 100, Lens.WeightLoss).Value >
            combiner.Combine(ChickenSatiety, ChickenDensity, 0, 100, Lens.WeightLoss).Value);
    }

    [Fact]
    public void Combine_WithoutLeucineData_MarksTheScorePartial()
    {
        var score = Combiner().Combine(ChickenSatiety, ChickenDensity, null, 100, Lens.WeightLoss);

        Assert.True(score.IsPartial);
    }

    [Fact]
    public void Combine_WithEveryComponent_DoesNotMarkTheScorePartial()
    {
        var score = Combiner().Combine(ChickenSatiety, ChickenDensity, 2.3, 100, Lens.WeightLoss);

        Assert.False(score.IsPartial);
    }

    [Fact]
    public void Combine_ZeroCalorieFood_LeavesDensityOutOfTheScore()
    {
        var score = Combiner().Combine(
            SparklingWaterSatiety, SparklingWaterDensity, null, 100, Lens.WeightLoss);

        Assert.Null(score.Density);
    }

    [Fact]
    public void Combine_ZeroCalorieFoodWithoutLeucine_ScoresOnSatietyAlone()
    {
        var score = Combiner().Combine(
            SparklingWaterSatiety, SparklingWaterDensity, null, 100, Lens.WeightLoss);

        Assert.Equal(score.Satiety, score.Value);
    }

    [Fact]
    public void Combine_ChickenBreastWithoutLeucineData_StillGradesAOrB()
    {
        var score = Combiner().Combine(ChickenSatiety, ChickenDensity, null, 100, Lens.WeightLoss);

        var grade = GradeThresholds.WeightLoss.GradeFor(score.Value);

        Assert.True(grade == Grade.A || grade == Grade.B);
    }

    [Fact]
    public void Combine_ChickenBreastWithoutLeucineData_GradesAboveZeroLeucine()
    {
        var combiner = Combiner();
        var thresholds = GradeThresholds.WeightLoss;

        Assert.Equal(
            new[] { Grade.A, Grade.B },
            new[]
            {
                combiner.Combine(ChickenSatiety, ChickenDensity, null, 100, Lens.WeightLoss),
                combiner.Combine(ChickenSatiety, ChickenDensity, 0, 100, Lens.WeightLoss)
            }.Select(score => thresholds.GradeFor(score.Value)));
    }

    [Fact]
    public void Combine_LargerPortion_RaisesOnlyProteinQuality()
    {
        var combiner = Combiner();

        var hundredGrams = combiner.Combine(ChickenSatiety, ChickenDensity, 1, 100, Lens.Fitness);
        var twoHundredGrams = combiner.Combine(ChickenSatiety, ChickenDensity, 1, 200, Lens.Fitness);

        Assert.Equal(hundredGrams.Satiety, twoHundredGrams.Satiety);
        Assert.Equal(hundredGrams.Density, twoHundredGrams.Density);
        Assert.True(twoHundredGrams.ProteinQuality > hundredGrams.ProteinQuality);
    }
}
