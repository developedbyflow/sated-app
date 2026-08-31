using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sated.Data;
using Sated.Data.Entities;

namespace Sated.Api.Tests;

public class FoodsDatabase : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=sated_test;Username=sated;Password=sated";

    private readonly WebApplicationFactory<Program> api =
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Sated"] = ConnectionString
                })));

    public HttpClient Client { get; }

    public FoodsDatabase() => Client = api.CreateClient();

    public async Task InitializeAsync()
    {
        using var scope = api.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SatedDbContext>();

        await database.Database.EnsureDeletedAsync();
        await database.Database.MigrateAsync();

        database.Foods.AddRange(Catalogue);
        await database.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await api.DisposeAsync();
    }

    private static Food[] Catalogue =>
    [
        Listed("Soy beverage, plain", "Plant-based milk"),
        Listed("Cheddar cheese", "Cheese"),
        WholeMilk(),
        Listed("Parmesan cheese", "Cheese"),
        Listed("Almond milk, unsweetened", "Plant-based milk"),
        Listed("Blue cheese", "Cheese"),
        Listed("Milk, nonfat", "Milk, nonfat"),
        Listed("Mozzarella cheese", "Cheese"),
        Listed("Cottage cheese, lowfat", "Cheese")
    ];

    private static Food WholeMilk() => new()
    {
        FdcId = 2705385,
        Description = "Milk, whole",
        Category = "Milk, whole",
        Source = FoodSource.UsdaFndds,
        TypicalGrams = 244,
        Servings =
        [
            new FoodServing { Description = "1 fl oz", Grams = 30.5, Sequence = 3 },
            new FoodServing { Description = "1 cup", Grams = 244, Sequence = 1 },
            new FoodServing { Description = "1 tbsp", Grams = 15.3, Sequence = 2 }
        ],
        Nutrients = new NutrientAmounts
        {
            Calories = 61,
            Protein = 3.27,
            Fat = 3.2,
            Fiber = 0,
            SaturatedFat = 1.86,
            Sodium = 38,
            VitaminA = 32,
            VitaminC = 0,
            VitaminD = 1.1,
            VitaminE = 0.05,
            Thiamine = 0.056,
            Calcium = 123,
            Iron = 0,
            Magnesium = 12,
            Potassium = 150
        }
    };

    private static Food Listed(string description, string category) => new()
    {
        Description = description,
        Category = category,
        Source = FoodSource.UsdaFndds,
        Nutrients = new NutrientAmounts
        {
            Calories = 0,
            Protein = 0,
            Fat = 0,
            Fiber = 0,
            SaturatedFat = 0,
            Sodium = 0
        }
    };
}
