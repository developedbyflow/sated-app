using System.Text.Json;
using System.Text.Json.Serialization;
using Sated.Scoring;

// FR-21 describes the Day Grade twice, and the two halves do not agree.
//
//   "medie ponderată (după cantitate) a Grade-urilor Meal-urilor zilei"  -> average the meals
//   "prin aceeași metodă ca FR-8"                                        -> aggregate the plate
//
// FR-8's method is precisely the one that is NOT an average of its parts' grades: it sums the
// nutrients by quantity and reruns the formula on the total. Applied to a day, that means the day
// is one large plate. The other reading scores each meal, then averages those scores by meal
// weight. They are different numbers, and this measures by how much before either is built.
//
// Two questions, and the second is the one that decides it:
//   1. How often do the two methods hand a day a different letter?
//   2. How often would averaging MISS the density floor? The floor reads a CombinedScore's
//      density component. Averaging meal scores throws the components away, so a day that should
//      be floored to E cannot be — the rule has nothing left to read.
//   3. What does aggregating cost when one food is hand-typed? Catalogue foods carry every
//      micronutrient (measured against the live database: 0 of 1,933 miss one), so a missing one
//      is only reachable through POST /api/foods, where they are optional. The flag to watch is
//      density's IsEstimated, NOT IsPartial: partial means a whole component is absent, while a
//      missing micronutrient renormalises the one it belongs to. Under the plate reading, one
//      such food marks the WHOLE day estimated. That is the price of this decision, measured
//      here rather than discovered later.

const int Days = 20_000;
const int Seed = 20260831;

var shipped = Calibration.Load();
var combiner = shipped.Engine();
var lens = shipped.Lenses.Single(candidate => candidate.Id == "weight-loss");

var foods = JsonSerializer.Deserialize<SurveyFoodsFile>(
    File.ReadAllText("../UsdaCoverageQuery/data/surveyDownload.json"))!.Foods;

var catalogue = new List<FoodInput>();

foreach (var food in foods)
{
    var amounts = food.FoodNutrients
        .Where(entry => entry.Amount is not null)
        .ToDictionary(entry => entry.Nutrient.Number, entry => entry.Amount!.Value);

    if (!Codes.Required.All(amounts.ContainsKey) || amounts["208"] <= 0)
    {
        continue;
    }

    catalogue.Add(new FoodInput(
        Category: food.WweiaFoodCategory?.Description ?? "Not included in a food category",
        Calories: amounts["208"], Protein: amounts["203"], Fat: amounts["204"],
        Fiber: amounts["291"], VitaminA: amounts["320"], VitaminC: amounts["401"],
        VitaminE: amounts["323"], Calcium: amounts["301"], Iron: amounts["303"],
        Magnesium: amounts["304"], Potassium: amounts["306"], SaturatedFat: amounts["606"],
        Sodium: amounts["307"], VitaminD: amounts["328"], Thiamine: amounts["404"]));
}

Console.WriteLine($"Catalog: {catalogue.Count} alimente cu toți nutrienții.");
Console.WriteLine($"Zile simulate: {Days:N0} · sămânță {Seed} · lentila {lens.Name}");
Console.WriteLine();

var random = new Random(Seed);
var differing = 0;
var byDistance = new Dictionary<int, int>();
var flooredAway = 0;
var emptyDays = 0;
var scoreGaps = new List<double>();
var typedDayIsPartial = 0;
var typedMealsStillMeasured = 0;
var typedMealsTotal = 0;

