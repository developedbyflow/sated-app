namespace Sated.Scoring.Tests;

// The calibration the tests were written against, spelled out here so a unit test never depends
// on the shipped file: a recalibration must break the tests that check the calibration, not every
// test that happened to need a lens. CalibrationTests is where these are held against the file.
internal static class Frozen
{
    public static readonly Lens WeightLoss = new(
        "weight-loss", "Weight Loss", satiety: 50, density: 35, proteinQuality: 15,
        proteinPerKg: new ProteinPerKg(1.6, 2.2));

    public static readonly Lens Fitness = new(
        "fitness", "Fitness", satiety: 25, density: 25, proteinQuality: 50,
        proteinPerKg: new ProteinPerKg(1.4, 2.0));

    public static readonly GradeThresholds WeightLossCutoffs =
        new(dStartsAt: 29.68, cStartsAt: 43.63, bStartsAt: 57.44, aStartsAt: 72.01);

    public static readonly GradeThresholds FitnessCutoffs =
        new(dStartsAt: 30.96, cStartsAt: 45.38, bStartsAt: 54.58, aStartsAt: 70.99);

    public const double ReferenceMealGrams = 300;

    public const double DensityFloor = 8;
}
