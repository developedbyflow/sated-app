using Sated.Parsing;

namespace Sated.Parsing.Tests;

public class SurveyPortionsTests
{
    [Fact]
    public void Of_PortionsOutOfOrderInTheFile_ComesBackInSequenceOrder()
    {
        var egg = Surveyed(
            new SurveyPortion(243, "1 cup (4.86 large eggs)", 5),
            new SurveyPortion(44, "1 medium", 6),
            new SurveyPortion(50, "1 large", 1));

        var servings = SurveyPortions.Of(egg);

        Assert.Equal(["1 large", "1 cup (4.86 large eggs)", "1 medium"],
            servings.Select(serving => serving.Description));
    }

    [Fact]
    public void Of_QuantityNotSpecified_IsNotOfferedAsAServing()
    {
        var egg = Surveyed(
            new SurveyPortion(50, "1 egg", 1),
            new SurveyPortion(50, SurveyPortions.QuantityNotSpecified, 4));

        var servings = SurveyPortions.Of(egg);

        Assert.Equal("1 egg", Assert.Single(servings).Description);
    }

    [Fact]
    public void Of_APortionWithNoWeight_IsDropped()
    {
        var milk = Surveyed(
            new SurveyPortion(0, "1 serving", 1),
            new SurveyPortion(244, "1 cup", 2));

        Assert.Equal("1 cup", Assert.Single(SurveyPortions.Of(milk)).Description);
    }

    [Fact]
    public void TypicalGramsOf_AFoodWithTheUnspecifiedRow_IsThatWeight()
    {
        var egg = Surveyed(
            new SurveyPortion(135, "1 cup", 2),
            new SurveyPortion(50, SurveyPortions.QuantityNotSpecified, 4));

        Assert.Equal(50, SurveyPortions.TypicalGramsOf(egg));
    }

    [Fact]
    public void TypicalGramsOf_AFoodWhoseUnspecifiedRowWeighsNothing_IsNull()
    {
        var milk = Surveyed(
            new SurveyPortion(246, "1 cup", 1),
            new SurveyPortion(0, SurveyPortions.QuantityNotSpecified, 2));

        Assert.Null(SurveyPortions.TypicalGramsOf(milk));
    }

    [Fact]
    public void TypicalGramsOf_AFoodWithoutTheUnspecifiedRow_IsNullRatherThanTheFirstPortion()
    {
        var milk = Surveyed(new SurveyPortion(246, "1 cup", 1));

        Assert.Null(SurveyPortions.TypicalGramsOf(milk));
    }

    private static SurveyFood Surveyed(params SurveyPortion[] portions) =>
        new(1, "Whatever", [], new SurveyCategory("Cheese"), portions);
}
