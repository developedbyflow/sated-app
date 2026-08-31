using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sated.Data;
using Sated.Data.Entities;

namespace Sated.Api.Tests;

public class SwapsDatabase : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=sated_test;Username=sated;Password=sated";

    public const string Category = "Peaches and nectarines";

    private readonly WebApplicationFactory<Program> api =
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Sated"] = ConnectionString
                })));

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Client = NewBrowser();

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

    public HttpClient NewBrowser() => api.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost")
    });

    public static string UnusedEmail() => $"{Guid.NewGuid():N}@sated.test";

    public async Task<int> IdOf(string description)
    {
        using var scope = api.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SatedDbContext>();

        return await database.Foods
            .Where(food => food.Description == description)
            .Select(food => food.Id)
            .FirstAsync();
    }

    private static Food[] Catalogue =>
    [
        Fruit("Nectarine, raw", calories: 43, protein: 1.06, fat: 0.28, fiber: 1.5,
            saturatedFat: 0, sodium: 13, vitaminA: 21, vitaminC: 2.9, vitaminD: 0,
            vitaminE: 0.61, thiamine: 0.034, calcium: 2, iron: 0.3, magnesium: 9, potassium: 131),
        Fruit("Peach, raw", calories: 46, protein: 0.91, fat: 0.27, fiber: 1.5,
            saturatedFat: 0.019, sodium: 13, vitaminA: 24, vitaminC: 4.1, vitaminD: 0,
            vitaminE: 0.73, thiamine: 0.024, calcium: 4, iron: 0.34, magnesium: 8, potassium: 122),
        Fruit("Peach, canned, NFS", calories: 48, protein: 0.43, fat: 0.09, fiber: 1.2,
            saturatedFat: 0.009, sodium: 4, vitaminA: 24, vitaminC: 2.7, vitaminD: 0,
            vitaminE: 0.44, thiamine: 0.008, calcium: 3, iron: 0.34, magnesium: 6, potassium: 116),
        Fruit("Peach, canned, in syrup", calories: 54, protein: 0.4, fat: 0.08, fiber: 1.2,
            saturatedFat: 0.006, sodium: 3, vitaminA: 25, vitaminC: 2.7, vitaminD: 0,
            vitaminE: 0.45, thiamine: 0.008, calcium: 2, iron: 0.3, magnesium: 5, potassium: 91),
        Fruit("Peach, canned, juice pack", calories: 41, protein: 0.45, fat: 0.1, fiber: 1.2,
            saturatedFat: 0.012, sodium: 6, vitaminA: 24, vitaminC: 2.8, vitaminD: 0,
            vitaminE: 0.44, thiamine: 0.009, calcium: 4, iron: 0.39, magnesium: 6, potassium: 141),
        Fruit("Peach, frozen", calories: 46, protein: 0.91, fat: 0.27, fiber: 1.5,
            saturatedFat: 0.019, sodium: 13, vitaminA: 24, vitaminC: 4.1, vitaminD: 0,
            vitaminE: 0.73, thiamine: 0.024, calcium: 4, iron: 0.34, magnesium: 8, potassium: 122),
        Fruit(NoLetter, calories: 0, protein: 0, fat: 0, fiber: 0,
            saturatedFat: 0, sodium: 0, vitaminA: 0, vitaminC: 0, vitaminD: 0,
            vitaminE: 0, thiamine: 0, calcium: 0, iron: 0, magnesium: 0, potassium: 0),
        Fruit(PartialRow, calories: 0, protein: 5, fat: 0, fiber: 3,
            saturatedFat: 0, sodium: 0, vitaminA: 900, vitaminC: 90, vitaminD: 20,
            vitaminE: 15, thiamine: 1.2, calcium: 1300, iron: 18, magnesium: 420, potassium: 4700)
    ];

    public const string PartialRow = "Peach, a row no request can create";

    public const string NoLetter = "Peach water, unsweetened";

    private static Food Fruit(
        string description, double calories, double protein, double fat, double fiber,
        double saturatedFat, double sodium, double vitaminA, double vitaminC, double vitaminD,
        double vitaminE, double thiamine, double calcium, double iron, double magnesium,
        double potassium) => new()
        {
            Description = description,
            Category = Category,
            Source = FoodSource.UsdaFndds,
            Nutrients = new NutrientAmounts
            {
                Calories = calories,
                Protein = protein,
                Fat = fat,
                Fiber = fiber,
                SaturatedFat = saturatedFat,
                Sodium = sodium,
                VitaminA = vitaminA,
                VitaminC = vitaminC,
                VitaminD = vitaminD,
                VitaminE = vitaminE,
                Thiamine = thiamine,
                Calcium = calcium,
                Iron = iron,
                Magnesium = magnesium,
                Potassium = potassium
            }
        };
}
