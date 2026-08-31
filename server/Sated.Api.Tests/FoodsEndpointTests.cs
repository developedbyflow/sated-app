using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Sated.Api.Dtos;
using Sated.Scoring;

namespace Sated.Api.Tests;

[Collection("Database")]
public class FoodsEndpointTests(FoodsDatabase database) : IClassFixture<FoodsDatabase>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

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
    public async Task Get_ListRow_CarriesNothingButIdDescriptionAndCategory()
    {
        var body = await database.Client.GetStringAsync("/api/foods?pageSize=1");

        using var page = JsonDocument.Parse(body);
        var first = page.RootElement.GetProperty("items")[0];

        Assert.Equal(
            ["category", "description", "id"],
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

    private async Task<GradeResponseDto> Graded(int id, string lensId) =>
        (await database.Client
            .GetFromJsonAsync<GradeResponseDto>($"/api/foods/{id}/grade?lensId={lensId}", Json))!;

    private async Task<GradeResponseDto> Posted(GradeRequestDto request)
    {
        var response = await database.Client.PostAsJsonAsync("/api/grades", request);

        return (await response.Content.ReadFromJsonAsync<GradeResponseDto>(Json))!;
    }

    private async Task<FoodListResponseDto> List(string url) =>
        (await database.Client.GetFromJsonAsync<FoodListResponseDto>(url))!;

    private async Task<FoodDetailDto> Detail(int id) =>
        (await database.Client.GetFromJsonAsync<FoodDetailDto>($"/api/foods/{id}"))!;

    private async Task<int> IdOf(string description) =>
        (await List($"/api/foods?search={Uri.EscapeDataString(description)}")).Items.Single().Id;
}
