namespace Sated.Scoring;

/// <summary>
/// The letter shown for a food, from A (best) to E (worst).
/// Not a way to compare two foods: a Swap compares scores within a category, because two foods
/// can share a letter while their scores differ sixteenfold — olive oil and butter both read E.
/// </summary>
public enum Grade
{
    A,
    B,
    C,
    D,
    E
}
