using Sated.Data.Entities;
using Sated.Scoring;
using Sated.Services;

namespace Sated.Services.Tests;

public class ScoringInputTests
{
    [Fact]
    public void From_LandsEveryNutrientInTheFieldThatMeansTheSameThing()
    {
        var food = Catalogued(new NutrientAmounts
        {
            Calories = 61,
            Protein = 3.27,
            Fat = 3.2,
            Fiber = 1.1,
            SaturatedFat = 1.86,
            Sodium = 38,
            VitaminA = 32,
            VitaminC = 2.4,
            VitaminD = 1.3,
            VitaminE = 0.05,
            Thiamine = 0.056,
            Calcium = 123,
            Iron = 0.03,
            Magnesium = 12,
            Potassium = 150,
            Leucine = 0.31
        });

        var input = ScoringInput.From(food);

        Assert.Equal(
            new FoodInput(
                Category: "Cheese",
                Calories: 61,
                Protein: 3.27,
                Fat: 3.2,
                Fiber: 1.1,
                VitaminA: 32,
                VitaminC: 2.4,
                VitaminE: 0.05,
                Calcium: 123,
                Iron: 0.03,
                Magnesium: 12,
                Potassium: 150,
                SaturatedFat: 1.86,
                Sodium: 38,
                VitaminD: 1.3,
                Thiamine: 0.056,
                LeucinePer100g: 0.31,
                LeucineIsEstimated: false),
            input);
    }

    [Fact]
    public void From_ALeucineTheCatalogueCarries_IsNotMarkedEstimated()
    {
        var food = Catalogued(Nutrients(leucine: 0.31));

        var input = ScoringInput.From(food);

        Assert.Equal(0.31, input.LeucinePer100g);
        Assert.False(input.LeucineIsEstimated);
    }

    [Fact]
    public void From_ALeucineTheCatalogueLacks_StaysNullSoTheEngineEstimatesIt()
    {
        var food = Catalogued(Nutrients(leucine: null));

        var input = ScoringInput.From(food);

        Assert.Null(input.LeucinePer100g);
    }

    [Fact]
    public void From_ACategoryNoRuleKnows_ArrivesUnchangedAndNotAsNull()
    {
        var food = Catalogued(Nutrients(leucine: null), category: "Iaurt de casă");

        var input = ScoringInput.From(food);

        Assert.Equal("Iaurt de casă", input.Category);
    }

    private static Food Catalogued(NutrientAmounts nutrients, string category = "Cheese") => new()
    {
        FdcId = 2705385,
        Description = "Milk, whole",
        Category = category,
        Nutrients = nutrients
    };

    private static NutrientAmounts Nutrients(double? leucine) => new()
    {
        Calories = 61,
        Protein = 3.27,
        Fat = 3.2,
        Fiber = 0,
        SaturatedFat = 1.86,
        Sodium = 38,
        Leucine = leucine
    };
}
