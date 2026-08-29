namespace Sated.Scoring.Tests;

public class PortionAggregateTests
{
    private static readonly double[] MeasuredSatiety =
        [0.5000, 1.9635, 2.3037, 2.8612, 4.6769];

    private static readonly double[] MeasuredDensity =
        [-884.5460, 8.7918, 18.8242, 37.0955, 535.6610];

    private static readonly FoodInput Spinach = new(
        Category: "Vegetables, raw",
        Calories: 23, Protein: 2.86, Fat: 0.39, Fiber: 2.2, VitaminA: 469, VitaminC: 28.1,
        VitaminE: 2.03, Calcium: 99, Iron: 2.71, Magnesium: 79, Potassium: 558,
        SaturatedFat: 0.063, Sodium: 79, VitaminD: 0, Thiamine: 0);

    private static readonly FoodInput Butter = new(
        Category: "Butter and animal fats",
        Calories: 717, Protein: 0.85, Fat: 81.11, Fiber: 0, VitaminA: 684, VitaminC: 0,
        VitaminE: 2.32, Calcium: 24, Iron: 0.02, Magnesium: 2, Potassium: 24,
        SaturatedFat: 51.37, Sodium: 643, VitaminD: 0, Thiamine: 0);

    private static readonly FoodInput Rice = new(
        Category: "Rice",
        Calories: 130, Protein: 2.69, Fat: 0.28, Fiber: 0.4, VitaminA: 0, VitaminC: 0,
        VitaminE: 0.04, Calcium: 10, Iron: 1.2, Magnesium: 12, Potassium: 35,
        SaturatedFat: 0.077, Sodium: 1, VitaminD: 0, Thiamine: 0);

    private static readonly FoodInput ChickenBreast = new(
        Category: "Chicken, whole pieces",
        Calories: 165, Protein: 31, Fat: 3.6, Fiber: 0, VitaminA: 9, VitaminC: 0, VitaminE: 0.27,
        Calcium: 15, Iron: 1, Magnesium: 29, Potassium: 256, SaturatedFat: 1, Sodium: 74, VitaminD: 0, Thiamine: 0);

    private static ScoreCombiner Combiner() =>
        new(new PercentileScale(MeasuredSatiety), new PercentileScale(MeasuredDensity),
            Frozen.ReferenceMealGrams);

    [Fact]
    public void Aggregate_SinglePortion_RestatesTheFoodPer100g()
    {
        var profile = PortionAggregate.Aggregate([new Portion(Spinach, 250)]);

        Assert.Equal(Spinach.Calories, profile.Calories, tolerance: 0.0001);
        Assert.Equal(Spinach.Potassium!.Value, profile.Potassium!.Value, tolerance: 0.0001);
    }

    [Fact]
    public void Aggregate_EqualWeights_LandsHalfwayBetweenTheTwo()
    {
        var profile = PortionAggregate.Aggregate([new Portion(Spinach, 100), new Portion(Butter, 100)]);

        Assert.Equal(370, profile.Calories, tolerance: 0.0001);
    }

    [Fact]
    public void Aggregate_HeavierPortion_PullsTheProfileTowardIt()
    {
        var profile = PortionAggregate.Aggregate([new Portion(Spinach, 300), new Portion(Butter, 100)]);

        Assert.Equal(196.5, profile.Calories, tolerance: 0.0001);
    }

    [Fact]
    public void Aggregate_SpinachAndButter_ScoresBelowTheAverageOfTheTwo()
    {
        var combiner = Combiner();
        var mixture = PortionAggregate.Aggregate([new Portion(Spinach, 100), new Portion(Butter, 100)]);
        var averaged = (combiner.Combine(Spinach, Frozen.WeightLoss).Value
            + combiner.Combine(Butter, Frozen.WeightLoss).Value) / 2;

        var score = combiner.Combine(mixture, Frozen.WeightLoss).Value;

        Assert.True(score < averaged);
    }

    [Fact]
    public void Aggregate_OnlyFatPortions_IsGradedLikeTheFatItself()
    {
        var combiner = new ScoreCombiner(
            new GeneralStrategies(
                new PercentileScale(MeasuredSatiety), new PercentileScale(MeasuredDensity),
                Frozen.ReferenceMealGrams),
            new CategoryRules([new CategoryRule(
                Butter.Category, Frozen.WeightLoss.Id, ScoreComponent.Satiety,
                FatQuality.UnsaturatedShare)]));
        var mixture = PortionAggregate.Aggregate([new Portion(Butter, 100)]);

        var score = combiner.Combine(mixture, Frozen.WeightLoss).Value;

        // A plate that is nothing but butter is butter. It used to differ, because the aggregate
        // is given a category no catalogue carries and so lost the rule the food had.
        Assert.Equal(combiner.Combine(Butter, Frozen.WeightLoss).Value, score);
    }

