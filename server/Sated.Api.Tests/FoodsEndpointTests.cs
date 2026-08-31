using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Sated.Api.Dtos;
using Sated.Data.Entities;
using Sated.Scoring;

namespace Sated.Api.Tests;

[Collection("Database")]
public class FoodsEndpointTests(FoodsDatabase database) : IClassFixture<FoodsDatabase>
{
    [Fact]
    public async Task Get_NoFilters_OrdersEveryFoodByDescription()
    {
        var page = await List("/api/foods");

        Assert.Equal(
            [
                "Almond milk, unsweetened",
                "Blue cheese",
                "Cheddar cheese",
                "Cottage cheese, lowfat",
                "Milk, nonfat",
                "Milk, whole",
                "Mozzarella cheese",
                "Parmesan cheese",
                "Soy beverage, plain"
            ],
            page.Items.Select(food => food.Description));
    }

    [Fact]
    public async Task Get_Search_IgnoresLetterCaseAndMatchesInsideTheDescription()
    {
        var page = await List("/api/foods?search=milk");

        Assert.Equal(
            ["Almond milk, unsweetened", "Milk, nonfat", "Milk, whole"],
            page.Items.Select(food => food.Description));
    }

    [Fact]
    public async Task Get_SearchShouted_FindsTheSameFoods()
    {
        var written = await List("/api/foods?search=milk");
        var shouted = await List("/api/foods?search=MILK");

        Assert.Equal(3, shouted.Total);
        Assert.Equal(written.Items, shouted.Items);
    }

    [Fact]
    public async Task Get_Category_MatchesExactlyIncludingLetterCase()
    {
        var written = await List("/api/foods?category=Cheese");
        var lowercase = await List("/api/foods?category=cheese");

        Assert.Equal(5, written.Total);
        Assert.Equal(0, lowercase.Total);
    }

    [Fact]
    public async Task Get_SearchAndCategory_NarrowTogether()
    {
        var page = await List("/api/foods?search=milk&category=Plant-based%20milk");

        Assert.Equal(
            ["Almond milk, unsweetened"],
            page.Items.Select(food => food.Description));
    }

    [Fact]
    public async Task Get_SecondPage_ContinuesWhereTheFirstEnded()
    {
        var page = await List("/api/foods?page=2&pageSize=3");

        Assert.Equal(
            ["Cottage cheese, lowfat", "Milk, nonfat", "Milk, whole"],
            page.Items.Select(food => food.Description));
    }

    [Fact]
    public async Task Get_Total_CountsEveryMatchNotJustThePage()
    {
        var page = await List("/api/foods?category=Cheese&pageSize=2");

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(5, page.Total);
    }

    [Fact]
    public async Task Get_PageAfterTheLast_IsEmptyAndKeepsTheTotal()
    {
        var page = await List("/api/foods?page=4&pageSize=3");

        Assert.Empty(page.Items);
        Assert.Equal(9, page.Total);
    }

