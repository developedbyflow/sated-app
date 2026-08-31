using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Sated.Data;
using Sated.Scoring;
using Sated.Services;

var connection = args.Length > 0
    ? args[0]
    : "Host=localhost;Port=5432;Database=sated;Username=sated;Password=sated";

var shipped = Calibration.Load();
var combiner = new ScoreCombiner(
    new GeneralStrategies(shipped.SatietyScale, shipped.DensityScales, shipped.ReferenceMealGrams),
    shipped.Rules);

var options = new DbContextOptionsBuilder<SatedDbContext>().UseNpgsql(connection).Options;
await using var database = new SatedDbContext(options);

var reading = Stopwatch.StartNew();
var foods = await database.Foods.ToListAsync();
reading.Stop();

Console.WriteLine($"Catalog: {foods.Count} alimente · citit în {reading.ElapsedMilliseconds} ms");
Console.WriteLine();
Console.WriteLine("Predicții scrise ÎNAINTE de rulare:");
Console.WriteLine("  P1  cea mai mare categorie trece de 100 de alimente");
Console.WriteLine("  P2  punctarea unei categorii întregi stă sub 50 ms");
Console.WriteLine("  P3  peste 20% dintre alimente n-au NICIO alternativă mai bună în categoria lor");
Console.WriteLine("  P4  sub 5% dintre alimente au nota parțială");
Console.WriteLine();

var lens = shipped.Lenses.First(candidate => candidate.Id == "weight-loss");

var scoring = Stopwatch.StartNew();
var graded = foods
    .Select(food =>
    {
        var score = combiner.Combine(ScoringInput.From(food), lens);
        return new Candidate(food.Id, food.Description, food.Category,
            shipped.GradeFor(score, lens), score.Value, score.IsPartial);
    })
    .ToArray();
scoring.Stop();

Console.WriteLine($"punctate toate cele {graded.Length} sub weight-loss: {scoring.Elapsed.TotalMilliseconds:F0} ms " +
    $"({scoring.Elapsed.TotalMilliseconds / graded.Length:F3} ms per aliment)");

Section("Mărimea categoriilor");

var categories = graded.GroupBy(food => food.Category).ToArray();
var sizes = categories.Select(group => group.Count()).Order().ToArray();

Console.WriteLine($"categorii: {categories.Length}");
Console.WriteLine($"mediană: {sizes[sizes.Length / 2]} · medie: {sizes.Average():F1} · maxim: {sizes[^1]}");
Console.WriteLine();
foreach (var group in categories.OrderByDescending(group => group.Count()).Take(5))
{
    Console.WriteLine($"  {Truncate(group.Key, 56),-56}{group.Count(),5}");
}

Section("Nota parțială");

var partial = graded.Count(food => food.IsPartial);
var letterless = graded.Count(food => food.Grade is null);

Console.WriteLine($"parțiale (IsPartial):        {partial,5}  ({Percent(partial, graded.Length)})");
Console.WriteLine($"fără literă (Grade null):    {letterless,5}  ({Percent(letterless, graded.Length)})");
Console.WriteLine($"parțiale ȘI cu literă:       {graded.Count(food => food.IsPartial && food.Grade is not null),5}");

Section("Câte alternative mai bune există");

var byCategory = graded.ToLookup(food => food.Category);

var counts = graded
    .Where(food => food.Grade is not null)
    .Select(food => new
    {
        Food = food,
        Better = byCategory[food.Category]
            .Where(other => other.Id != food.Id && !other.IsPartial && other.Grade < food.Grade)
            .ToArray()
    })
    .ToArray();

foreach (var bucket in new[] { 0, 1, 2 })
{
    var many = counts.Count(row => row.Better.Length == bucket);
    Console.WriteLine($"  {bucket} alternative mai bune  {many,5}  ({Percent(many, counts.Length)})");
}

var full = counts.Count(row => row.Better.Length >= 3);
Console.WriteLine($"  3 sau mai multe        {full,5}  ({Percent(full, counts.Length)})");

Section("Literă mai bună SAU scor mai bun — Grade.cs spune că litera nu compară");

var byScore = graded
    .Where(food => food.Grade is not null)
    .Select(food => new
    {
        Food = food,
        Better = byCategory[food.Category]
            .Where(other => other.Id != food.Id && !other.IsPartial && other.Grade is not null
                && other.Score > food.Score)
            .ToArray()
    })
    .ToArray();

