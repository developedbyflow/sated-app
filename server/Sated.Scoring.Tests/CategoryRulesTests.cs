namespace Sated.Scoring.Tests;

public class CategoryRulesTests
{
    private static CategoryRule Rule(string category, ScoreComponent component) =>
        new(category, Lens.WeightLoss.Name, component, (food, grams) => 50);

    [Fact]
    public void Find_PairingNobodyRegistered_ReturnsNull()
    {
        Assert.Null(CategoryRules.None.Find("Pizza", Lens.WeightLoss, ScoreComponent.Density));
    }

    [Fact]
    public void Find_RegisteredPairing_ReturnsTheStrategy()
    {
        var rules = new CategoryRules([Rule("Butter and animal fats", ScoreComponent.Density)]);

        Assert.NotNull(rules.Find("Butter and animal fats", Lens.WeightLoss, ScoreComponent.Density));
    }

    [Fact]
    public void Find_SameCategoryOtherComponent_ReturnsNull()
    {
        var rules = new CategoryRules([Rule("Butter and animal fats", ScoreComponent.Density)]);

        Assert.Null(rules.Find("Butter and animal fats", Lens.WeightLoss, ScoreComponent.Satiety));
    }

    [Fact]
    public void Constructor_TwoRulesOverTheSameComponent_Throws()
    {
        Assert.Throws<ArgumentException>(() => new CategoryRules(
        [
            Rule("Butter and animal fats", ScoreComponent.Density),
            Rule("Butter and animal fats", ScoreComponent.Density)
        ]));
    }

    [Fact]
    public void Constructor_SameCategoryDifferentComponents_IsAccepted()
    {
        var rules = new CategoryRules(
        [
            Rule("Butter and animal fats", ScoreComponent.Density),
            Rule("Butter and animal fats", ScoreComponent.Satiety)
        ]);

        Assert.NotNull(rules.Find("Butter and animal fats", Lens.WeightLoss, ScoreComponent.Satiety));
    }
}
