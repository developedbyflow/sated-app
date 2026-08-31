namespace Sated.Scoring.Tests;

public class ProteinTargetTests
{
    private const double IdealKgAt180 = 22 * 1.8 * 1.8;

    [Fact]
    public void For_LeanUser_ScalesAlmostOnTheActualWeight()
    {
        var target = ProteinTarget.For(weightKg: 80, heightCm: 180, Frozen.WeightLoss);

        Assert.Equal(117.5, target!.MinGrams, tolerance: 0.1);
        Assert.Equal(161.6, target.MaxGrams, tolerance: 0.1);
    }

    [Fact]
    public void For_HeavyUser_StaysBelowWhatTheActualWeightWouldEvenAsk()
    {
        var target = ProteinTarget.For(weightKg: 130, heightCm: 180, Frozen.WeightLoss);

        Assert.Equal(137.5, target!.MinGrams, tolerance: 0.1);
        Assert.True(target.MaxGrams < 130 * Frozen.WeightLoss.ProteinPerKg!.Min);
    }

    [Fact]
    public void AdjustedKg_BelowIdealWeight_IsTheActualWeight()
    {
        Assert.Equal(65, ProteinTarget.AdjustedKg(weightKg: 65, heightCm: 180));
    }

    [Fact]
    public void AdjustedKg_AboveIdealWeight_CountsAQuarterOfTheExcess()
    {
        Assert.Equal(
            IdealKgAt180 + 1,
            ProteinTarget.AdjustedKg(IdealKgAt180 + 4, heightCm: 180),
            tolerance: 0.001);
    }

    [Fact]
    public void AdjustedKg_TheSameWeightOnAShorterBody_CountsMoreOfItAsExcess()
    {
        var tall = ProteinTarget.AdjustedKg(weightKg: 82, heightCm: 180);
        var shorter = ProteinTarget.AdjustedKg(weightKg: 82, heightCm: 165);

        Assert.True(shorter < tall);
    }

    [Fact]
    public void For_UserWithoutAWeight_HasNoTarget()
    {
        Assert.Null(ProteinTarget.For(weightKg: null, heightCm: 180, Frozen.WeightLoss));
    }

    [Fact]
    public void For_UserWithoutAHeight_HasNoTarget()
    {
        Assert.Null(ProteinTarget.For(weightKg: 80, heightCm: null, Frozen.WeightLoss));
    }

    [Fact]
    public void For_WeightOfZero_HasNoTarget()
    {
        Assert.Null(ProteinTarget.For(weightKg: 0, heightCm: 180, Frozen.WeightLoss));
    }

    [Fact]
    public void For_LensTheFileGivesNoRange_HasNoTarget()
    {
        var uncalibrated =
            new Lens("maintenance", "Maintenance", satiety: 40, density: 30, proteinQuality: 30);

        Assert.Null(ProteinTarget.For(weightKg: 80, heightCm: 180, uncalibrated));
    }

    [Fact]
    public void For_TheSameBodyUnderTwoLenses_AsksMoreUnderWeightLoss()
    {
        var weightLoss = ProteinTarget.For(weightKg: 80, heightCm: 180, Frozen.WeightLoss);
        var fitness = ProteinTarget.For(weightKg: 80, heightCm: 180, Frozen.Fitness);

        Assert.True(weightLoss!.MinGrams > fitness!.MinGrams);
    }
}
