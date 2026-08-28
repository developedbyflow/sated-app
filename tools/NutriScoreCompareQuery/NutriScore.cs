using System.Globalization;

namespace NutriScoreCompare;

/// <summary>
/// The Nutri-Score 2023 algorithm for GENERAL foods only, as published by Santé publique France.
/// </summary>
/// <remarks>
/// Beverages and the fats/oils/nuts/seeds family are scored under different rules and are NOT
/// implemented here. That is a deliberate refusal rather than an omission: the general branch can
/// be checked against a worked example whose answer we already verified against the official
/// calculator (cheddar, FNDDS 2705709, score 16, grade D), and the other two cannot. An unchecked
/// reimplementation would produce a number that looks like evidence and is not.
/// The red meat protein cap is also not implemented; it only lowers scores, for a bounded set.
/// </remarks>
public static class NutriScore
{
    // Points are "how many thresholds does this value pass", so each table is read the same way.
    private static readonly double[] EnergyKj =
        [335, 670, 1005, 1340, 1675, 2010, 2345, 2680, 3015, 3350];

    private static readonly double[] SugarsG =
        [3.4, 6.8, 10, 14, 17, 20, 24, 27, 31, 34, 37, 41, 44, 48, 51];

    private static readonly double[] SaturatedFatG = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    private static readonly double[] SaltMg =
        [200, 400, 600, 800, 1000, 1200, 1400, 1600, 1800, 2000,
         2200, 2400, 2600, 2800, 3000, 3200, 3400, 3600, 3800, 4000];

    private static readonly double[] ProteinG = [2.4, 4.8, 7.2, 9.6, 12, 14, 17];

    private static readonly double[] FibreG = [3.0, 4.1, 5.2, 6.3, 7.4];

    // Sodium is reported in mg; salt is what the algorithm asks for, and the conversion is fixed.
    private const double SaltPerSodium = 2.5;

    // A kilocalorie is 4.184 kJ. The tables are in kJ because the algorithm is European.
    private const double KjPerKcal = 4.184;

    /// <summary>The negative side, the positive side, and the score they produce.</summary>
    /// <param name="isCheese">
    /// Cheese takes its protein points unconditionally, which is the one exception the published
    /// algorithm makes to the rule below. It is implemented because it is the only branch we hold a
    /// verified worked example for — without it the check on cheddar passes by a slack of exactly
    /// the protein points, which is a check that cannot fail for the right reason.
    /// </param>
    public static Result Calculate(Input food, bool isCheese = false)
    {
        var energy = Points(food.CaloriesPer100g * KjPerKcal, EnergyKj);
        var sugars = Points(food.SugarsG, SugarsG);
        var saturated = Points(food.SaturatedFatG, SaturatedFatG);
        var salt = Points(food.SodiumMg * SaltPerSodium, SaltMg);

        var negative = energy + sugars + saturated + salt;

        var protein = Points(food.ProteinG, ProteinG);
        var fibre = Points(food.FibreG, FibreG);
        var produce = ProducePoints(food.ProduceSharePercent);

        // The rule that keeps a high-negative food from buying its way back with protein alone.
        // Cheese is the exception in the published algorithm; it is not reachable here, because
        // this implementation refuses the categories it would apply to.
        var positive = negative >= 11 && !isCheese
            ? fibre + produce
            : protein + fibre + produce;

        var score = negative - positive;

        return new Result(score, Grade(score), negative, positive,
            energy, sugars, saturated, salt, protein, fibre, produce);
    }

    /// <summary>A to E from the score, using the 2023 cutoffs for general foods.</summary>
    public static char Grade(int score) =>
        score <= 0 ? 'A'
        : score <= 2 ? 'B'
        : score <= 10 ? 'C'
        : score <= 18 ? 'D'
        : 'E';

    // 0 below 40%, then 1, 2 and 5. The jump from 2 to 5 is in the published table, not a typo.
    private static int ProducePoints(double percent) =>
        percent > 80 ? 5
        : percent > 60 ? 2
        : percent > 40 ? 1
        : 0;

    private static int Points(double value, double[] thresholds)
    {
        var points = 0;

        foreach (var threshold in thresholds)
        {
            if (value > threshold)
            {
                points++;
            }
        }

        return points;
    }

    public record Input(
        double CaloriesPer100g,
        double SugarsG,
        double SaturatedFatG,
        double SodiumMg,
        double ProteinG,
        double FibreG,
        double ProduceSharePercent
    );

    public record Result(
        int Score,
        char Grade,
        int Negative,
        int Positive,
        int EnergyPoints,
        int SugarPoints,
        int SaturatedFatPoints,
        int SaltPoints,
        int ProteinPoints,
        int FibrePoints,
        int ProducePoints
    )
    {
        public string Breakdown =>
            string.Create(CultureInfo.InvariantCulture,
                $"energie {EnergyPoints} + zaharuri {SugarPoints} + saturate {SaturatedFatPoints} " +
                $"+ sare {SaltPoints} = {Negative}  ·  proteină {ProteinPoints} + fibră {FibrePoints} " +
                $"+ fructe/legume {ProducePoints} = {Positive}");
    }
}
