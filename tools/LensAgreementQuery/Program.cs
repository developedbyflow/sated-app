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

var foods = await database.Foods.ToListAsync();
var lenses = shipped.Lenses.ToArray();

Console.WriteLine($"Catalog: {foods.Count} alimente · lentile: {string.Join(", ", lenses.Select(lens => lens.Id))}");
Console.WriteLine();
Console.WriteLine("Predicții scrise ÎNAINTE de rulare:");
Console.WriteLine("  P1  sub 60% din alimente au aceeași literă sub toate trei");
Console.WriteLine("  P2  cel puțin un aliment sare două litere între lentile");
Console.WriteLine("  P3  weight-loss și fitness se despart mai des decât weight-loss și glp-1");
Console.WriteLine();

var rows = foods
    .Select(food => new
    {
        food.Description,
        food.Category,
        Grades = lenses.ToDictionary(
            lens => lens.Id,
            lens =>
            {
                var input = ScoringInput.From(food);
                return shipped.GradeFor(combiner.Combine(input, lens), lens);
            })
    })
    .ToArray();

var lettered = rows.Where(row => row.Grades.Values.All(grade => grade is not null)).ToArray();
var unlettered = rows.Length - lettered.Length;

var agreeing = lettered.Count(row => row.Grades.Values.Distinct().Count() == 1);
var twoLetters = lettered.Count(row => row.Grades.Values.Distinct().Count() == 2);
var threeLetters = lettered.Count(row => row.Grades.Values.Distinct().Count() == 3);

Console.WriteLine($"fără literă sub cel puțin o lentilă: {unlettered}");
Console.WriteLine($"cu literă peste tot:                {lettered.Length}");
Console.WriteLine();
Console.WriteLine($"  aceeași literă sub toate trei   {agreeing,5}  ({Percent(agreeing)})");
Console.WriteLine($"  două litere distincte           {twoLetters,5}  ({Percent(twoLetters)})");
Console.WriteLine($"  trei litere distincte           {threeLetters,5}  ({Percent(threeLetters)})");

int Gap(Grade? a, Grade? b) => Math.Abs((int)a! - (int)b!);

var widest = lettered
    .Select(row => new
    {
        row.Description,
        row.Grades,
        Spread = lenses.SelectMany(_ => lenses, (left, right) => Gap(row.Grades[left.Id], row.Grades[right.Id])).Max()
    })
    .ToArray();

Console.WriteLine();
foreach (var group in widest.GroupBy(row => row.Spread).OrderBy(group => group.Key))
{
    Console.WriteLine($"  distanță maximă {group.Key} litere: {group.Count(),5}  ({Percent(group.Count())})");
}

Console.WriteLine();
Console.WriteLine("perechi care se despart:");
foreach (var left in lenses)
{
    foreach (var right in lenses.Where(candidate => string.CompareOrdinal(candidate.Id, left.Id) > 0))
    {
        var apart = lettered.Count(row => row.Grades[left.Id] != row.Grades[right.Id]);
        Console.WriteLine($"  {left.Id,-12} vs {right.Id,-12} {apart,5}  ({Percent(apart)})");
    }
}

Console.WriteLine();
Console.WriteLine($"{"aliment",-56}{string.Concat(lenses.Select(lens => $"{Short(lens.Id),12}"))}");
foreach (var row in widest.OrderByDescending(row => row.Spread).ThenBy(row => row.Description).Take(12))
{
    Console.WriteLine($"{Truncate(row.Description, 56),-56}{string.Concat(lenses.Select(lens => $"{row.Grades[lens.Id],12}"))}");
}

var watermelon = lettered.FirstOrDefault(row => row.Description.Contains("Watermelon", StringComparison.OrdinalIgnoreCase));
Console.WriteLine();
Console.WriteLine(watermelon is null
    ? "pepenele nu e în catalog sub numele ăsta"
    : $"pepene ({Truncate(watermelon.Description, 40)}): " +
      string.Join(" · ", lenses.Select(lens => $"{Short(lens.Id)} {watermelon.Grades[lens.Id]}")));

string Percent(int count) => $"{(double)count / Math.Max(1, lettered.Length) * 100:F1}%";

static string Short(string lensId) => lensId switch
{
    "weight-loss" => "weight",
    "protein-focus" => "protein",
    _ => lensId
};

static string Truncate(string text, int length) => text.Length <= length ? text : text[..length];
