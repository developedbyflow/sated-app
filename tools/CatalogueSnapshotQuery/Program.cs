using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sated.Scoring;

// The gate grades 68 foods. The catalogue holds 5,432, and until now nothing at all touched the
// other 5,364: every repair in sessions 9 through 11 was measured over the whole catalogue by
// hand, once, and then nothing held that measurement. A regression there is invisible — the gate
// stays green while letters move in the product.
//
// The obstacle is that the 63 MB FNDDS file cannot be committed, so no test can read it. What can
// be committed is the answer: the letter and score this engine gives every food in it. Checked in,
// that file turns "measured by hand" into a diff — the change to the engine and the letters it
// moved land in the same commit, and the next run says so on its own.
//
// The snapshot is the measurement, not a second source of truth: it is only ever regenerated from
// the engine, never edited. A moved letter is not a failure, it is a cost — the tool prints the
// cost and refuses to overwrite silently, which is the part nobody could do by hand.
//
//   dotnet run              compares against the committed snapshot, exits 1 if anything moved
//   dotnet run -- --write   regenerates it, after you have read what moved

const string DataPath = "../UsdaCoverageQuery/data/surveyDownload.json";
const string SnapshotPath = "catalogue-grades.csv";

var write = args.Contains("--write");

if (!File.Exists(DataPath))
{
    Console.WriteLine($"Lipsește {DataPath} — 63 MB, gitignored.");
    Console.WriteLine("Fără el nu există catalog de măsurat. Nu e o regresie, e o dată lipsă.");
    return 2;
}

var shipped = Calibration.Load();

var combiner = new ScoreCombiner(
    new GeneralStrategies(
        shipped.SatietyScale, shipped.DensityScales, shipped.ReferenceMealGrams),
    shipped.Rules);

var lenses = shipped.Lenses;

var foods = JsonSerializer.Deserialize<SurveyFoodsFile>(File.ReadAllText(DataPath))!.Foods;

// Sorted by id, so the file has one canonical order and a diff shows moved letters rather than
// moved lines. FNDDS enumeration order is not guaranteed to survive a re-download.
var current = new SortedDictionary<int, Row>();
var incomplete = 0;

foreach (var food in foods)
{
    var amounts = food.FoodNutrients
        .Where(entry => entry.Amount is not null)
        .ToDictionary(entry => entry.Nutrient.Number, entry => entry.Amount!.Value);

    if (!Codes.Required.All(amounts.ContainsKey))
    {
        incomplete++;
        continue;
    }

    var input = new FoodInput(
        Category: food.WweiaFoodCategory?.Description ?? "Not included in a food category",
        Calories: amounts["208"],
        Protein: amounts["203"],
        Fat: amounts["204"],
        Fiber: amounts["291"],
        VitaminA: amounts["320"],
        VitaminC: amounts["401"],
        VitaminE: amounts["323"],
        Calcium: amounts["301"],
        Iron: amounts["303"],
        Magnesium: amounts["304"],
        Potassium: amounts["306"],
        SaturatedFat: amounts["606"],
        Sodium: amounts["307"]);

    // Leucine stays null: FNDDS carries no amino acid data, so every food takes the estimate and
    // every grade here is a Partial Grade. That is the shipped catalogue, so it is what a
    // regression must be measured against — filling leucine in would snapshot a catalogue that
    // does not exist yet. When Epic 3 brings measured leucine, the diff is the cost of Epic 3.
    var grades = new string[lenses.Length];
    var scores = new double[lenses.Length];

    for (var index = 0; index < lenses.Length; index++)
    {
        var score = combiner.Combine(input, lenses[index]);

        grades[index] = shipped.GradeFor(score, lenses[index]).ToString();
        scores[index] = score.Value;
    }

    current[food.FdcId] = new Row(food.Description, grades, scores);
}

Console.WriteLine($"Calibrare: {shipped.Catalogue}, măsurată {shipped.MeasuredOn}");
Console.WriteLine($"Catalog: {foods.Count} alimente · punctate: {current.Count}" +
    (incomplete > 0 ? $" · {incomplete} fără nutrienții necesari" : string.Empty));
Console.WriteLine($"Lentile: {string.Join(", ", lenses.Select(lens => lens.Name))}");

var previous = File.Exists(SnapshotPath) ? ReadSnapshot(SnapshotPath) : null;

