namespace Sated.Scoring.Tests;

// The calibration the tests were written against, spelled out here so a unit test never depends
// on the shipped file: a recalibration must break the tests that check the calibration, not every
// test that happened to need a lens. CalibrationTests is where these are held against the file.
internal static class Frozen
{
    public static readonly Lens WeightLoss =
        new("Weight Loss", satiety: 50, density: 30, proteinQuality: 20);

    public static readonly Lens Fitness =
        new("Fitness", satiety: 25, density: 25, proteinQuality: 50);

    public static readonly GradeThresholds WeightLossCutoffs =
        new(dStartsAt: 31.81, cStartsAt: 45.55, bStartsAt: 58.64, aStartsAt: 71.77);

    public static readonly GradeThresholds FitnessCutoffs =
        new(dStartsAt: 32.80, cStartsAt: 46.89, bStartsAt: 57.18, aStartsAt: 71.50);

    public const double ReferenceMealGrams = 300;

    public const double DensityFloor = 8;
}
