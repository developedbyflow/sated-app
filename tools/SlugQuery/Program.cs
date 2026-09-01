using Microsoft.EntityFrameworkCore;
using Sated.Data;
using Sated.Parsing;

var connection = args.Length > 0
    ? args[0]
    : "Host=localhost;Port=5432;Database=sated;Username=sated;Password=sated";

var options = new DbContextOptionsBuilder<SatedDbContext>().UseNpgsql(connection).Options;
await using var database = new SatedDbContext(options);

var foods = await database.Foods
    .IgnoreQueryFilters()
    .Where(food => food.OwnerId == null)
    .Select(food => new { food.Id, food.Description, food.Slug })
    .ToListAsync();

Console.WriteLine($"Catalog: {foods.Count} alimente");

Section("Se ciocnesc slug-urile scoase din descriere?");

var slugs = foods.Select(food => Slug.From(food.Description)).ToArray();
var distinct = slugs.Distinct().Count();

Console.WriteLine($"slug-uri distincte: {distinct} din {slugs.Length} → {slugs.Length - distinct} ciocniri");

foreach (var group in slugs.GroupBy(slug => slug).Where(group => group.Count() > 1).Take(5))
{
    Console.WriteLine($"  {group.Key} × {group.Count()}");
}

Section("Ce s-ar întâmpla dacă am tăia slug-ul la o lungime maximă");

Console.WriteLine($"cel mai lung slug: {slugs.Max(slug => slug.Length)} caractere · " +
    $"mediu: {slugs.Average(slug => slug.Length):F0}");

foreach (var cap in new[] { 40, 60, 80, 100 })
{
    var cut = slugs.Select(slug => slug.Length <= cap ? slug : slug[..cap]).ToArray();

    Console.WriteLine($"  tăiat la {cap,3}: {cut.Length - cut.Distinct().Count(),3} ciocniri");
}

Section("Slug-ul din baza de date e cel pe care îl scrie C#-ul?");

var stored = foods.Where(food => food.Slug is not null).ToArray();
var wrong = stored.Where(food => food.Slug != Slug.From(food.Description)).ToArray();

Console.WriteLine($"rânduri cu slug: {stored.Length} din {foods.Count}");
Console.WriteLine($"rânduri pe care migrarea și C#-ul le scriu diferit: {wrong.Length}");

foreach (var food in wrong.Take(5))
{
    Console.WriteLine($"  {food.Id}  {food.Description}");
    Console.WriteLine($"      în bază: {food.Slug}");
    Console.WriteLine($"      din C#:  {Slug.From(food.Description)}");
}

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"── {title} ".PadRight(94, '─'));
}
