using System.Globalization;
using System.Text;

namespace Sated.Scoring.Tests;

public class CatalogueSampleTests
{
    private static readonly Calibration Shipped = Calibration.Load();

    private static readonly ScoreCombiner Engine = new(
        new GeneralStrategies(
            Shipped.SatietyScale, Shipped.DensityScales, Shipped.ReferenceMealGrams),
        Shipped.Rules);

    [Fact]
    public void GradeFor_EveryFoodInTheCatalogueSample_MatchesTheRecordedLetter()
    {
        var moved = new List<string>();

        foreach (var food in Sample())
        {
            for (var index = 0; index < Shipped.Lenses.Length; index++)
            {
                var lens = Shipped.Lenses[index];
                var grade = Shipped.GradeFor(Engine.Combine(food.Input, lens), lens);

                if (grade != food.Grades[index])
                {
                    moved.Add($"{food.Description} · {lens.Name} · " +
                        $"{food.Grades[index]} → {grade}");
                }
            }
        }

        Assert.Empty(moved);
    }

    [Fact]
    public void GradeFor_TheWatermelonTheEpicNames_IsAUnderWeightLossAndCUnderFitness()
    {
        var watermelon = new FoodInput(
            Category: "Melons",
            Calories: 30, Protein: 0.61, Fat: 0.15, Fiber: 0.4,
            VitaminA: 28, VitaminC: 8.1, VitaminE: 0.05,
            Calcium: 7, Iron: 0.24, Magnesium: 10, Potassium: 112,
            SaturatedFat: 0.016, Sodium: 1,
            VitaminD: 0, Thiamine: 0.033);

        Assert.Equal(Grade.A, GradeUnder(watermelon, "weight-loss"));
        Assert.Equal(Grade.C, GradeUnder(watermelon, "fitness"));
    }

    private static Grade? GradeUnder(FoodInput food, string lensId)
    {
        var lens = Shipped.Lenses.Single(candidate => candidate.Id == lensId);

        return Shipped.GradeFor(Engine.Combine(food, lens), lens);
    }

    private static IEnumerable<SampleFood> Sample()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "catalogue-sample.csv");
        var lines = File.ReadAllLines(path).Where(line => !line.StartsWith('#')).Skip(1);

        foreach (var line in lines.Where(line => line.Length > 0))
        {
            var cells = Split(line);
            var lenses = Shipped.Lenses.Length;

            yield return new SampleFood(
                Description: string.Join(',', cells.Skip(17 + lenses)),
                Grades: [.. cells.Skip(16).Take(lenses).Select(ParseGrade)],
                Input: new FoodInput(
                    Category: cells[16 + lenses],
                    Calories: Number(cells[1]),
                    Protein: Number(cells[2]),
                    Fat: Number(cells[3]),
                    Fiber: Number(cells[4]),
                    VitaminA: Absent(cells[5]),
                    VitaminC: Absent(cells[6]),
                    VitaminE: Absent(cells[7]),
                    Calcium: Absent(cells[8]),
                    Iron: Absent(cells[9]),
                    Magnesium: Absent(cells[10]),
                    Potassium: Absent(cells[11]),
                    SaturatedFat: Number(cells[12]),
                    Sodium: Number(cells[13]),
                    VitaminD: Absent(cells[14]),
                    Thiamine: Absent(cells[15])));
        }
    }

    // Half the category names carry a comma — "Olives, pickles, pickled vegetables" — so the file
    // quotes them and this has to honour it. Splitting on every comma reads the category short and
    // shifts every column after it, which is how a green test can still be reading nonsense.
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

    private static double Number(string cell) =>
        double.Parse(cell, CultureInfo.InvariantCulture);

    private static double? Absent(string cell) =>
        cell.Length == 0 ? null : Number(cell);

    // A dash is a food the engine gives no letter to, not a letter that failed to parse.
    private static Grade? ParseGrade(string cell) =>
        cell == "-" ? null : Enum.Parse<Grade>(cell);

    private record SampleFood(string Description, Grade?[] Grades, FoodInput Input);
}
