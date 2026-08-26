using System.Globalization;
using System.Text;

namespace Sated.Scoring.Tests;

/// <summary>
/// Relations that must hold for every food, checked against the 399 in catalogue-sample.csv
/// rather than against foods somebody chose. The tests elsewhere say what one food scores; these
/// say what can never happen, and that is the difference that matters — every defect this engine
/// has shipped passed a full suite of the first kind on the day it was written.
/// </summary>
public class EnginePropertyTests
{
    private static readonly Calibration Shipped = Calibration.Load();

    private static readonly ScoreCombiner Engine = new(
        new GeneralStrategies(
            Shipped.SatietyScale, Shipped.DensityScales, Shipped.ReferenceMealGrams),
        Shipped.Rules);

    [Fact]
    public void Aggregate_OnePortionOfAnyFood_CarriesEveryNutrientOfThatFood()
    {
        var lost = new List<string>();

        foreach (var food in Sample())
        {
            var plate = PortionAggregate.Aggregate([new Portion(food, 100)]);

            foreach (var (name, of) in Nutrients)
            {
                // Tolerance, not equality: the aggregate arrives at the same number by a
                // different route — value * grams / total — so the last bits differ. What this
                // property is about is a nutrient going missing, not the fourteenth decimal.
                if (of(plate) is null != of(food) is null
                    || Math.Abs((of(plate) ?? 0) - (of(food) ?? 0)) > 1e-9)
                {
                    lost.Add($"{name}: {of(food)} → {of(plate)}");
                }
            }
        }

        Assert.Empty(lost);
    }

    [Fact]
    public void GradeFor_AnyFoodGradedTwice_GivesTheSameLetter()
    {
        foreach (var food in Sample())
        {
            foreach (var lens in Shipped.Lenses)
            {
                Assert.Equal(
                    Shipped.GradeFor(Engine.Combine(food, lens), lens),
                    Shipped.GradeFor(Engine.Combine(food, lens), lens));
            }
        }
    }

    [Fact]
    public void Combine_AnyFood_KeepsEveryComponentInsideZeroToOneHundred()
    {
        var outside = new List<string>();

        foreach (var food in Sample())
        {
            foreach (var lens in Shipped.Lenses)
            {
                var score = Engine.Combine(food, lens);

                foreach (var (name, value) in new (string, ComponentValue?)[]
                    { ("satiety", score.Satiety), ("density", score.Density),
                      ("protein", score.ProteinQuality), ("combined", new(score.Value, false)) })
                {
                    if (value is not null && (value.Score < 0 || value.Score > 100))
                    {
                        outside.Add($"{food.Category} · {lens.Name} · {name} = {value.Score}");
                    }
                }
            }
        }

        Assert.Empty(outside);
    }

    [Fact]
    public void GradeFor_AnyFoodWhoseComponentARuleReplaced_IsNeverFlooredToE()
    {
        var floored = new List<string>();

        foreach (var food in Sample())
        {
            foreach (var lens in Shipped.Lenses)
            {
                var score = Engine.Combine(food, lens);

                if (!score.CategoryIsRuled)
                {
                    continue;
                }

                // A food with no grade at all was not floored, it was never graded. The floor is
                // what this property is about, so the two must not be counted as the same event.
                if (Shipped.GradeFor(score, lens) is { } grade
                    && grade != Shipped.ThresholdsFor(lens).GradeForScoreAlone(score.Value))
                {
                    floored.Add($"{food.Category} · {lens.Name}");
                }
            }
        }

        Assert.Empty(floored);
    }

    [Fact]
    public void Calculate_AnyFoodWithMoreSodium_NeverScoresHigherOnDensity()
    {
        var risen = new List<string>();

        foreach (var food in Sample())
        {
            var saltier = food with { Sodium = food.Sodium + 100 };

            var before = DensityScore.Calculate(ForDensity(food));
            var after = DensityScore.Calculate(ForDensity(saltier));

            if (before is not null && after > before)
            {
                risen.Add($"{food.Category}: {before} → {after}");
            }
        }

        Assert.Empty(risen);
    }

    private static readonly (string Name, Func<FoodInput, double?> Of)[] Nutrients =
    [
        ("calories", food => food.Calories), ("protein", food => food.Protein),
        ("fat", food => food.Fat), ("fiber", food => food.Fiber),
        ("vitaminA", food => food.VitaminA), ("vitaminC", food => food.VitaminC),
        ("vitaminE", food => food.VitaminE), ("calcium", food => food.Calcium),
        ("iron", food => food.Iron), ("magnesium", food => food.Magnesium),
        ("potassium", food => food.Potassium), ("saturatedFat", food => food.SaturatedFat),
        ("sodium", food => food.Sodium), ("vitaminD", food => food.VitaminD),
        ("thiamine", food => food.Thiamine)
    ];

    private static DensityInput ForDensity(FoodInput food) => new(
        food.Calories, food.Protein, food.Fiber, food.VitaminA, food.VitaminC, food.VitaminE,
        food.Calcium, food.Iron, food.Magnesium, food.Potassium, food.SaturatedFat, food.Sodium,
        food.VitaminD, food.Thiamine);

    private static IEnumerable<FoodInput> Sample()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "catalogue-sample.csv");
        var lines = File.ReadAllLines(path).Where(line => !line.StartsWith('#')).Skip(1);
        var lenses = Shipped.Lenses.Length;

        foreach (var line in lines.Where(line => line.Length > 0))
        {
            var cells = Split(line);

            yield return new FoodInput(
                Category: cells[16 + lenses],
                Calories: Number(cells[1]), Protein: Number(cells[2]), Fat: Number(cells[3]),
                Fiber: Number(cells[4]), VitaminA: Absent(cells[5]), VitaminC: Absent(cells[6]),
                VitaminE: Absent(cells[7]), Calcium: Absent(cells[8]), Iron: Absent(cells[9]),
                Magnesium: Absent(cells[10]), Potassium: Absent(cells[11]),
                SaturatedFat: Number(cells[12]), Sodium: Number(cells[13]),
                VitaminD: Absent(cells[14]), Thiamine: Absent(cells[15]));
        }
    }

    private static string[] Split(string line)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;

        foreach (var character in line)
        {
            if (character == '"')
            {
                quoted = !quoted;
            }
            else if (character == ',' && !quoted)
            {
                cells.Add(cell.ToString());
                cell.Clear();
            }
            else
            {
                cell.Append(character);
            }
        }

        cells.Add(cell.ToString());

        return [.. cells];
    }

    private static double Number(string cell) => double.Parse(cell, CultureInfo.InvariantCulture);

    private static double? Absent(string cell) => cell.Length == 0 ? null : Number(cell);
}
