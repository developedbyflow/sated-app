using Microsoft.EntityFrameworkCore;
using Sated.Data.Entities;

namespace Sated.Data;

public class SatedDbContext(DbContextOptions<SatedDbContext> options) : DbContext(options)
{
    public DbSet<Food> Foods => Set<Food>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Food>().OwnsOne(food => food.Nutrients);
        modelBuilder.Entity<Food>().HasIndex(food => food.FdcId).IsUnique();
    }
}
