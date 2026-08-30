using Microsoft.EntityFrameworkCore;
using Sated.Data;
using Sated.Parsing;

var survey = args.Length > 0
    ? args[0]
    : "../UsdaCoverageQuery/data/surveyDownload.json";

var connection = args.Length > 1
    ? args[1]
    : "Host=localhost;Port=5432;Database=sated;Username=sated;Password=sated";

if (!File.Exists(survey))
{
    Console.Error.WriteLine($"Survey file not found: {Path.GetFullPath(survey)}");
    return 1;
}

Console.WriteLine($"Reading {Path.GetFullPath(survey)}");

using var json = File.OpenRead(survey);
var result = CatalogueImport.Read(json);

Console.WriteLine($"{result.Accepted.Count} foods accepted, {result.Rejected.Count} rejected.");

foreach (var group in result.Rejected.GroupBy(rejection => rejection.Reason).OrderBy(group => group.Key))
{
    Console.WriteLine($"  {group.Key}: {group.Count()}");
    foreach (var rejection in group.Take(3))
    {
        Console.WriteLine($"      {rejection.Description}");
    }

    if (group.Count() > 3)
    {
        Console.WriteLine($"      ... and {group.Count() - 3} more");
    }
}

var options = new DbContextOptionsBuilder<SatedDbContext>().UseNpgsql(connection).Options;
await using var context = new SatedDbContext(options);

var existing = await context.Foods.CountAsync();
Console.WriteLine($"Replacing {existing} rows in Foods.");

await context.Foods.ExecuteDeleteAsync();
context.Foods.AddRange(result.Accepted);
await context.SaveChangesAsync();

Console.WriteLine($"Foods now holds {await context.Foods.CountAsync()} rows.");
return 0;
