namespace Sated.Scoring.Tests;

public class ProteinCompletenessTests
{
    [Fact]
    public void EstimateLeucinePer100g_ChickenBreast_UsesTheMeasuredShare()
    {
        Assert.Equal(2.3312, ProteinCompleteness.EstimateLeucinePer100g(31), tolerance: 0.0001);
    }

    [Fact]
    public void EstimateLeucinePer100g_CategoryWithAMeasuredShare_UsesItRatherThanTheMedian()
    {
        // Boiled potato's category measures 6.02% against the catalogue median of 7.52%. If this
        // ever reads equal, the table stopped being consulted and every food quietly went back to
        // one number.
        var measured = ProteinCompleteness.EstimateLeucinePer100g(
            10, "White potatoes, baked or boiled");

        Assert.Equal(0.602, measured, tolerance: 0.0001);
        Assert.NotEqual(ProteinCompleteness.EstimateLeucinePer100g(10), measured, tolerance: 0.0001);
    }

    [Fact]
    public void EstimateLeucinePer100g_CategoryNobodyMeasured_FallsBackToTheMedian()
    {
        // A category name from another catalogue. The 125 measured ones cover this catalogue, and
        // a food arriving from anywhere else must take the median rather than throw or read zero.
        Assert.Equal(
            ProteinCompleteness.EstimateLeucinePer100g(10),
            ProteinCompleteness.EstimateLeucinePer100g(10, "Huiles d'olive"),
            tolerance: 0.0001);
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
