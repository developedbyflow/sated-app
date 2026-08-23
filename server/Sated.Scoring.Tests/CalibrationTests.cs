using System.Text.Json;

namespace Sated.Scoring.Tests;

public class CalibrationTests
{
    private static readonly Calibration Shipped = Calibration.Load();

    private static readonly string ShippedPath =
        Path.Combine(AppContext.BaseDirectory, "calibration.json");

    [Fact]
    public void Lenses_FromShippedFile_MatchTheFrozenWeights()
    {
        var lens = Shipped.Lenses.Single(candidate => candidate.Name == "Weight Loss");

        Assert.Equal(Frozen.WeightLoss.Satiety, lens.Satiety);
        Assert.Equal(Frozen.WeightLoss.Density, lens.Density);
        Assert.Equal(Frozen.WeightLoss.ProteinQuality, lens.ProteinQuality);
    }

    [Fact]
    public void ThresholdsFor_EachLens_GradeEveryScoreLikeTheFrozenCutoffs()
    {
        var frozen = new[]
        {
            (Lens: Frozen.WeightLoss, Cutoffs: Frozen.WeightLossCutoffs),
            (Lens: Frozen.Fitness, Cutoffs: Frozen.FitnessCutoffs)
        };

        foreach (var (lens, cutoffs) in frozen)
        {
            for (var score = 0.0; score <= 100.0; score += 0.01)
            {
                Assert.Equal(cutoffs.GradeFor(score), Shipped.ThresholdsFor(lens).GradeFor(score));
            }
        }
    }

    [Fact]
    public void SatietyScale_FromShippedFile_PutsEachMeasuredQuartileAtItsPercentile()
    {
        Assert.Equal(0, Shipped.SatietyScale.Normalize(0.5000), tolerance: 0.001);
        Assert.Equal(25, Shipped.SatietyScale.Normalize(1.9635), tolerance: 0.001);
        Assert.Equal(50, Shipped.SatietyScale.Normalize(2.3037), tolerance: 0.001);
        Assert.Equal(75, Shipped.SatietyScale.Normalize(2.8612), tolerance: 0.001);
        Assert.Equal(100, Shipped.SatietyScale.Normalize(4.6769), tolerance: 0.001);
    }

    [Fact]
    public void DensityScale_FromShippedFile_PutsEachMeasuredQuartileAtItsPercentile()
    {
        Assert.Equal(0, Shipped.DensityScale.Normalize(-884.5460), tolerance: 0.001);
        Assert.Equal(25, Shipped.DensityScale.Normalize(8.7432), tolerance: 0.001);
        Assert.Equal(50, Shipped.DensityScale.Normalize(18.6392), tolerance: 0.001);
        Assert.Equal(75, Shipped.DensityScale.Normalize(36.3666), tolerance: 0.001);
        Assert.Equal(100, Shipped.DensityScale.Normalize(535.6610), tolerance: 0.001);
    }

    [Fact]
    public void Rules_FromShippedFile_ReplaceTheSatietyOfEveryFatCategoryUnderBothLenses()
    {
        var fatCategories = new[]
        {
            "Salad dressings and vegetable oils", "Butter and animal fats", "Margarine", "Mayonnaise"
        };

        foreach (var category in fatCategories)
        {
            Assert.NotNull(Shipped.Rules.Find(category, Frozen.WeightLoss, ScoreComponent.Satiety));
            Assert.NotNull(Shipped.Rules.Find(category, Frozen.Fitness, ScoreComponent.Satiety));
            Assert.Null(Shipped.Rules.Find(category, Frozen.WeightLoss, ScoreComponent.Density));
        }
    }

    [Fact]
    public void Rules_FromShippedFile_ReplaceTheDensityOfNutsAndNotTheirSatiety()
    {
        Assert.NotNull(Shipped.Rules.Find(
            "Nuts and seeds", Frozen.WeightLoss, ScoreComponent.Density));
        Assert.Null(Shipped.Rules.Find(
            "Nuts and seeds", Frozen.WeightLoss, ScoreComponent.Satiety));
    }

