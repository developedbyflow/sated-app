using Microsoft.EntityFrameworkCore;
using Sated.Data.Entities;

namespace Sated.Data;

public class SatedDbContext(DbContextOptions<SatedDbContext> options) : DbContext(options)
{
    public DbSet<Food> Foods => Set<Food>();
}