    [Fact]
    public void Aggregate_NoPortions_Throws()
    {
        Assert.Throws<ArgumentException>(() => PortionAggregate.Aggregate([]));
    }

    [Fact]
    public void Aggregate_PortionWithoutWeight_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PortionAggregate.Aggregate([new Portion(Spinach, 0)]));
    }

    [Fact]
    public void Aggregate_PortionWithoutLeucineData_EstimatesFromItsProtein()
    {
        var profile = PortionAggregate.Aggregate([new Portion(Rice, 200)]);

        Assert.Equal(Rice.Protein * 0.0752, profile.LeucinePer100g!.Value, tolerance: 0.0001);
    }

    [Fact]
    public void Aggregate_PortionWithoutLeucineData_MarksTheAggregateEstimated()
    {
        var profile = PortionAggregate.Aggregate([new Portion(Rice, 100)]);

        Assert.True(profile.LeucineIsEstimated);
    }

    [Fact]
    public void Aggregate_PortionsWithMeasuredLeucine_LeavesTheAggregateMeasured()
    {
        var profile = PortionAggregate.Aggregate(
            [new Portion(Rice with { LeucinePer100g = 0.2 }, 100),
             new Portion(ChickenBreast with { LeucinePer100g = 2.3 }, 100)]);

        Assert.False(profile.LeucineIsEstimated);
    }

    [Fact]
    public void Aggregate_OneIngredientMissingLeucine_MarksTheWholePlateEstimated()
    {
        var profile = PortionAggregate.Aggregate(
            [new Portion(Rice with { LeucinePer100g = 0.2 }, 100), new Portion(ChickenBreast, 100)]);

        Assert.True(profile.LeucineIsEstimated);
    }

    [Fact]
    public void Aggregate_NestedAggregate_KeepsCarryingTheEstimate()
    {
        var recipe = PortionAggregate.Aggregate([new Portion(Rice, 100)]);

        var meal = PortionAggregate.Aggregate(
            [new Portion(recipe, 150), new Portion(ChickenBreast with { LeucinePer100g = 2.3 }, 100)]);

        Assert.True(meal.LeucineIsEstimated);
    }

    [Fact]
    public void Combine_AggregateWithEstimatedLeucine_ReportsTheComponentAsEstimated()
    {
        var mixture = PortionAggregate.Aggregate([new Portion(Rice, 100)]);

        var score = Combiner().Combine(mixture, Frozen.Fitness);

        Assert.True(score.ProteinQuality!.IsEstimated);
    }

    [Fact]
    public void Combine_PlateThatIsAlmostEntirelyOil_IsJudgedAsTheFatItIs()
    {
        var oil = new FoodInput(
            Category: "Salad dressings and vegetable oils",
            Calories: 884, Protein: 0, Fat: 100, Fiber: 0, VitaminA: 0, VitaminC: 0, VitaminE: 14,
            Calcium: 1, Iron: 0.6, Magnesium: 0, Potassium: 1, SaturatedFat: 13.8, Sodium: 2,
            VitaminD: 0, Thiamine: 0);

        var lettuce = new FoodInput(
            Category: "Lettuce and lettuce salads",
            Calories: 15, Protein: 1.4, Fat: 0.2, Fiber: 1.3, VitaminA: 370, VitaminC: 9.2,
            VitaminE: 0.2, Calcium: 36, Iron: 0.9, Magnesium: 13, Potassium: 194,
            SaturatedFat: 0, Sodium: 28, VitaminD: 0, Thiamine: 0.07);

        var plate = PortionAggregate.Aggregate([new Portion(oil, 100), new Portion(lettuce, 10)]);

        var combiner = new ScoreCombiner(
            new GeneralStrategies(new PercentileScale(MeasuredSatiety),
                new PercentileScale(MeasuredDensity), Frozen.ReferenceMealGrams),
            new CategoryRules([], new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Salad dressings and vegetable oils", "Lettuce and lettuce salads" }));

        Assert.Equal(
            FatQuality.UnsaturatedShare(plate)!.Score,
            combiner.Combine(plate, Frozen.WeightLoss).FatQuality!.Score);
    }
}