    [Fact]
    public void Rules_FromShippedFile_SilenceTheSatietyOfDrinksThatCarryCaloriesAndOnlyThose()
    {
        foreach (var category in new[] { "Soft drinks", "Sport and energy drinks" })
        {
            Assert.NotNull(Shipped.Rules.Find(category, Frozen.WeightLoss, ScoreComponent.Satiety));
            Assert.NotNull(Shipped.Rules.Find(category, Frozen.Fitness, ScoreComponent.Satiety));
            Assert.Null(Shipped.Rules.Find(category, Frozen.WeightLoss, ScoreComponent.Density));
        }

        foreach (var category in new[] { "Diet soft drinks", "Diet sport and energy drinks" })
        {
            Assert.Null(Shipped.Rules.Find(category, Frozen.WeightLoss, ScoreComponent.Satiety));
            Assert.Null(Shipped.Rules.Find(category, Frozen.Fitness, ScoreComponent.Satiety));
        }
    }

    [Fact]
    public void Rules_FromShippedFile_LeaveEveryOtherCategoryToTheGeneralFormula()
    {
        Assert.Null(Shipped.Rules.Find(
            "Chicken, whole pieces", Frozen.WeightLoss, ScoreComponent.Satiety));
        Assert.Null(Shipped.Rules.Find(
            "Chicken, whole pieces", Frozen.WeightLoss, ScoreComponent.Density));
    }

    [Fact]
    public void ReferenceMealGrams_FromShippedFile_MatchesTheFrozenMeal()
    {
        Assert.Equal(Frozen.ReferenceMealGrams, Shipped.ReferenceMealGrams);
    }

    [Fact]
    public void DensityFloor_FromShippedFile_MatchesTheFrozenFloor()
    {
        Assert.Equal(Frozen.DensityFloor, Shipped.DensityFloor);
    }

    [Fact]
    public void GradeFor_BaconBelowTheDensityFloor_CannotBeatE()
    {
        var bacon = new CombinedScore(
            47.5,
            ComponentValue.Measured(52.2),
            ComponentValue.Measured(4.5),
            ComponentValue.Measured(100));

        Assert.Equal(Grade.E, Shipped.GradeFor(bacon, Frozen.WeightLoss));
    }

    [Fact]
    public void GradeFor_FlaxseedOilBelowTheFloorButInARuledCategory_KeepsItsLetter()
    {
        var flaxseedOil = new CombinedScore(
            47.44,
            ComponentValue.Measured(91.02),
            ComponentValue.Measured(5.78),
            ComponentValue.Measured(0.97))
        {
            CategoryIsRuled = true
        };

        Assert.Equal(Grade.C, Shipped.GradeFor(flaxseedOil, Frozen.WeightLoss));
    }

    [Fact]
    public void GradeFor_WaterWithNoDensityAtAll_IsNotFloored()
    {
        var water = new CombinedScore(
            68.6,
            ComponentValue.Measured(96),
            Density: null,
            ProteinQuality: ComponentValue.Measured(0));

        Assert.Equal(Grade.B, Shipped.GradeFor(water, Frozen.WeightLoss));
    }

    [Fact]
    public void Load_FileWithoutNotes_Throws()
    {
        var path = CopyWith("\"notes\"", "\"remarks\"");

        Assert.Throws<JsonException>(() => Calibration.Load(path));
    }

    [Fact]
    public void Load_UnknownStrategyName_Throws()
    {
        var path = CopyWith("unsaturatedShare", "noSuchStrategy");

        Assert.Throws<ArgumentException>(() => Calibration.Load(path));
    }

    [Fact]
    public void Load_UnknownComponentName_Throws()
    {
        var path = CopyWith("\"component\": \"Density\"", "\"component\": \"Vibes\"");

        Assert.Throws<ArgumentException>(() => Calibration.Load(path));
    }

    [Fact]
    public void Load_LensWithoutThresholds_Throws()
    {
        var path = CopyWith("\"thresholds\"", "\"cutoffs\"");

        Assert.Throws<JsonException>(() => Calibration.Load(path));
    }

    [Fact]
    public void Load_TwoLensesUnderTheSameName_Throws()
    {
        var path = CopyWith("\"name\": \"Fitness\"", "\"name\": \"Weight Loss\"");

        Assert.Throws<ArgumentException>(() => Calibration.Load(path));
    }

    [Fact]
    public void ThresholdsFor_LensTheFileDoesNotCalibrate_Throws()
    {
        var glp1 = new Lens("GLP-1", satiety: 40, density: 30, proteinQuality: 30);

        Assert.Throws<ArgumentException>(() => Shipped.ThresholdsFor(glp1));
    }

    private static string CopyWith(string original, string replacement)
    {
        var path = Path.Combine(Path.GetTempPath(), $"calibration-{Guid.NewGuid()}.json");
        File.WriteAllText(path, File.ReadAllText(ShippedPath).Replace(original, replacement));

        return path;
    }
}
