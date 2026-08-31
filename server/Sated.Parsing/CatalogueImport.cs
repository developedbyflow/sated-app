using System.Text.Json;
using Sated.Data.Entities;

namespace Sated.Parsing;

public record ImportResult(IReadOnlyList<Food> Accepted, IReadOnlyList<Rejection> Rejected);

public static class CatalogueImport
{
    private const string NotTheEatenForm = "not reconstituted";

    public static ImportResult Read(Stream survey)
    {
        var file = JsonSerializer.Deserialize<SurveyFile>(survey)
            ?? throw new InvalidDataException("The survey file parsed to nothing.");

        var accepted = new List<Food>();
        var rejected = new List<Rejection>();

        foreach (var food in file.Foods)
        {
            var amounts = AmountsByCode(food);
            var reason = ReasonToReject(food, amounts);

            if (reason is not null)
            {
                rejected.Add(new Rejection(food.FdcId, food.Description, reason.Value));
                continue;
            }

            accepted.Add(ToFood(food, amounts));
        }

        return new ImportResult(accepted, rejected);
    }

    private static RejectionReason? ReasonToReject(
        SurveyFood food, IReadOnlyDictionary<string, double> amounts)
    {
        if (food.Category is null || !CatalogueCategories.Selected.Contains(food.Category.Description))
        {
            return RejectionReason.OutsideTheSelectedCategories;
        }

        if (food.Description.Contains(NotTheEatenForm, StringComparison.OrdinalIgnoreCase))
        {
            return RejectionReason.NotTheEatenForm;
        }

        if (!NutrientCodes.Required.All(amounts.ContainsKey))
        {
            return RejectionReason.MissingRequiredNutrient;
        }

        return null;
    }

    private static Food ToFood(SurveyFood food, IReadOnlyDictionary<string, double> amounts) =>
        new()
        {
            FdcId = food.FdcId,
            Description = food.Description,
            Category = food.Category!.Description,
            Source = FoodSource.UsdaFndds,
            TypicalGrams = SurveyPortions.TypicalGramsOf(food),
            Servings = [.. SurveyPortions.Of(food)],
            Nutrients = new NutrientAmounts
            {
                Calories = amounts[NutrientCodes.Calories],
                Protein = amounts[NutrientCodes.Protein],
                Fat = amounts[NutrientCodes.Fat],
                Fiber = amounts[NutrientCodes.Fiber],
                SaturatedFat = amounts[NutrientCodes.SaturatedFat],
                Sodium = amounts[NutrientCodes.Sodium],
                VitaminA = Optional(amounts, NutrientCodes.VitaminA),
                VitaminC = Optional(amounts, NutrientCodes.VitaminC),
                VitaminD = Optional(amounts, NutrientCodes.VitaminD),
                VitaminE = Optional(amounts, NutrientCodes.VitaminE),
                Thiamine = Optional(amounts, NutrientCodes.Thiamine),
                Calcium = Optional(amounts, NutrientCodes.Calcium),
                Iron = Optional(amounts, NutrientCodes.Iron),
                Magnesium = Optional(amounts, NutrientCodes.Magnesium),
                Potassium = Optional(amounts, NutrientCodes.Potassium),
                Leucine = Optional(amounts, NutrientCodes.Leucine)
            }
        };

    private static double? Optional(IReadOnlyDictionary<string, double> amounts, string code) =>
        amounts.TryGetValue(code, out var amount) ? amount : null;

    private static IReadOnlyDictionary<string, double> AmountsByCode(SurveyFood food) =>
        food.Nutrients
            .Where(entry => entry.Amount is not null)
            .GroupBy(entry => entry.Nutrient.Number)
            .ToDictionary(group => group.Key, group => group.First().Amount!.Value);
}
