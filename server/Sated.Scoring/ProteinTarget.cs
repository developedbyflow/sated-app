namespace Sated.Scoring;

public record ProteinTarget(double MinGrams, double MaxGrams)
{
    private const double NormalBmi = 22;
    private const double ShareOfExcessWeight = 0.25;

    public static ProteinTarget? For(double? weightKg, double? heightCm, Lens lens)
    {
        if (weightKg is not > 0 || heightCm is not > 0 || lens.ProteinPerKg is null)
        {
            return null;
        }

        var adjustedKg = AdjustedKg(weightKg.Value, heightCm.Value);

        return new ProteinTarget(
            adjustedKg * lens.ProteinPerKg.Min,
            adjustedKg * lens.ProteinPerKg.Max);
    }

    public static double AdjustedKg(double weightKg, double heightCm)
    {
        var heightM = heightCm / 100;
        var idealKg = NormalBmi * heightM * heightM;

        return weightKg <= idealKg
            ? weightKg
            : idealKg + ShareOfExcessWeight * (weightKg - idealKg);
    }
}
