namespace Sated.Scoring.Tests;

public class ScoreCombinerTests
{
    // Breakpoints 0, 25, 50, 75 and 100 measured on FNDDS — every 25th of the 101 in
    // tools/GradeDistributionQuery/percentiles.csv.
    private static readonly double[] MeasuredSatiety =
        [0.5000, 1.9635, 2.3037, 2.8612, 4.6769];

    private static readonly double[] MeasuredDensity =
        [-884.5460, 8.7918, 18.8242, 37.0955, 535.6610];

    private static readonly FoodInput ChickenBreast = new(
        Category: "Chicken, whole pieces",
        Calories: 165, Protein: 31, Fat: 3.6, Fiber: 0, VitaminA: 9, VitaminC: 0, VitaminE: 0.27,
        Calcium: 15, Iron: 1, Magnesium: 29, Potassium: 256, SaturatedFat: 1, Sodium: 74);

    private static readonly FoodInput SparklingWater = new(
        Category: "Enhanced water",
        Calories: 0, Protein: 0, Fat: 0, Fiber: 0, VitaminA: 0, VitaminC: 0, VitaminE: 0,
        Calcium: 0, Iron: 0, Magnesium: 0, Potassium: 0, SaturatedFat: 0, Sodium: 4);

    private static ScoreCombiner Combiner() =>
        new(new PercentileScale(MeasuredSatiety), new PercentileScale(MeasuredDensity),
            Frozen.ReferenceMealGrams);

    private static GeneralStrategies General() =>
        new(new PercentileScale(MeasuredSatiety), new PercentileScale(MeasuredDensity),
            Frozen.ReferenceMealGrams);

    private static ScoreCombiner CombinerWith(params CategoryRule[] rules) =>
        new(General(), new CategoryRules(rules));

    [Fact]
    public void Combine_RuleThatHasNoAnswer_FallsBackToTheGeneralFormula()
    {
        var combiner = CombinerWith(new CategoryRule(
            ChickenBreast.Category, Frozen.WeightLoss.Name, ScoreComponent.Satiety,
            food => null));

        Assert.Equal(
            Combiner().Combine(ChickenBreast, Frozen.WeightLoss).Satiety.Score,
            combiner.Combine(ChickenBreast, Frozen.WeightLoss).Satiety.Score);
    }

    [Fact]
    public void Combine_RuleThatHasNoAnswerOnDensity_KeepsTheComponentInsteadOfDroppingIt()
    {
        var combiner = CombinerWith(new CategoryRule(
            ChickenBreast.Category, Frozen.WeightLoss.Name, ScoreComponent.Density,
            food => null));

        Assert.Equal(
            Combiner().Combine(ChickenBreast, Frozen.WeightLoss).Density!.Score,
            combiner.Combine(ChickenBreast, Frozen.WeightLoss).Density!.Score);
    }

    [Fact]
    public void Combine_RuleOnProtein_FillsAComponentTheGeneralFormulaLeavesEmpty()
    {
        var combiner = CombinerWith(new CategoryRule(
            ChickenBreast.Category, Frozen.WeightLoss.Name, ScoreComponent.ProteinQuality,
            food => ComponentValue.Measured(80)));

        var score = combiner.Combine(ChickenBreast, Frozen.WeightLoss);

        Assert.Equal(80, score.ProteinQuality!.Score);
        Assert.False(score.IsPartial);
    }

    [Fact]
    public void Combine_RuleOnDensity_OutweighsTheGeneralFormula()
    {
        var combiner = CombinerWith(new CategoryRule(
            ChickenBreast.Category, Frozen.WeightLoss.Name, ScoreComponent.Density,
            food => ComponentValue.Measured(0)));

        Assert.True(
            combiner.Combine(ChickenBreast, Frozen.WeightLoss).Value <
            Combiner().Combine(ChickenBreast, Frozen.WeightLoss).Value);
    }

    [Fact]
    public void Combine_RuleForAnotherLens_DoesNotApply()
    {
        var combiner = CombinerWith(new CategoryRule(
            ChickenBreast.Category, Frozen.Fitness.Name, ScoreComponent.ProteinQuality,
            food => ComponentValue.Measured(80)));

        Assert.True(
            combiner.Combine(ChickenBreast, Frozen.WeightLoss).ProteinQuality!.IsEstimated);
    }

    [Fact]
    public void Combine_RuleForAnotherCategory_DoesNotApply()
    {
        var combiner = CombinerWith(new CategoryRule(
            "Butter and animal fats", Frozen.WeightLoss.Name, ScoreComponent.ProteinQuality,
            food => ComponentValue.Measured(80)));

        Assert.True(
            combiner.Combine(ChickenBreast, Frozen.WeightLoss).ProteinQuality!.IsEstimated);
    }

