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
        var lens = Shipped.Lenses.Single(candidate => candidate.Id == "weight-loss");

        Assert.Equal(Frozen.WeightLoss.Satiety, lens.Satiety);
        Assert.Equal(Frozen.WeightLoss.Density, lens.Density);
        Assert.Equal(Frozen.WeightLoss.ProteinQuality, lens.ProteinQuality);
    }

    [Fact]
    public void ProteinPerKg_FromShippedFile_MatchesTheFrozenRanges()
    {
        foreach (var frozen in new[] { Frozen.WeightLoss, Frozen.Fitness })
        {
            var shipped = Shipped.Lenses.Single(candidate => candidate.Id == frozen.Id);

            Assert.Equal(frozen.ProteinPerKg!.Min, shipped.ProteinPerKg!.Min);
            Assert.Equal(frozen.ProteinPerKg.Max, shipped.ProteinPerKg.Max);
        }
    }

    [Fact]
    public void ProteinPerKg_FromShippedFile_AsksMoreUnderWeightLossThanUnderFitness()
    {
        var weightLoss = Shipped.Lenses.Single(lens => lens.Id == "weight-loss");
        var fitness = Shipped.Lenses.Single(lens => lens.Id == "fitness");

        Assert.True(weightLoss.ProteinPerKg!.Min > fitness.ProteinPerKg!.Min);
        Assert.True(weightLoss.ProteinPerKg.Max > fitness.ProteinPerKg.Max);
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
                Assert.Equal(cutoffs.GradeForScoreAlone(score), Shipped.ThresholdsFor(lens).GradeForScoreAlone(score));
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
    public void Rules_FromShippedFile_LeaveEveryFatCategoryToFatQualityInstead()
    {
        // These four carried a rule until fat quality became a component of its own. A rule is a
        // switch, and the switch was the defect: a food inside one of these names was lifted to
        // its unsaturated share and a food outside kept the general formula, so honey mustard dip
        // read 10.5 against 42.7 for regular mayonnaise while beating it on every nutrient.
        var fatCategories = new[]
        {
            "Salad dressings and vegetable oils", "Butter and animal fats", "Margarine", "Mayonnaise"
        };

        foreach (var category in fatCategories)
        {
            Assert.Null(Shipped.Rules.Find(category, Frozen.WeightLoss, ScoreComponent.Satiety));
            Assert.Null(Shipped.Rules.Find(category, Frozen.Fitness, ScoreComponent.Satiety));
            Assert.Null(Shipped.Rules.Find(category, Frozen.WeightLoss, ScoreComponent.Density));
        }
    }

    [Fact]
    public void Rules_FromShippedFile_LeaveNutsToFatQualityToo()
    {
        Assert.Null(Shipped.Rules.Find(
            "Nuts and seeds", Frozen.WeightLoss, ScoreComponent.Density));
        Assert.Null(Shipped.Rules.Find(
            "Nuts and seeds", Frozen.WeightLoss, ScoreComponent.Satiety));
    }

    [Fact]
    public void Rules_FromShippedFile_AreNowAllTheSameOne()
    {
        // Every rule left in the file silences the satiety of a drink. Liquidity is the one thing
        // a nutrient cannot report, so the category name is the only signal there is — and the
        // test below proves the coverage instead of trusting the list.
        Assert.All(Shipped.Rules.All, rule => Assert.Equal(ScoreComponent.Satiety, rule.Component));
    }

    [Fact]
    public void Rules_FromShippedFile_CoverEveryDrinkCategoryTheCatalogueCarries()
    {
        // The gap this closes was measured: tonic water is filed under Flavored or carbonated
        // water and ginger ale under Soft drinks, they carry the same 34 kcal of sugar water, and
        // the names alone graded them 49.4 and 4.5. Coffee and Tea are deliberately absent — those
        // categories hold macchiato and milk tea, which carry nutrition this rule does not cover.
        var sugarWater = new[]
        {
            "Soft drinks", "Sport and energy drinks", "Diet sport and energy drinks",
            "Fruit drinks", "Other diet drinks", "Flavored or carbonated water", "Enhanced water"
        };

        foreach (var category in sugarWater)
        {
            Assert.Contains(category, Shipped.CatalogueCategories);

            foreach (var lens in Shipped.Lenses)
            {
                Assert.NotNull(Shipped.Rules.Find(category, lens, ScoreComponent.Satiety));
            }
        }
    }

    [Fact]
    public void Rules_FromShippedFile_SilenceTheSatietyOfEveryDrinkExceptDietSoftDrinks()
    {
        var silenced = new[]
        {
            "Soft drinks", "Sport and energy drinks", "Diet sport and energy drinks"
        };

        foreach (var category in silenced)
        {
            Assert.NotNull(Shipped.Rules.Find(category, Frozen.WeightLoss, ScoreComponent.Satiety));
            Assert.NotNull(Shipped.Rules.Find(category, Frozen.Fitness, ScoreComponent.Satiety));
            Assert.Null(Shipped.Rules.Find(category, Frozen.WeightLoss, ScoreComponent.Density));
        }

        // The one drink category deliberately left on the general formula. A diet cola carries no
        // calories to dilute and lands C, which is the order the product wants — tap water above
        // it, sugared cola below. A diet energy drink is not the same food: it is fortified, so
        // its density read 91.2 at 4 kcal and graded A until P47.
        Assert.Null(Shipped.Rules.Find("Diet soft drinks", Frozen.WeightLoss, ScoreComponent.Satiety));
        Assert.Null(Shipped.Rules.Find("Diet soft drinks", Frozen.Fitness, ScoreComponent.Satiety));
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
    public void GradeFor_DietIcedTeaWhoseDensityCameFromTheCalorieFloor_IsNotFloored()
    {
        // 1 kcal per 100 g: its density of 6.0 was reached by dividing by the 10 kcal floor, so it
        // describes no food. Flooring on it sent the tea to E while a diet cola, identical in every
        // way that matters, kept its C. Same numbers marked Measured still floor — see below.
        var dietIcedTea = new CombinedScore(
            49.8,
            ComponentValue.Measured(96),
            ComponentValue.Estimated(6.0),
            ComponentValue.Measured(0));

        Assert.Equal(Grade.C, Shipped.GradeFor(dietIcedTea, Frozen.WeightLoss));
    }

    [Fact]
    public void GradeFor_TheSameScoreWithAMeasuredDensity_IsStillFloored()
    {
        var measuredInstead = new CombinedScore(
            49.8,
            ComponentValue.Measured(96),
            ComponentValue.Measured(6.0),
            ComponentValue.Measured(0));

        Assert.Equal(Grade.E, Shipped.GradeFor(measuredInstead, Frozen.WeightLoss));
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
        var path = CopyWith("noSatiety", "noSuchStrategy");

        Assert.Throws<ArgumentException>(() => Calibration.Load(path));
    }

    [Fact]
    public void Load_UnknownComponentName_Throws()
    {
        var path = CopyWith("\"component\": \"Satiety\"", "\"component\": \"Vibes\"");

        Assert.Throws<ArgumentException>(() => Calibration.Load(path));
    }

    [Fact]
    public void Load_LensWithoutThresholds_Throws()
    {
        var path = CopyWith("\"thresholds\"", "\"cutoffs\"");

        Assert.Throws<JsonException>(() => Calibration.Load(path));
    }

    [Fact]
    public void Load_LensWithoutAProteinRange_Throws()
    {
        var path = CopyWith("\"proteinPerKg\"", "\"proteinGrams\"");

        Assert.Throws<JsonException>(() => Calibration.Load(path));
    }

    [Fact]
    public void Load_LensWhoseProteinRangeIsASingleNumber_Throws()
    {
        var path = CopyWith(
            "\"proteinPerKg\": { \"min\": 1.6, \"max\": 2.2 }",
            "\"proteinPerKg\": { \"min\": 1.6, \"max\": 1.6 }");

        Assert.Throws<ArgumentOutOfRangeException>(() => Calibration.Load(path));
    }

    [Fact]
    public void Load_TwoLensesUnderTheSameId_Throws()
    {
        var path = CopyWith("\"id\": \"fitness\"", "\"id\": \"weight-loss\"");

        Assert.Throws<ArgumentException>(() => Calibration.Load(path));
    }

    [Fact]
    public void Load_LensWithoutAnId_Throws()
    {
        var path = CopyWith("\"id\": \"fitness\"", "\"slug\": \"fitness\"");

        Assert.Throws<JsonException>(() => Calibration.Load(path));
    }

    [Fact]
    public void Load_LensWithABlankId_Throws()
    {
        var path = CopyWith("\"id\": \"fitness\"", "\"id\": \"   \"");

        Assert.Throws<ArgumentException>(() => Calibration.Load(path));
    }

    [Fact]
    public void ThresholdsFor_LensTheFileDoesNotCalibrate_Throws()
    {
        var uncalibrated = new Lens("maintenance", "Maintenance", satiety: 40, density: 30, proteinQuality: 30);

        Assert.Throws<ArgumentException>(() => Shipped.ThresholdsFor(uncalibrated));
    }

    [Fact]
    public void Load_LensAskingForANutrientSetTheEngineDoesNotHave_Throws()
    {
        var path = CopyWith(
            "\"densityNutrients\": \"nrf9.2\"", "\"densityNutrients\": \"nrf11.2-glp1\"");

        Assert.Throws<ArgumentException>(() => Calibration.Load(path));
    }

    private static string CopyWith(string original, string replacement)
    {
        var path = Path.Combine(Path.GetTempPath(), $"calibration-{Guid.NewGuid()}.json");
        File.WriteAllText(path, File.ReadAllText(ShippedPath).Replace(original, replacement));

        return path;
    }
}
