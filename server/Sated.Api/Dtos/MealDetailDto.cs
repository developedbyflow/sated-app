using Sated.Data.Entities;
using Sated.Scoring;
using Sated.Services;

namespace Sated.Api.Dtos;

public record MealEntryDto(
    int Id,
    int FoodId,
    string Description,
    double QuantityGrams,
    double DisplayAmount,
    string DisplayUnit,
    bool QuantityEstimated,
    int? FromRecipeId,
    string? FromRecipeName);

public record MealDetailDto(
    int Id,
    DateOnly Date,
    string Name,
    DateTimeOffset LoggedAt,
    string EngineVersion,
    double TotalGrams,
    MealEntryDto[] Entries,
    GradeResponseDto? Grade)
{
    public static MealDetailDto From(Meal meal, GradeResponseDto? grade) => new(
        meal.Id,
        meal.Day.Date,
        meal.Name,
        meal.LoggedAt,
        meal.EngineVersion,
        meal.Entries.Sum(entry => entry.QuantityGrams),
        [.. meal.Entries.Select(entry => new MealEntryDto(
            entry.Id,
            entry.FoodId,
            entry.Food.Description,
            entry.QuantityGrams,
            entry.DisplayAmount,
            entry.DisplayUnit,
            entry.QuantityEstimated,
            entry.FromRecipeId,
            entry.FromRecipeName))],
        grade);
}

public record DayDto(DateOnly Date, MealDetailDto[] Meals);

public record MealExportDto(
    DateOnly Date,
    string Name,
    DateTimeOffset LoggedAt,
    string EngineVersion,
    double TotalGrams,
    MealEntryDto[] Entries)
{
    public static MealExportDto From(Meal meal)
    {
        var detail = MealDetailDto.From(meal, null);

        return new MealExportDto(
            detail.Date,
            detail.Name,
            detail.LoggedAt,
            detail.EngineVersion,
            detail.TotalGrams,
            detail.Entries);
    }
}