var noneByLetter = counts.Count(row => row.Better.Length == 0);
var noneByScore = byScore.Count(row => row.Better.Length == 0);

Console.WriteLine($"{"regula",-26}{"zero alternative",18}{"trei sau mai multe",22}");
Console.WriteLine($"{"literă strict mai bună",-26}{noneByLetter,10} ({Percent(noneByLetter, counts.Length)}){counts.Count(row => row.Better.Length >= 3),14} ({Percent(counts.Count(row => row.Better.Length >= 3), counts.Length)})");
Console.WriteLine($"{"scor strict mai mare",-26}{noneByScore,10} ({Percent(noneByScore, byScore.Length)}){byScore.Count(row => row.Better.Length >= 3),14} ({Percent(byScore.Count(row => row.Better.Length >= 3), byScore.Length)})");

var sameLetterBetterScore = byScore
    .Where(row => row.Better.Length > 0)
    .Sum(row => row.Better.OrderByDescending(other => other.Score).Take(3)
        .Count(other => other.Grade == row.Food.Grade));

var topThreeByScore = byScore.Sum(row => Math.Min(3, row.Better.Length));

Console.WriteLine();
Console.WriteLine($"din primele 3 alternative alese după scor, câte poartă ACEEAȘI literă ca alimentul de plecare:");
Console.WriteLine($"  {sameLetterBetterScore} din {topThreeByScore}  ({Percent(sameLetterBetterScore, topThreeByScore)})");

var butter = graded.FirstOrDefault(food => food.Description.StartsWith("Butter, stick", StringComparison.OrdinalIgnoreCase))
    ?? graded.FirstOrDefault(food => food.Description.StartsWith("Butter", StringComparison.OrdinalIgnoreCase));

if (butter is not null)
{
    Console.WriteLine();
    Console.WriteLine($"exemplul din Grade.cs — {Truncate(butter.Description, 46)} ({butter.Grade} {butter.Score:F1}), categoria {butter.Category}:");

    foreach (var other in byCategory[butter.Category]
        .Where(other => other.Id != butter.Id && !other.IsPartial && other.Grade is not null && other.Score > butter.Score)
        .OrderByDescending(other => other.Score)
        .Take(5))
    {
        Console.WriteLine($"    {Truncate(other.Description, 52),-52}{other.Grade,3}{other.Score,8:F1}");
    }
}

Section("Cât costă o cerere de swap, naiv");

var largest = categories.OrderByDescending(group => group.Count()).First();
var sample = foods.Where(food => food.Category == largest.Key).ToList();

var perRequest = Stopwatch.StartNew();
for (var run = 0; run < 100; run++)
{
    _ = sample
        .Select(food => combiner.Combine(ScoringInput.From(food), lens))
        .ToArray();
}
perRequest.Stop();

Console.WriteLine($"cea mai mare categorie: {largest.Key} · {sample.Count} alimente");
Console.WriteLine($"punctată de 100 de ori: {perRequest.Elapsed.TotalMilliseconds:F0} ms " +
    $"→ {perRequest.Elapsed.TotalMilliseconds / 100:F2} ms per cerere");

Section("Egalități la vârf — contează ordinea de departajare?");

var ties = counts
    .Where(row => row.Better.Length > 3)
    .Count(row =>
    {
        var ranked = row.Better.OrderByDescending(other => other.Score).ToArray();
        return Math.Abs(ranked[2].Score - ranked[3].Score) < 0.000001;
    });

Console.WriteLine($"alimente cu peste 3 candidați: {counts.Count(row => row.Better.Length > 3)}");
Console.WriteLine($"dintre ele, locul 3 și locul 4 la scor egal: {ties}");
Console.WriteLine(ties == 0
    ? "departajarea după scor e unică pe datele astea — a doua cheie NU e exercitată"
    : "departajarea după scor NU ajunge: a doua cheie chiar decide");

static string Percent(int count, int total) => $"{(double)count / Math.Max(1, total) * 100:F1}%";

static string Truncate(string text, int length) => text.Length <= length ? text : text[..length];

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"── {title} ".PadRight(94, '─'));
}

record Candidate(int Id, string Description, string Category, Grade? Grade, double Score, bool IsPartial);
