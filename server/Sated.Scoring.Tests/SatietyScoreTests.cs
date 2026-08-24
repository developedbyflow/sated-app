namespace Sated.Scoring.Tests;

public class SatietyScoreTests
{
    [Fact]
    public void Calculate_ChickenBreast_MatchesBriefValue()
    {
        var chickenBreast = new SatietyInput(
            Calories: 165,
            Protein: 31,
            Fat: 3.6,
            Fiber: 0
        );

        var score = SatietyScore.Calculate(chickenBreast);

        Assert.Equal(3.3, score, tolerance: 0.1);
    }

    [Fact]
    public void Calculate_Croissant_MatchesBriefValue()
    {
        var croissant = new SatietyInput(
            Calories: 406,
            Protein: 8.2,
            Fat: 21,
            Fiber: 2.6
        );

        var score = SatietyScore.Calculate(croissant);

        Assert.Equal(1.6, score, tolerance: 0.1);
    }

        [Fact]
    public void Calculate_ProteinAboveCap_ScoresSameAsAtCap()
    {
        var atCap = new SatietyInput(Calories: 200, Protein: 30, Fat: 5, Fiber: 5);
        var aboveCap = atCap with { Protein = 80 };

        Assert.Equal(SatietyScore.Calculate(atCap), SatietyScore.Calculate(aboveCap));
    }

    [Fact]
    public void Calculate_FiberAboveCap_ScoresSameAsAtCap()
    {
        var atCap = new SatietyInput(Calories: 250, Protein: 14, Fat: 3.5, Fiber: 12);
        var aboveCap = atCap with { Fiber = 27 };

        Assert.Equal(SatietyScore.Calculate(atCap), SatietyScore.Calculate(aboveCap));
    }

    [Fact]
    public void Calculate_FatAboveCap_ScoresSameAsAtCap()
    {
        var atCap = new SatietyInput(Calories: 600, Protein: 20, Fat: 50, Fiber: 7);
        var aboveCap = atCap with { Fat = 80 };

        Assert.Equal(SatietyScore.Calculate(atCap), SatietyScore.Calculate(aboveCap));
    }

    [Fact]
    public void Calculate_CaloriesBelowFloor_ScoresSameAsAtFloor()
    {
        var atFloor = new SatietyInput(Calories: 30, Protein: 2, Fat: 0.2, Fiber: 1);
        var belowFloor = atFloor with { Calories = 5 };

        Assert.Equal(SatietyScore.Calculate(atFloor), SatietyScore.Calculate(belowFloor));
    }

        [Fact]
    public void Calculate_MaximalInput_ClampsToFive()
    {
        var maximal = new SatietyInput(Calories: 30, Protein: 30, Fat: 0, Fiber: 12);

        Assert.Equal(5, SatietyScore.Calculate(maximal));
    }

    [Fact]
    public void Calculate_OliveOil_ClampsToZeroPointFive()
    {
        var oliveOil = new SatietyInput(Calories: 884, Protein: 0, Fat: 100, Fiber: 0);

        Assert.Equal(0.5, SatietyScore.Calculate(oliveOil));
    }

    [Fact]
    public void Calculate_EnergyReportedInKilojoules_Throws()
    {
        var lardInKilojoules = new SatietyInput(
            Calories: 902 * 4.184, Protein: 0, Fat: 100, Fiber: 0);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => SatietyScore.Calculate(lardInKilojoules));
    }

    [Fact]
    public void Calculate_TheDensestFoodInTheCatalogue_IsAccepted()
    {
        var lard = new SatietyInput(Calories: 902, Protein: 0, Fat: 100, Fiber: 0);

        Assert.InRange(SatietyScore.Calculate(lard), 0.5, 5);
    }
}