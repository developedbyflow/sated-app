using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sated.Data;
using Sated.Data.Entities;

namespace Sated.Api.Tests;

public class AccountsDatabase : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=sated_test;Username=sated;Password=sated";

    private readonly WebApplicationFactory<Program> api = Api("1000");

    private readonly WebApplicationFactory<Program> throttled = Api("3");

    public async Task InitializeAsync()
    {
        using var scope = api.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SatedDbContext>();

        await database.Database.EnsureDeletedAsync();
        await database.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await api.DisposeAsync();
        await throttled.DisposeAsync();
    }

    public HttpClient NewBrowser() => OverHttps(api);

    public HttpClient NewThrottledBrowser() => OverHttps(throttled);

    public static string UnusedEmail() => $"{Guid.NewGuid():N}@sated.test";

    public async Task<int> AddFood(
        string description,
        string? ownerId,
        double calories = 250,
        double protein = 17,
        double fat = 20,
        string? slug = null)
    {
        using var scope = api.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SatedDbContext>();

        var food = new Food
        {
            Description = description,
            Slug = slug,
            Category = "Cheese",
            Source = ownerId is null ? FoodSource.UsdaFndds : FoodSource.UserEntered,
            OwnerId = ownerId,
            Nutrients = new NutrientAmounts
            {
                Calories = calories,
                Protein = protein,
                Fat = fat,
                Fiber = 0,
                SaturatedFat = 12,
                Sodium = 900
            }
        };

        database.Foods.Add(food);
        await database.SaveChangesAsync();

        return food.Id;
    }

    public async Task<int> FoodsWithId(int id)
    {
        using var scope = api.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SatedDbContext>();

        return await database.Foods.IgnoreQueryFilters().CountAsync(food => food.Id == id);
    }

    public Task<int> Water() => AddFood("Water", ownerId: null, calories: 0, protein: 0, fat: 0);

    public async Task<int> IngredientsOf(int recipeId)
    {
        using var scope = api.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SatedDbContext>();

        return await database.Set<RecipeIngredient>()
            .IgnoreQueryFilters()
            .CountAsync(ingredient => ingredient.RecipeId == recipeId);
    }

    public async Task<int> AddFoodWithServing(string description, string serving, double grams)
    {
        var id = await AddFood(description, ownerId: null);

        using var scope = api.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SatedDbContext>();

        database.Set<FoodServing>().Add(new FoodServing
        {
            FoodId = id,
            Description = serving,
            Grams = grams,
            Sequence = 1
        });

        await database.SaveChangesAsync();

        return id;
    }

    public async Task<int> MealsWithId(int mealId)
    {
        using var scope = api.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SatedDbContext>();

        return await database.Set<Meal>().IgnoreQueryFilters().CountAsync(meal => meal.Id == mealId);
    }

    public async Task<int> ConsentsOf(string userId)
    {
        using var scope = api.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SatedDbContext>();

        return await database.Consents.CountAsync(consent => consent.UserId == userId);
    }

    private static WebApplicationFactory<Program> Api(string loginAttemptsPerMinute) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Sated"] = ConnectionString,
                    ["RateLimits:LoginPerMinute"] = loginAttemptsPerMinute
                }));

            builder.ConfigureServices(services =>
                services.AddSingleton<TimeProvider>(new ClockFinerThanPostgres()));
        });

    private static HttpClient OverHttps(WebApplicationFactory<Program> api) =>
        api.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
}
