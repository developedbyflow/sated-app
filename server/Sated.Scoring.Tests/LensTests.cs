namespace Sated.Scoring.Tests;

public class LensTests
{


    [Fact]
    public void Constructor_WeightsThatDoNotAddUp_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new Lens("typo", "Typo", satiety: 50, density: 30, proteinQuality: 30));
    }

    [Fact]
    public void Constructor_NegativeWeightOffsetByAnother_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Lens("inverted", "Inverted", satiety: -50, density: 100, proteinQuality: 50));
    }

    [Fact]
    public void Constructor_ZeroWeight_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Lens("blind", "Blind", satiety: 0, density: 50, proteinQuality: 50));
    }

    [Fact]
    public void Constructor_ThreeWaySplitWithRounding_IsAccepted()
    {
        var even = new Lens("even", "Even", satiety: 33.3, density: 33.3, proteinQuality: 33.4);

        Assert.Equal(33.4, even.ProteinQuality);
    }
}
