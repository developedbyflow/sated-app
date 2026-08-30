using Sated.Data.Entities;
using Sated.Parsing;

namespace Sated.Parsing.Tests;

public class CatalogueImportTests
{
    private static ImportResult ReadSample()
    {
        using var sample = File.OpenRead("survey-sample.json");
        return CatalogueImport.Read(sample);
    }

    private static Food ChickenBreast() =>
        ReadSample().Accepted.Single(food => food.FdcId == 2705967);

    private static Food Broccoli() =>
        ReadSample().Accepted.Single(food => food.FdcId == 2709643);

    private static Rejection RejectionOf(int fdcId) =>
        ReadSample().Rejected.Single(rejection => rejection.FdcId == fdcId);

    [Fact]
    public void Read_MilkHuman_RejectedForItsCategory()
    {
        Assert.Equal(RejectionReason.OutsideTheSelectedCategories, RejectionOf(2705383).Reason);
    }

    [Fact]
    public void Read_CocoaPowder_RejectedForItsCategory()
    {
        Assert.Equal(RejectionReason.OutsideTheSelectedCategories, RejectionOf(2705587).Reason);
    }

    [Fact]
    public void Read_FrozenOrangeJuice_RejectedAsNotTheEatenForm()
    {
        Assert.Equal(RejectionReason.NotTheEatenForm, RejectionOf(2709191).Reason);
    }

    [Fact]
    public void Read_Sample_AcceptsOnlyTheTwoEatenFoods()
    {
        Assert.Equal([2705967, 2709643], ReadSample().Accepted.Select(food => food.FdcId).Order());
    }

    [Fact]
    public void Read_ChickenBreast_MapsEveryNutrientToItsOwnColumn()
    {
        var nutrients = ChickenBreast().Nutrients;

        Assert.Equal(206, nutrients.Calories);
        Assert.Equal(25.7, nutrients.Protein);
        Assert.Equal(10.6, nutrients.Fat);
        Assert.Equal(0.0, nutrients.Fiber);
        Assert.Equal(2.51, nutrients.SaturatedFat);
        Assert.Equal(329, nutrients.Sodium);
        Assert.Equal(17.0, nutrients.VitaminA);
        Assert.Equal(0.0, nutrients.VitaminC);
        Assert.Equal(0.1, nutrients.VitaminD);
        Assert.Equal(0.96, nutrients.VitaminE);
        Assert.Equal(0.073, nutrients.Thiamine);
        Assert.Equal(7.0, nutrients.Calcium);
        Assert.Equal(0.53, nutrients.Iron);
        Assert.Equal(24.0, nutrients.Magnesium);
        Assert.Equal(292, nutrients.Potassium);
    }

    [Fact]
    public void Read_Broccoli_SeparatesFiberFromVitaminC()
    {
        var nutrients = Broccoli().Nutrients;

        Assert.Equal(2.4, nutrients.Fiber);
        Assert.Equal(91.3, nutrients.VitaminC);
    }

    [Fact]
    public void Read_Broccoli_LeavesNoOptionalNutrientAtZeroByAccident()
    {
        var nutrients = Broccoli().Nutrients;

        Assert.Equal(8.0, nutrients.VitaminA);
        Assert.Equal(0.15, nutrients.VitaminE);
        Assert.Equal(0.0, nutrients.VitaminD);
        Assert.Equal(0.077, nutrients.Thiamine);
        Assert.Equal(46.0, nutrients.Calcium);
        Assert.Equal(0.69, nutrients.Iron);
        Assert.Equal(21.0, nutrients.Magnesium);
        Assert.Equal(303, nutrients.Potassium);
    }

    [Fact]
    public void Read_ChickenBreast_KeepsTheCategoryTheSurveyWrote()
    {
        Assert.Equal("Chicken, whole pieces", ChickenBreast().Category);
    }

    [Fact]
    public void Read_SurveyCarriesNoAminoAcids_LeavesLeucineNull()
    {
        Assert.Null(ChickenBreast().Nutrients.Leucine);
    }
}
