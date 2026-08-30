using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sated.Api.Dtos;

namespace Sated.Api.Tests;

public class FoodsEndpointTests(FoodsDatabase database) : IClassFixture<FoodsDatabase>
{
    [Fact]
    public async Task Get_NoFilters_OrdersEveryFoodByDescription()
    {
        var page = await List("/api/foods");

        Assert.Equal(
            [
                "Almond milk, unsweetened",
                "Butter, salted",
                "Cheddar cheese",
                "Chicken breast, roasted",
                "Milk chocolate",
                "Olive oil",
                "Skim milk",
                "Whole milk",
                "Yogurt, plain"
            ],
            page.Items.Select(food => food.Description));
    }

    [Fact]
    public async Task Get_Search_IgnoresLetterCaseAndMatchesInsideTheDescription()
    {
        var page = await List("/api/foods?search=milk");

        Assert.Equal(
            ["Almond milk, unsweetened", "Milk chocolate", "Skim milk", "Whole milk"],
            page.Items.Select(food => food.Description));
    }

    [Fact]
    public async Task Get_SearchShouted_FindsTheSameFoods()
    {
        var written = await List("/api/foods?search=milk");
        var shouted = await List("/api/foods?search=MILK");

        Assert.Equal(4, shouted.Total);
        Assert.Equal(written.Items, shouted.Items);
    }

    [Fact]
    public async Task Get_Category_MatchesExactlyIncludingLetterCase()
    {
        var written = await List("/api/foods?category=Milk%20and%20dairy");
        var lowercase = await List("/api/foods?category=milk%20and%20dairy");

        Assert.Equal(5, written.Total);
        Assert.Equal(0, lowercase.Total);
    }

    [Fact]
    public async Task Get_SearchAndCategory_NarrowTogether()
    {
        var page = await List("/api/foods?search=milk&category=Milk%20and%20dairy");

        Assert.Equal(
            ["Almond milk, unsweetened", "Skim milk", "Whole milk"],
            page.Items.Select(food => food.Description));
    }

    [Fact]
    public async Task Get_SecondPage_ContinuesWhereTheFirstEnded()
    {
        var page = await List("/api/foods?page=2&pageSize=3");

        Assert.Equal(
            ["Chicken breast, roasted", "Milk chocolate", "Olive oil"],
            page.Items.Select(food => food.Description));
    }

    [Fact]
    public async Task Get_Total_CountsEveryMatchNotJustThePage()
    {
        var page = await List("/api/foods?category=Milk%20and%20dairy&pageSize=2");

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

    private async Task<FoodListResponseDto> List(string url) =>
        (await database.Client.GetFromJsonAsync<FoodListResponseDto>(url))!;
}
