namespace Sated.Parsing;

internal static class NutrientCodes
{
    public const string Calories = "208";
    public const string Protein = "203";
    public const string Fat = "204";
    public const string Fiber = "291";
    public const string SaturatedFat = "606";
    public const string Sodium = "307";

    public const string VitaminA = "320";
    public const string VitaminC = "401";
    public const string VitaminD = "328";
    public const string VitaminE = "323";
    public const string Thiamine = "404";
    public const string Calcium = "301";
    public const string Iron = "303";
    public const string Magnesium = "304";
    public const string Potassium = "306";
    public const string Leucine = "504";

    public static readonly string[] Required =
        [Calories, Protein, Fat, Fiber, SaturatedFat, Sodium];
}
