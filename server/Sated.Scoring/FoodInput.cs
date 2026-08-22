namespace Sated.Scoring;

/// <summary>
/// One food as the engine sees it: its category, its nutrients per 100 g, and its leucine when
/// the catalogue happens to carry it. Units follow <see cref="DensityInput"/>.
/// </summary>
/// <param name="Category">
/// The catalogue's own category name, verbatim. It selects the category rule (FR-6), so a
/// renamed or normalised category silently changes which rule applies.
/// </param>
public record FoodInput(
    string Category,
    double Calories,
    double Protein,
    double Fat,
    double Fiber,
    double VitaminA,
    double VitaminC,
    double VitaminE,
    double Calcium,
    double Iron,
    double Magnesium,
    double Potassium,
    double SaturatedFat,
    double Sodium,
    double? LeucinePer100g = null
)
{
    // The three scores read overlapping nutrients, and used to be handed their own input record
    // each. Two records meant calories could read 165 in one and 200 in the other, producing a
    // grade for a food that does not exist without anything failing. One record, derived twice.

    internal SatietyInput ForSatiety() => new(Calories, Protein, Fat, Fiber);

    internal DensityInput ForDensity() => new(
        Calories, Protein, Fiber, VitaminA, VitaminC, VitaminE,
        Calcium, Iron, Magnesium, Potassium, SaturatedFat, Sodium);
}