for (var day = 0; day < Days; day++)
{
    var meals = new List<List<Portion>>();

    foreach (var _ in Enumerable.Range(0, random.Next(2, 5)))
    {
        var portions = Enumerable
            .Range(0, random.Next(1, 5))
            .Select(_ => new Portion(
                catalogue[random.Next(catalogue.Count)], random.Next(40, 351)))
            .ToList();

        meals.Add(portions);
    }

    var everyPortion = meals.SelectMany(meal => meal).ToList();

    var plate = combiner.Combine(PortionAggregate.Aggregate(everyPortion), lens);
    var asOnePlate = shipped.GradeFor(plate, lens);

    var mealScores = meals
        .Select(meal => (
            Score: combiner.Combine(PortionAggregate.Aggregate(meal), lens),
            Grams: meal.Sum(portion => portion.Grams)))
        .ToArray();

    var totalGrams = mealScores.Sum(meal => meal.Grams);
    var averaged = mealScores.Sum(meal => meal.Score.Value * meal.Grams) / totalGrams;
    Grade? asAverage = shipped.ThresholdsFor(lens).GradeForScoreAlone(averaged);

    scoreGaps.Add(Math.Abs(plate.Value - averaged));

    if (asOnePlate is Grade.E
        && shipped.ThresholdsFor(lens).GradeForScoreAlone(plate.Value) is not Grade.E)
    {
        flooredAway++;
    }

    if (plate.IsNutritionallyEmpty)
    {
        emptyDays++;
    }

    var handTyped = meals[random.Next(meals.Count)];
    var typedPortion = handTyped[random.Next(handTyped.Count)];
    var stripped = typedPortion.Food with { VitaminC = null, Magnesium = null };
    var typedMeals = meals
        .Select(meal => meal.Select(portion =>
            ReferenceEquals(portion, typedPortion)
                ? portion with { Food = stripped }
                : portion).ToList())
        .ToList();

    if (combiner.Combine(
            PortionAggregate.Aggregate([.. typedMeals.SelectMany(meal => meal)]), lens)
        .Density is { IsEstimated: true })
    {
        typedDayIsPartial++;
    }

    typedMealsTotal += typedMeals.Count;
    typedMealsStillMeasured += typedMeals.Count(meal =>
        combiner.Combine(PortionAggregate.Aggregate(meal), lens)
            .Density is { IsEstimated: false });

    if (asOnePlate != asAverage)
    {
        differing++;

        var distance = asOnePlate is null || asAverage is null
            ? 99
            : Math.Abs((int)asOnePlate.Value - (int)asAverage.Value);

        byDistance[distance] = byDistance.GetValueOrDefault(distance) + 1;
    }
}

Section("1 — cât de des diferă litera");
Console.WriteLine($"Zile cu literă diferită: {differing:N0} din {Days:N0}  ({(double)differing / Days:P2})");

foreach (var (distance, count) in byDistance.OrderBy(entry => entry.Key))
{
    var label = distance == 99 ? "una are literă, alta nu" : $"la {distance} litere distanță";
    Console.WriteLine($"  {label,-28} {count,7:N0}  ({(double)count / Days:P2})");
}

Console.WriteLine();
Console.WriteLine($"Diferența de scor, mediană: {Median(scoreGaps):F2} puncte · p95: {Percentile(scoreGaps, 95):F2} · maxim: {scoreGaps.Max():F2}");

Section("2 — ce pierde media: podeaua de densitate");
Console.WriteLine($"Zile coborâte la E de podea    : {flooredAway:N0}  ({(double)flooredAway / Days:P2})");
Console.WriteLine($"Zile fără nicio literă (goale) : {emptyDays:N0}  ({(double)emptyDays / Days:P2})");
Console.WriteLine("Media scorurilor nu poate aplica niciuna: componentele nu mai există după mediere.");

Section("3 — prețul agregării: un aliment tastat de user, fără vitamina C și magneziu");
Console.WriteLine($"Densitatea zilei devine estimată : {typedDayIsPartial:N0}  ({(double)typedDayIsPartial / Days:P2})");
Console.WriteLine($"Mese cu densitate încă măsurată  : {typedMealsStillMeasured:N0} din {typedMealsTotal:N0}  ({(double)typedMealsStillMeasured / typedMealsTotal:P2})");
Console.WriteLine("Sub media pe mese, doar masa atinsă ar fi marcată. Sub farfurie, toată ziua e.");

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"── {title} ".PadRight(100, '─'));
}

static double Median(List<double> values) => Percentile(values, 50);

static double Percentile(List<double> values, int percentile)
{
    var sorted = values.Order().ToArray();

    return sorted[Math.Clamp((int)Math.Round(percentile / 100.0 * (sorted.Length - 1)), 0, sorted.Length - 1)];
}

public static class Codes
{
    public static readonly string[] Required =
        ["208","203","204","291","320","401","323","301","303","304","306","606","307","328","404"];
}
public record Nutrient([property: JsonPropertyName("number")] string Number);
public record FoodNutrient([property: JsonPropertyName("nutrient")] Nutrient Nutrient,
    [property: JsonPropertyName("amount")] double? Amount);
public record WweiaCategory([property: JsonPropertyName("wweiaFoodCategoryDescription")] string Description);
public record FoodItem(
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("foodNutrients")] FoodNutrient[] FoodNutrients,
    [property: JsonPropertyName("wweiaFoodCategory")] WweiaCategory? WweiaFoodCategory);
public record SurveyFoodsFile(
    [property: JsonPropertyName("SurveyFoods")] FoodItem[] Foods);
