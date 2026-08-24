namespace Sated.Scoring.Tests;

public class ProteinCompletenessTests
{
    [Fact]
    public void EstimateLeucinePer100g_ChickenBreast_UsesTheMeasuredShare()
    {
        Assert.Equal(2.3312, ProteinCompleteness.EstimateLeucinePer100g(31), tolerance: 0.0001);
    }

    [Fact]
    public void EstimateLeucinePer100g_NoProtein_IsZero()
    {
        Assert.Equal(0, ProteinCompleteness.EstimateLeucinePer100g(0));
    }

    [Fact]
    public void EstimateLeucinePer100g_NegativeProtein_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ProteinCompleteness.EstimateLeucinePer100g(-1));
    }
}
