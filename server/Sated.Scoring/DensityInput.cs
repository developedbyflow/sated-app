namespace Sated.Scoring;

/// <summary>
/// A food's nutrient values per 100 g, as reported by USDA FNDDS.
/// Protein, fiber and saturated fat are grams; vitamin A is micrograms RAE;
/// every other micronutrient is milligrams.
/// </summary>
public record DensityInput(
    double Calories,
    double Protein,
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
    double VitaminD = 0,
    double Thiamine = 0
);