namespace Sated.Scoring.Tests;

public class PercentileScaleTests
{
    // Density breakpoints 0, 25, 50, 75 and 100 measured on FNDDS — every 25th of the 101
    // in tools/GradeDistributionQuery/percentiles.csv.
    private static readonly double[] MeasuredDensity =
        [-884.5460, 8.7918, 18.8242, 37.0955, 535.6610];

    [Fact]
    public void Normalize_CatalogueMedian_ReturnsFifty()
    {
        var scale = new PercentileScale(MeasuredDensity);

        Assert.Equal(50, scale.Normalize(18.8242));
    }

    [Fact]
    public void Normalize_MidwayBetweenBreakpoints_InterpolatesLinearly()
    {
        var scale = new PercentileScale(MeasuredDensity);

        Assert.Equal(37.5, scale.Normalize(13.8080), tolerance: 0.01);
    }

    [Fact]
    public void Normalize_BuffaloSauce_ReturnsZero()
    {
        var scale = new PercentileScale(MeasuredDensity);

        Assert.Equal(0, scale.Normalize(-884.5460));
    }

    [Fact]
    public void Normalize_BeetGreens_ReturnsHundred()
    {
        var scale = new PercentileScale(MeasuredDensity);

        Assert.Equal(100, scale.Normalize(535.6610));
    }

    [Fact]
    public void Normalize_ScoresBeyondTheCatalogue_FlattenIntoItsEnds()
    {
        var scale = new PercentileScale(MeasuredDensity);

        Assert.Equal(0, scale.Normalize(-50_000));
        Assert.Equal(100, scale.Normalize(50_000));
    }

    [Fact]
    public void Constructor_DecreasingBreakpoints_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PercentileScale([0, 10, 5, 20]));
    }

    [Fact]
    public void Constructor_SingleBreakpoint_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PercentileScale([42]));
    }
}
