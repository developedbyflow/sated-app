namespace Sated.Scoring;

public static class SatietyScore
{
    // Fullness Factor, from patent US 7,620,531 B1 (CondeNet, 2005). Not peer-reviewed.
    // The five coefficients were fitted together — do not tune one alone.
    // The four limits are the boundary of the measured data: past them the cubed terms diverge.

    private const double CalorieFloor = 30;
    private const double ProteinCap = 30;
    private const double FiberCap = 12;
    private const double FatCap = 50;

    /// <summary>
    /// Calculates the Satiety Score from a food's per-100g nutrient values.
    /// </summary>
    /// <returns>A score between 0.5 and 5. Higher means more filling per calorie.</returns>
    public static double Calculate(SatietyInput profile)
    {
        var cal = Math.Max(CalorieFloor, profile.Calories);
        var pr = Math.Min(ProteinCap, profile.Protein);
        var df = Math.Min(FiberCap, profile.Fiber);
        var tf = Math.Min(FatCap, profile.Fat);

        var raw = 41.7 / Math.Pow(cal, 0.7)
                + 0.05 * pr
                + 0.000617 * Math.Pow(df, 3)
                - 0.00000725 * Math.Pow(tf, 3)
                + 0.617;

        return Math.Clamp(raw, 0.5, 5);
    }
}