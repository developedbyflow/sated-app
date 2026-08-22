namespace Sated.Scoring.Tests;

public class GradeThresholdsTests
{
    [Fact]
    public void GradeFor_ScoreExactlyAtACutoff_TakesTheHigherLetter()
    {
        Assert.Equal(Grade.A, GradeThresholds.WeightLoss.GradeFor(71.77));
    }

    [Fact]
    public void GradeFor_ScoreJustBelowACutoff_TakesTheLowerLetter()
    {
        Assert.Equal(Grade.B, GradeThresholds.WeightLoss.GradeFor(71.76));
    }

    [Fact]
    public void GradeFor_CatalogueFloor_ReturnsE()
    {
        Assert.Equal(Grade.E, GradeThresholds.WeightLoss.GradeFor(0));
    }

    [Fact]
    public void GradeFor_CatalogueCeiling_ReturnsA()
    {
        Assert.Equal(Grade.A, GradeThresholds.WeightLoss.GradeFor(100));
    }

    [Fact]
    public void GradeFor_EveryBand_IsReachable()
    {
        var thresholds = GradeThresholds.WeightLoss;

        Assert.Equal(
            new[] { Grade.E, Grade.D, Grade.C, Grade.B, Grade.A },
            new[] { 10.0, 40.0, 50.0, 65.0, 90.0 }.Select(thresholds.GradeFor));
    }

    [Fact]
    public void For_TheTwoCalibratedLenses_CarryDifferentCutoffs()
    {
        Assert.Equal(Grade.D, GradeThresholds.For(Lens.WeightLoss).GradeFor(32.0));
        Assert.Equal(Grade.E, GradeThresholds.For(Lens.Fitness).GradeFor(32.0));
    }

    [Fact]
    public void For_UncalibratedLens_Throws()
    {
        var glp1 = new Lens("GLP-1", satiety: 30, density: 40, proteinQuality: 30);

        Assert.Throws<ArgumentException>(() => GradeThresholds.For(glp1));
    }

    [Fact]
    public void Constructor_EqualCutoffs_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new GradeThresholds(dStartsAt: 20, cStartsAt: 40, bStartsAt: 40, aStartsAt: 80));
    }

    [Fact]
    public void Constructor_DecreasingCutoffs_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new GradeThresholds(dStartsAt: 20, cStartsAt: 60, bStartsAt: 40, aStartsAt: 80));
    }

    [Fact]
    public void Constructor_CutoffThatLeavesEUnreachable_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GradeThresholds(dStartsAt: 0, cStartsAt: 40, bStartsAt: 60, aStartsAt: 80));
    }

    [Fact]
    public void Constructor_CutoffThatLeavesAUnreachable_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GradeThresholds(dStartsAt: 20, cStartsAt: 40, bStartsAt: 60, aStartsAt: 101));
    }
}
