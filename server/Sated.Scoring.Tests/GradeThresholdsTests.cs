namespace Sated.Scoring.Tests;

public class GradeThresholdsTests
{
    [Fact]
    public void GradeForScoreAlone_ScoreExactlyAtACutoff_TakesTheHigherLetter()
    {
        Assert.Equal(Grade.A, Frozen.WeightLossCutoffs.GradeForScoreAlone(72.01));
    }

    [Fact]
    public void GradeForScoreAlone_ScoreJustBelowACutoff_TakesTheLowerLetter()
    {
        Assert.Equal(Grade.B, Frozen.WeightLossCutoffs.GradeForScoreAlone(72.00));
    }

    [Fact]
    public void GradeForScoreAlone_CatalogueFloor_ReturnsE()
    {
        Assert.Equal(Grade.E, Frozen.WeightLossCutoffs.GradeForScoreAlone(0));
    }

    [Fact]
    public void GradeForScoreAlone_CatalogueCeiling_ReturnsA()
    {
        Assert.Equal(Grade.A, Frozen.WeightLossCutoffs.GradeForScoreAlone(100));
    }

    [Fact]
    public void GradeForScoreAlone_EveryBand_IsReachable()
    {
        var thresholds = Frozen.WeightLossCutoffs;

        Assert.Equal(
            new[] { Grade.E, Grade.D, Grade.C, Grade.B, Grade.A },
            new[] { 10.0, 40.0, 50.0, 65.0, 90.0 }.Select(thresholds.GradeForScoreAlone));
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
