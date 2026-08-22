namespace Sated.Scoring;

/// <summary>
/// One way of computing one component of a grade, already on 0-100.
/// A category rule swaps the strategy for a component; it can never set the grade itself.
/// </summary>
/// <remarks>
/// No portion is passed in. Every component is a per-100 g quantity — the protein component
/// included, since Story 1.9 moved it onto a fixed reference meal — so how much was eaten
/// cannot change a grade. Keeping the parameter would advertise a knob that does nothing.
/// </remarks>
/// <returns>
/// A component score between 0 and 100, or null when this food has no value for it — missing
/// data or a calculation with no answer. Never zero to mean absent (FR-7).
/// </returns>
public delegate ComponentValue? ComponentStrategy(FoodInput food);
