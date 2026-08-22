namespace Sated.Scoring;

/// <summary>
/// One way of computing one component of a grade, already on 0-100.
/// A category rule swaps the strategy for a component; it can never set the grade itself.
/// </summary>
/// <param name="grams">How much of the food is being scored.</param>
/// <returns>
/// A component score between 0 and 100, or null when this food has no value for it — missing
/// data or a calculation with no answer. Never zero to mean absent (FR-7).
/// </returns>
public delegate double? ComponentStrategy(FoodInput food, double grams);
