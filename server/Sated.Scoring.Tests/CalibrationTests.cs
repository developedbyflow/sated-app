using System.Text.Json;

namespace Sated.Scoring.Tests;

public class CalibrationTests
{
    private static readonly Calibration Shipped = Calibration.Load();

    private static readonly string ShippedPath =
        Path.Combine(AppContext.BaseDirectory, "calibration.json");

    [Fact]
    public void Lenses_FromShippedFile_MatchTheWeightsStillInCode()
    {
        var lens = Shipped.Lenses.Single(candidate => candidate.Name == "Weight Loss");

        Assert.Equal(Lens.WeightLoss.Satiety, lens.Satiety);
        Assert.Equal(Lens.WeightLoss.Density, lens.Density);
        Assert.Equal(Lens.WeightLoss.ProteinQuality, lens.ProteinQuality);
    }

    [Fact]
    public void ThresholdsFor_EachLens_GradeEveryScoreLikeTheCutoffsStillInCode()
    {
        foreach (var lens in new[] { Lens.WeightLoss, Lens.Fitness })
        {
            for (var score = 0.0; score <= 100.0; score += 0.01)
            {
                Assert.Equal(
                    GradeThresholds.For(lens).GradeFor(score),
                    Shipped.ThresholdsFor(lens).GradeFor(score));
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
        Assert.Equal(25, Shipped.DensityScale.Normalize(8.7918), tolerance: 0.001);
        Assert.Equal(50, Shipped.DensityScale.Normalize(18.8242), tolerance: 0.001);
        Assert.Equal(75, Shipped.DensityScale.Normalize(37.0955), tolerance: 0.001);
        Assert.Equal(100, Shipped.DensityScale.Normalize(535.6610), tolerance: 0.001);
    }

    [Fact]
    public void Rules_FromShippedFile_ReplaceTheSameComponentsAsTheTableStillInCode()
    {
        Assert.NotNull(Shipped.Rules.Find(
            "Salad dressings and vegetable oils", Lens.WeightLoss, ScoreComponent.Satiety));
        Assert.NotNull(Shipped.Rules.Find(
            "Nuts and seeds", Lens.Fitness, ScoreComponent.Density));
        Assert.Null(Shipped.Rules.Find(
            "Nuts and seeds", Lens.Fitness, ScoreComponent.Satiety));
        Assert.Null(Shipped.Rules.Find(
            "Chicken, whole pieces", Lens.WeightLoss, ScoreComponent.Density));
    }

    [Fact]
    public void ReferenceMealGrams_FromShippedFile_MatchesTheConstantStillInCode()
    {
        Assert.Equal(ProteinQualityScore.ReferenceMealGrams, Shipped.ReferenceMealGrams);
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
