namespace Sated.Scoring.Tests;

public class ProteinCompletenessTests
{
    [Fact]
    public void IsPlantProtein_GrainCategory_IsTrue()
    {
        Assert.True(ProteinCompleteness.IsPlantProtein("Yeast breads"));
    }

    [Fact]
    public void IsPlantProtein_MeatDish_IsFalse()
    {
        Assert.False(ProteinCompleteness.IsPlantProtein("Meat mixed dishes"));
    }

    [Fact]
    public void IsPlantProtein_CategoryNobodyListed_FallsBackToComplete()
    {
        Assert.False(ProteinCompleteness.IsPlantProtein("Pizza"));
    }

    [Fact]
    public void IsPlantProtein_DifferentCasing_StillMatches()
    {
        Assert.True(ProteinCompleteness.IsPlantProtein("YEAST BREADS"));
    }

    [Fact]
    public void EstimateLeucinePer100g_ChickenBreast_UsesTheAnimalShare()
    {
        Assert.Equal(2.728, ProteinCompleteness.EstimateLeucinePer100g(31, "Chicken, whole pieces"),
            tolerance: 0.001);
    }

    [Fact]
    public void EstimateLeucinePer100g_Lentils_UsesThePlantShare()
    {
        Assert.Equal(0.639, ProteinCompleteness.EstimateLeucinePer100g(9, "Beans, peas, legumes"),
            tolerance: 0.001);
    }

    [Fact]
    public void EstimateLeucinePer100g_SameProteinEitherClass_DiffersByUnderAQuarter()
    {
        var animal = ProteinCompleteness.EstimateLeucinePer100g(20, "Fish");
        var plant = ProteinCompleteness.EstimateLeucinePer100g(20, "Nuts and seeds");

        Assert.True(animal / plant < 1.25);
    }

    [Fact]
    public void EstimateLeucinePer100g_NoProtein_IsZero()
    {
        Assert.Equal(0, ProteinCompleteness.EstimateLeucinePer100g(0, "Coffee"));
    }

    [Fact]
    public void EstimateLeucinePer100g_NegativeProtein_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ProteinCompleteness.EstimateLeucinePer100g(-1, "Fish"));
    }
}