if (previous is null)
{
    WriteSnapshot();
    Console.WriteLine();
    Console.WriteLine($"Prima rulare: {SnapshotPath} scris cu {current.Count} alimente.");
    Console.WriteLine("De acum, orice literă care se mișcă în catalog apare ca diff.");
    return 0;
}

if (!previous.Lenses.SequenceEqual(lenses.Select(lens => lens.Name)))
{
    Section("Lentile");
    Console.WriteLine($"instantaneu: {string.Join(", ", previous.Lenses)}");
    Console.WriteLine($"calibrare:   {string.Join(", ", lenses.Select(lens => lens.Name))}");
    Console.WriteLine();
    Console.WriteLine("Setul de lentile s-a schimbat — coloanele nu se mai compară aliniat.");

    if (!write)
    {
        Console.WriteLine("Rulează cu --write; diff-ul din commit e costul.");
        return 1;
    }

    WriteSnapshot();
    Console.WriteLine($"{SnapshotPath} rescris cu {current.Count} alimente pe " +
        $"{lenses.Length} lentile. Diff-ul din commit e costul măsurat.");
    return 0;
}

var added = current.Keys.Except(previous.Rows.Keys).ToArray();
var removed = previous.Rows.Keys.Except(current.Keys).ToArray();
var shared = current.Keys.Intersect(previous.Rows.Keys).ToArray();

var letterMoves = new List<Move>();
var scoreOnly = new List<Move>();

foreach (var id in shared)
{
    var now = current[id];
    var then = previous.Rows[id];

    for (var index = 0; index < lenses.Length; index++)
    {
        var move = new Move(
            now.Description,
            lenses[index].Name,
            then.Grades[index],
            now.Grades[index],
            now.Scores[index] - then.Scores[index]);

        if (move.From != move.To)
        {
            letterMoves.Add(move);
        }
        // A tenth of a point is below what the file records, so anything smaller is rounding in
        // the snapshot rather than a change in the engine.
        else if (Math.Abs(move.Delta) >= 0.05)
        {
            scoreOnly.Add(move);
        }
    }
}

if (added.Length > 0 || removed.Length > 0)
{
    Section("Alimente intrate și ieșite");
    Console.WriteLine($"intrate: {added.Length} · ieșite: {removed.Length}");

    foreach (var id in added.Take(10))
    {
        Console.WriteLine($"  + {id}  {current[id].Description}");
    }

    foreach (var id in removed.Take(10))
    {
        Console.WriteLine($"  - {id}  {previous.Rows[id].Description}");
    }

    if (added.Length + removed.Length > 20)
    {
        Console.WriteLine($"  … {added.Length + removed.Length - 20} nelistate");
    }
}

Section("Litere mutate");

if (letterMoves.Count == 0)
{
    Console.WriteLine("niciuna · catalogul dă exact aceleași litere");
}
else
{
    // Grouped by which letter moved where, because that is the sentence a decision log wants:
    // "4 litere, toate greșite azi" reads off this table, a list of 733 names does not.
    foreach (var group in letterMoves
        .GroupBy(move => (move.Lens, move.From, move.To))
        .OrderByDescending(group => group.Count()))
    {
        var examples = group.Take(3).Select(move => Truncate(move.Description, 38));

        Console.WriteLine($"{group.Key.Lens,-13} {group.Key.From} → {group.Key.To}  " +
            $"{group.Count(),5}   {string.Join(" · ", examples)}");
    }

    var foodsMoved = letterMoves.Select(move => move.Description).Distinct().Count();

    Console.WriteLine();
    Console.WriteLine($"total: {letterMoves.Count} litere pe {foodsMoved} alimente " +
        $"({(double)foodsMoved / current.Count * 100:F1}% din catalog)");
}

Section("Scoruri mutate fără schimbare de literă");

if (scoreOnly.Count == 0)
{
    Console.WriteLine("niciunul");
}
else
{
    // The drift P55 leaves as written debt lives here: a change can move every score without
    // moving a letter, and then the next recalibration moves 733 of them at once.
    var largest = scoreOnly.MaxBy(move => Math.Abs(move.Delta))!;

    Console.WriteLine($"{scoreOnly.Count} punctaje · media |Δ| " +
        $"{scoreOnly.Average(move => Math.Abs(move.Delta)):F2} · maxim " +
        $"{Math.Abs(largest.Delta):F2} ({Truncate(largest.Description, 40)}, {largest.Lens})");
}

Section("Verdict");

