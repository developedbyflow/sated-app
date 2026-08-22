namespace Sated.Scoring;

/// <summary>One food and how much of it went into a Recipe or a Meal (FR-8).</summary>
/// <param name="Grams">
/// The weight as logged. Architecture §Cantitățile freezes this at logging time, so the engine
/// only ever sees grams — never a display unit that would need converting twice.
/// </param>
public record Portion(FoodInput Food, double Grams);