    [Fact]
    public void Combine_RuleWrittenInAnotherCasing_StillApplies()
    {
        var combiner = CombinerWith(new CategoryRule(
            ChickenBreast.Category.ToUpperInvariant(), Frozen.WeightLoss.Name,
            ScoreComponent.ProteinQuality, food => ComponentValue.Measured(80)));

        Assert.Equal(80, combiner.Combine(ChickenBreast, Frozen.WeightLoss).ProteinQuality?.Score);
    }

    [Fact]
    public void Combine_GeneralFormula_MarksEveryComponentMeasured()
    {
        var score = Combiner().Combine(
            ChickenBreast with { LeucinePer100g = 2.3 }, Frozen.WeightLoss);

        Assert.False(score.HasEstimatedComponents);
    }

    [Fact]
    public void Combine_RuleThatEstimates_MarksTheScoreAsCarryingAnEstimate()
    {
        var combiner = CombinerWith(new CategoryRule(
            ChickenBreast.Category, Frozen.WeightLoss.Name, ScoreComponent.ProteinQuality,
            food => ComponentValue.Estimated(80)));

        var score = combiner.Combine(ChickenBreast, Frozen.WeightLoss);

        Assert.False(score.IsPartial);
        Assert.True(score.HasEstimatedComponents);
    }

    [Fact]
    public void Combine_ChickenBreastUnderWeightLoss_WeightsAllThreeComponents()
    {
        var score = Combiner().Combine(ChickenBreast with { LeucinePer100g = 0.5 }, Frozen.WeightLoss);

        Assert.Equal(73.0553, score.Value, tolerance: 0.0001);
    }

    [Fact]
    public void Combine_ChickenBreastUnderFitness_ScoresDifferentlyFromWeightLoss()
    {
        var score = Combiner().Combine(ChickenBreast with { LeucinePer100g = 0.5 }, Frozen.Fitness);

        Assert.Equal(64.0715, score.Value, tolerance: 0.0001);
    }

    [Fact]
    public void Combine_WithoutLeucineData_ScoresHigherThanWithZeroLeucine()
    {
        var combiner = Combiner();

        Assert.True(
            combiner.Combine(ChickenBreast, Frozen.WeightLoss).Value >
            combiner.Combine(ChickenBreast with { LeucinePer100g = 0 }, Frozen.WeightLoss).Value);
    }

    [Fact]
    public void Combine_WithoutLeucineData_IsNotPartialButSaysTheComponentWasGuessed()
    {
        var score = Combiner().Combine(ChickenBreast, Frozen.WeightLoss);

        Assert.False(score.IsPartial);
        Assert.True(score.ProteinQuality!.IsEstimated);
    }

    [Fact]
    public void Combine_WithEveryComponent_DoesNotMarkTheScorePartial()
    {
        var score = Combiner().Combine(ChickenBreast with { LeucinePer100g = 2.3 }, Frozen.WeightLoss);

        Assert.False(score.IsPartial);
    }

    [Fact]
    public void Combine_ZeroCalorieFood_LeavesDensityOutOfTheScore()
    {
        var score = Combiner().Combine(
            SparklingWater, Frozen.WeightLoss);

        Assert.Null(score.Density);
    }

    [Fact]
    public void Combine_ZeroCalorieFood_RedistributesTheDensityWeight()
    {
        var score = Combiner().Combine(SparklingWater, Frozen.WeightLoss);

        Assert.Equal(score.Satiety.Score * 50 / 70, score.Value);
    }

    [Fact]
    public void Combine_ChickenBreastWithoutLeucineData_StillGradesAOrB()
    {
        var score = Combiner().Combine(ChickenBreast, Frozen.WeightLoss);

        var grade = Frozen.WeightLossCutoffs.GradeFor(score.Value);

        Assert.True(grade == Grade.A || grade == Grade.B);
    }

    [Fact]
    public void Combine_ChickenBreastWithoutLeucineData_GradesAboveZeroLeucine()
    {
        var combiner = Combiner();
        var thresholds = Frozen.WeightLossCutoffs;

        Assert.Equal(
            new[] { Grade.A, Grade.B },
            new[]
            {
                combiner.Combine(ChickenBreast, Frozen.WeightLoss),
                combiner.Combine(ChickenBreast with { LeucinePer100g = 0 }, Frozen.WeightLoss)
            }.Select(score => thresholds.GradeFor(score.Value)));
    }
}