var changed = letterMoves.Count + scoreOnly.Count + added.Length + removed.Length;

if (changed == 0)
{
    Console.WriteLine("NEATINS — catalogul dă exact ce dădea la ultimul instantaneu.");
    return 0;
}

if (write)
{
    WriteSnapshot();
    Console.WriteLine($"{SnapshotPath} rescris. Diff-ul din commit e costul măsurat.");
    return 0;
}

Console.WriteLine("MUTAT — costul de mai sus nu e încă scris nicăieri.");
Console.WriteLine("Citește-l, apoi `dotnet run -- --write` ca să intre în instantaneu.");
return 1;

void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"── {title} ".PadRight(100, '─'));
}

void WriteSnapshot()
{
    var file = new StringBuilder();

    file.AppendLine("# Fiecare aliment din FNDDS, cu litera și punctajul pe care i le dă motorul.");
    file.AppendLine("# Generat de tools/CatalogueSnapshotQuery — nu se editează de mână.");
    file.AppendLine($"# Calibrare: {shipped.Catalogue}, măsurată {shipped.MeasuredOn}");
    file.AppendLine("# Descrierea e ultima coloană și conține virgule: se citește ca rest de linie.");

    file.AppendLine(string.Join(',',
        ["fdcId", .. lenses.SelectMany(lens => new[] { $"{lens.Name} grade", $"{lens.Name} score" }),
         "description"]));

    foreach (var (id, row) in current)
    {
        var cells = new List<string> { id.ToString(CultureInfo.InvariantCulture) };

        for (var index = 0; index < lenses.Length; index++)
        {
            cells.Add(row.Grades[index]);
            cells.Add(row.Scores[index].ToString("F1", CultureInfo.InvariantCulture));
        }

        cells.Add(row.Description);
        file.AppendLine(string.Join(',', cells));
    }

    File.WriteAllText(SnapshotPath, file.ToString());
}

static Snapshot ReadSnapshot(string path)
{
    var lines = File.ReadAllLines(path).Where(line => !line.StartsWith('#')).ToArray();

    var header = lines[0].Split(',');

    // Two columns per lens, plus fdcId and description: the header names the lenses the snapshot
    // was taken under, so adding one is caught rather than read as a shifted column.
    var lensNames = Enumerable.Range(0, (header.Length - 2) / 2)
        .Select(index => header[1 + index * 2][..^" grade".Length])
        .ToArray();

    var rows = new Dictionary<int, Row>();

    foreach (var line in lines.Skip(1).Where(line => line.Length > 0))
    {
        var cells = line.Split(',');
        var grades = new string[lensNames.Length];
        var scores = new double[lensNames.Length];

        for (var index = 0; index < lensNames.Length; index++)
        {
            grades[index] = cells[1 + index * 2];
            scores[index] = double.Parse(cells[2 + index * 2], CultureInfo.InvariantCulture);
        }

        // Everything past the fixed columns is the description, commas and all.
        var description = string.Join(',', cells.Skip(1 + lensNames.Length * 2));

        rows[int.Parse(cells[0], CultureInfo.InvariantCulture)] =
            new Row(description, grades, scores);
    }

    return new Snapshot(lensNames, rows);
}

static string Truncate(string text, int length) =>
    text.Length <= length ? text : text[..length];

record Row(string Description, string[] Grades, double[] Scores);

record Snapshot(string[] Lenses, Dictionary<int, Row> Rows);

record Move(string Description, string Lens, string From, string To, double Delta);

public static class Codes
{
    public static readonly string[] Required =
        ["208", "203", "204", "291", "320", "401", "323", "301", "303", "304", "306", "606", "307"];
}

public record Nutrient([property: JsonPropertyName("number")] string Number);

public record FoodNutrient(
    [property: JsonPropertyName("nutrient")] Nutrient Nutrient,
    [property: JsonPropertyName("amount")] double? Amount
);

public record WweiaCategory(
    [property: JsonPropertyName("wweiaFoodCategoryDescription")] string Description
);

public record FoodItem(
    [property: JsonPropertyName("fdcId")] int FdcId,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("foodNutrients")] List<FoodNutrient> FoodNutrients,
    [property: JsonPropertyName("wweiaFoodCategory")] WweiaCategory? WweiaFoodCategory
);

public record SurveyFoodsFile([property: JsonPropertyName("SurveyFoods")] List<FoodItem> Foods);
