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
        Listed("Yogurt, plain", "Milk and dairy"),
        Listed("Milk chocolate", "Sweets"),
        Listed("Butter, salted", "Fats and oils"),
        Listed("Whole milk", "Milk and dairy"),
        Listed("Chicken breast, roasted", "Poultry"),
        Listed("Almond milk, unsweetened", "Milk and dairy"),
        Listed("Olive oil", "Fats and oils"),
        Listed("Cheddar cheese", "Milk and dairy"),
        Listed("Skim milk", "Milk and dairy")
    ];

    private static Food Listed(string description, string category) => new()
    {
        Description = description,
        Category = category,
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