    [Fact]
    public async Task Get_PageSizeAboveTheCap_IsRejected()
    {
        var allowed = await database.Client.GetAsync("/api/foods?pageSize=100");
        var refused = await database.Client.GetAsync("/api/foods?pageSize=101");

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    [Fact]
    public async Task Get_PageBelowOne_IsRejected()
    {
        var response = await database.Client.GetAsync("/api/foods?page=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_ListRow_CarriesNoNutrientsAndSaysWhereTheFoodCameFrom()
    {
        var body = await database.Client.GetStringAsync("/api/foods?pageSize=1");

        using var page = JsonDocument.Parse(body);
        var first = page.RootElement.GetProperty("items")[0];

        Assert.Equal(
            ["category", "description", "id", "source"],
            first.EnumerateObject().Select(field => field.Name).Order());
    }

    [Fact]
    public async Task GetById_CarriesTheNutrientsTheListLeftOut()
    {
        var milk = await Detail(await IdOf("Milk, whole"));

        Assert.Equal(61, milk.Nutrients.Calories);
        Assert.Equal(3.27, milk.Nutrients.Protein);
        Assert.Equal(123, milk.Nutrients.Calcium);
        Assert.Null(milk.Nutrients.Leucine);
    }

    [Fact]
    public async Task GetById_AMissingNutrient_IsNullNotZero()
    {
        var cheese = await Detail(await IdOf("Blue cheese"));

        Assert.Equal(0, cheese.Nutrients.Calories);
        Assert.Null(cheese.Nutrients.Calcium);
    }

    [Fact]
    public async Task GetById_CarriesFdcIdOnlyForFoodsThatCameFromUsda()
    {
        var milk = await Detail(await IdOf("Milk, whole"));
        var cheese = await Detail(await IdOf("Blue cheese"));

        Assert.Equal(2705385, milk.FdcId);
        Assert.Null(cheese.FdcId);
    }

    [Fact]
    public async Task GetById_AgreesWithTheRowTheListShowed()
    {
        var listed = (await List("/api/foods?search=Mozzarella")).Items.Single();

        var detail = await Detail(listed.Id);

        Assert.Equal((listed.Id, listed.Description, listed.Category),
            (detail.Id, detail.Description, detail.Category));
    }

    [Fact]
    public async Task GetById_AnIdThatIsNotThere_Returns404()
    {
        var response = await database.Client.GetAsync("/api/foods/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AnIdThatIsNotANumber_Returns404()
    {
        var response = await database.Client.GetAsync("/api/foods/milk");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetGrade_WholeMilkUnderWeightLoss_IsB()
    {
        var graded = await Graded(await IdOf("Milk, whole"), "weight-loss");

        Assert.Equal(Grade.B, graded.Grade);
    }

    [Fact]
    public async Task GetGrade_TheSameFoodUnderTwoLenses_ScoresDifferently()
    {
        var id = await IdOf("Milk, whole");

        var underWeightLoss = await Graded(id, "weight-loss");
        var underFitness = await Graded(id, "fitness");

        Assert.NotEqual(underWeightLoss.Score, underFitness.Score);
    }

    [Fact]
    public async Task GetGrade_ProteinQuality_IsEstimatedBecauseTheCatalogueCarriesNoLeucine()
    {
        var graded = await Graded(await IdOf("Milk, whole"), "weight-loss");

        Assert.True(graded.ProteinQuality!.IsEstimated);
        Assert.False(graded.Satiety.IsEstimated);
    }

    [Fact]
    public async Task GetGrade_UnknownLens_RejectsTheLensIdField()
    {
        var response = await database.Client
            .GetAsync($"/api/foods/{await IdOf("Milk, whole")}/grade?lensId=keto");

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("lensId", problem!.Errors.Keys);
    }

    [Fact]
    public async Task GetGrade_NoLensAtAll_IsRejectedTheSameWay()
    {
        var response = await database.Client
            .GetAsync($"/api/foods/{await IdOf("Milk, whole")}/grade");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetGrade_AFoodThatIsNotThere_Returns404()
    {
        var response = await database.Client.GetAsync("/api/foods/999999/grade?lensId=fitness");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetGrade_MatchesPostGradesForTheSameNutrients()
    {
        var milk = await Detail(await IdOf("Milk, whole"));

        var fromTheCatalogue = await Graded(milk.Id, "weight-loss");
        var fromTheClient = await Posted(new GradeRequestDto
        {
            LensId = "weight-loss",
            Category = milk.Category,
            Calories = milk.Nutrients.Calories,
            Protein = milk.Nutrients.Protein,
            Fat = milk.Nutrients.Fat,
            Fiber = milk.Nutrients.Fiber,
            SaturatedFat = milk.Nutrients.SaturatedFat,
            Sodium = milk.Nutrients.Sodium,
            Carbohydrate = 4.8,
            VitaminA = milk.Nutrients.VitaminA,
            VitaminC = milk.Nutrients.VitaminC,
            VitaminD = milk.Nutrients.VitaminD,
            VitaminE = milk.Nutrients.VitaminE,
            Thiamine = milk.Nutrients.Thiamine,
            Calcium = milk.Nutrients.Calcium,
            Iron = milk.Nutrients.Iron,
            Magnesium = milk.Nutrients.Magnesium,
            Potassium = milk.Nutrients.Potassium
        });

        Assert.Equal(fromTheClient, fromTheCatalogue);
    }

    [Fact]
    public async Task GetGrades_WholeMilk_LettersEveryLensInTheOrderTheLensListUses()
    {
        var compared = await Compared(await IdOf("Milk, whole"));

        Assert.Equal(["weight-loss", "fitness", "glp-1"], compared.Select(entry => entry.LensId));
        Assert.Equal<Grade?>([Grade.B, Grade.C, Grade.B], compared.Select(entry => entry.Grade.Grade));
    }

    [Fact]
    public async Task GetGrades_EachEntry_MatchesTheSameFoodAskedForThatLensAlone()
    {
        var id = await IdOf("Milk, whole");

        var compared = await Compared(id);

        foreach (var entry in compared)
        {
            Assert.Equal(await Graded(id, entry.LensId), entry.Grade);
        }
    }

    [Fact]
    public async Task GetGrades_AFoodWithNothingInIt_StillNamesEveryLensWithNoLetter()
    {
        var compared = await Compared(await IdOf("Cheddar cheese"));

        Assert.Equal(3, compared.Count);
        Assert.All(compared, entry => Assert.Null(entry.Grade.Grade));
    }

    [Fact]
    public async Task GetGrades_AFoodThatIsNotThere_Returns404()
    {
        var response = await database.Client.GetAsync("/api/foods/999999/grades");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<GradeResponseDto> Graded(int id, string lensId) =>
        (await database.Client
            .GetFromJsonAsync<GradeResponseDto>($"/api/foods/{id}/grade?lensId={lensId}", ApiJson.Options))!;

    private async Task<IReadOnlyList<LensGradeResponseDto>> Compared(int id) =>
        (await database.Client
            .GetFromJsonAsync<IReadOnlyList<LensGradeResponseDto>>($"/api/foods/{id}/grades", ApiJson.Options))!;

    private async Task<GradeResponseDto> Posted(GradeRequestDto request)
    {
        var response = await database.Client.PostAsJsonAsync("/api/grades", request);

        return (await response.Content.ReadFromJsonAsync<GradeResponseDto>(ApiJson.Options))!;
    }

    private async Task<FoodListResponseDto> List(string url) =>
        (await database.Client.GetFromJsonAsync<FoodListResponseDto>(url, ApiJson.Options))!;

    [Fact]
    public async Task Detail_ACatalogueFood_SaysItCameFromUsda()
    {
        var milk = await Detail(await IdOf("Milk, whole"));

        Assert.Equal(FoodSource.UsdaFndds, milk.Provenance.Source);
    }

    [Fact]
    public async Task Detail_ACatalogueFood_NamesLeucineAsEstimatedAndNothingAsAbsent()
    {
        var milk = await Detail(await IdOf("Milk, whole"));

        Assert.Equal(["leucine"], milk.Provenance.Estimated);
        Assert.Empty(milk.Provenance.Absent);
    }

    [Fact]
    public async Task Detail_ACatalogueFoodTheImportLeftThin_NamesEveryNutrientItDoesNotHave()
    {
        var thin = await Detail(await IdOf("Blue cheese"));

        Assert.Contains("magnesium", thin.Provenance.Absent);
        Assert.DoesNotContain("leucine", thin.Provenance.Absent);
    }

    [Fact]
    public async Task Detail_AFoodWithServings_ListsThemBySequenceNotByArrayOrder()
    {
        var milk = await Detail(await IdOf("Milk, whole"));

        Assert.Equal(
            ["1 cup", "1 tbsp", "1 fl oz"],
            milk.Servings.Select(serving => serving.Description));
    }

    [Fact]
    public async Task Detail_AFoodWithServings_CarriesTheAmountUsdaAssumesWhenNobodySaid()
    {
        var milk = await Detail(await IdOf("Milk, whole"));

        Assert.Equal(244, milk.TypicalGrams);
    }

    [Fact]
    public async Task Detail_AFoodTheImportGaveNoServings_ListsNoneRatherThanGuessing()
    {
        var cheese = await Detail(await IdOf("Blue cheese"));

        Assert.Empty(cheese.Servings);
        Assert.Null(cheese.TypicalGrams);
    }

    private async Task<FoodDetailDto> Detail(int id) =>
        (await database.Client.GetFromJsonAsync<FoodDetailDto>($"/api/foods/{id}", ApiJson.Options))!;

    private async Task<int> IdOf(string description) =>
        (await List($"/api/foods?search={Uri.EscapeDataString(description)}")).Items.Single().Id;
}
