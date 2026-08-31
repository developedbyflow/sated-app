using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Sated.Data.Entities;

namespace Sated.Data;

public class SatedDbContext(
    DbContextOptions<SatedDbContext> options, ICurrentUser? currentUser = null)
    : IdentityDbContext<AppUser>(options)
{
    private string? AskedBy => currentUser?.Id;

    public DbSet<Food> Foods => Set<Food>();

    public DbSet<ConsentDocument> ConsentDocuments => Set<ConsentDocument>();

    public DbSet<Consent> Consents => Set<Consent>();

    public DbSet<Recipe> Recipes => Set<Recipe>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Food>(food =>
        {
            food.OwnsOne(entry => entry.Nutrients);
            food.Property(entry => entry.Source).HasConversion<string>();
            food.HasIndex(entry => entry.FdcId).IsUnique();
            food.HasIndex(entry => entry.OwnerId);

            food.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(entry => entry.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            food.HasMany(entry => entry.Servings)
                .WithOne(serving => serving.Food)
                .HasForeignKey(serving => serving.FoodId)
                .OnDelete(DeleteBehavior.Cascade);

            food.HasQueryFilter(entry => entry.OwnerId == null || entry.OwnerId == AskedBy);
        });

        modelBuilder.Entity<FoodServing>(serving =>
        {
            serving.ToTable("FoodServings");

            serving.HasQueryFilter(entry =>
                entry.Food.OwnerId == null || entry.Food.OwnerId == AskedBy);
        });

        modelBuilder.Entity<Recipe>(recipe =>
        {
            recipe.HasMany(entry => entry.Ingredients)
                .WithOne()
                .HasForeignKey(ingredient => ingredient.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            recipe.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(entry => entry.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            recipe.HasIndex(entry => entry.OwnerId);

            recipe.HasQueryFilter(entry => entry.OwnerId == AskedBy);
        });

        modelBuilder.Entity<RecipeIngredient>(ingredient =>
        {
            ingredient.ToTable("RecipeIngredients");

            ingredient.HasOne(entry => entry.Food)
                .WithMany()
                .HasForeignKey(entry => entry.FoodId)
                .OnDelete(DeleteBehavior.Cascade);

            ingredient.HasQueryFilter(entry =>
                entry.Food.OwnerId == null || entry.Food.OwnerId == AskedBy);
        });

        modelBuilder.Entity<ConsentDocument>(document =>
        {
            document.Property(published => published.Purpose).HasConversion<string>();
            document.HasIndex(published => new { published.Purpose, published.Version }).IsUnique();
            document.HasData(FirstHealthDataDocument);
        });

        modelBuilder.Entity<Consent>(consent =>
        {
            consent.HasOne(signed => signed.Document)
                .WithMany()
                .HasForeignKey(signed => signed.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            consent.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(signed => signed.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            consent.HasIndex(signed => signed.UserId);
        });
    }

    private static ConsentDocument FirstHealthDataDocument => new()
    {
        Id = 1,
        Purpose = ConsentPurpose.HealthData,
        Version = "2026-08-31",
        PublishedAt = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero),
        Text =
            """
            Sated needs two kinds of information about you that count as health data: your body
            weight, and what you eat.

            Your weight is used to work out your daily protein target. What you eat is used to
            grade your food and to show you your day. Neither is used for anything else, and
            neither is shared with anyone outside Sated.

            The law treats this as a special category of personal data. That is why you are being
            asked here, separately from the terms you accepted when you created your account.
            Nothing on this screen covers marketing, analytics, or passing anything to anyone else.
            Sated does none of those.

            You can withdraw this at any time from Settings, in one action — the same as giving it.

            Withdrawing deletes the data it covers: your weight, and everything you have logged.
            Your account stays and you can still sign in, but Sated has nothing left to work with,
            so grades and targets stop. That is not a penalty for withdrawing; it is what the
            product is made of.
            """
    };
}
