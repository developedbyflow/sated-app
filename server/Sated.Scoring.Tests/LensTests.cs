namespace Sated.Scoring.Tests;

public class LensTests
{
    [Fact]
    public void WeightLoss_MatchesFr25()
    {
        Assert.Equal(50, Lens.WeightLoss.Satiety);
        Assert.Equal(30, Lens.WeightLoss.Density);
        Assert.Equal(20, Lens.WeightLoss.ProteinQuality);
    }

    [Fact]
    public void Fitness_MatchesFr25()
    {
        Assert.Equal(25, Lens.Fitness.Satiety);
        Assert.Equal(25, Lens.Fitness.Density);
        Assert.Equal(50, Lens.Fitness.ProteinQuality);
    }

    [Fact]
    public void Constructor_WeightsThatDoNotAddUp_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new Lens("Typo", satiety: 50, density: 30, proteinQuality: 30));
    }

    [Fact]
    public void Constructor_NegativeWeightOffsetByAnother_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Lens("Inverted", satiety: -50, density: 100, proteinQuality: 50));
    }

    [Fact]
    public void Constructor_ZeroWeight_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Lens("Blind", satiety: 0, density: 50, proteinQuality: 50));
    }

    [Fact]
    public void Constructor_ThreeWaySplitWithRounding_IsAccepted()
    {
        var even = new Lens("Even", satiety: 33.3, density: 33.3, proteinQuality: 33.4);

        Assert.Equal(33.4, even.ProteinQuality);
    }
}
