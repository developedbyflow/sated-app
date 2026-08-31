using Sated.Data.Entities;

namespace Sated.Api.Dtos;

public record FoodProvenanceDto(
    FoodSource Source,
    string[] Estimated,
    string[] Absent)
{
    public static FoodProvenanceDto Of(Food food) => new(
        food.Source,
        food.Nutrients.Leucine is null ? ["leucine"] : [],
        [.. WithoutAValue(food.Nutrients)]);

    private static IEnumerable<string> WithoutAValue(NutrientAmounts nutrients)
    {
        if (nutrients.VitaminA is null) yield return "vitaminA";
        if (nutrients.VitaminC is null) yield return "vitaminC";
        if (nutrients.VitaminD is null) yield return "vitaminD";
        if (nutrients.VitaminE is null) yield return "vitaminE";
        if (nutrients.Thiamine is null) yield return "thiamine";
        if (nutrients.Calcium is null) yield return "calcium";
        if (nutrients.Iron is null) yield return "iron";
        if (nutrients.Magnesium is null) yield return "magnesium";
        if (nutrients.Potassium is null) yield return "potassium";
    }
}
