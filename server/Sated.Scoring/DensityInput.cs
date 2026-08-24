namespace Sated.Scoring;

/// <summary>
/// A food's nutrient values per 100 g. Protein, fiber and saturated fat are grams; vitamin A and
/// vitamin D are micrograms; every other micronutrient is milligrams.
/// </summary>
/// <remarks>
/// The micronutrients are nullable and null means unknown, never zero. A food typed in from a
/// package label carries only what the label prints — no vitamin A, C or E, no magnesium, no
/// thiamine — and counting those as zero claims the food contains none of them. Measured on the
/// gate's 68 foods, that claim costs exactly one letter on twenty of them.
/// Calories, protein and fiber stay required because every label in the world prints them.
/// Saturated fat and sodium stay required because they are the two limiters: an absent limiter
/// would raise a food's score, so dropping one is never the safe direction.
/// </remarks>
public record DensityInput(
    double Calories,
    double Protein,
    double Fiber,
    double? VitaminA,
    double? VitaminC,
    double? VitaminE,
    double? Calcium,
    double? Iron,
    double? Magnesium,
    double? Potassium,
    double SaturatedFat,
    double Sodium,
    double? VitaminD = null,
    double? Thiamine = null
);