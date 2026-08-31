using Sated.Data.Entities;

namespace Sated.Parsing;

public static class SurveyPortions
{
    public const string QuantityNotSpecified = "Quantity not specified";

    public static FoodServing[] Of(SurveyFood food) =>
        [.. Usable(food)
            .Where(portion => portion.Description != QuantityNotSpecified)
            .OrderBy(portion => portion.SequenceNumber)
            .Select(portion => new FoodServing
            {
                Description = portion.Description!,
                Grams = portion.GramWeight,
                Sequence = portion.SequenceNumber
            })];

    public static double? TypicalGramsOf(SurveyFood food) =>
        Usable(food)
            .Where(portion => portion.Description == QuantityNotSpecified)
            .OrderBy(portion => portion.SequenceNumber)
            .Select(portion => (double?)portion.GramWeight)
            .FirstOrDefault();

    private static IEnumerable<SurveyPortion> Usable(SurveyFood food) =>
        (food.Portions ?? [])
            .Where(portion => portion.GramWeight > 0)
            .Where(portion => !string.IsNullOrWhiteSpace(portion.Description));
}
