using Microsoft.AspNetCore.Mvc;
using Sated.Api.Dtos;
using Sated.Scoring;

namespace Sated.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GradesController(Calibration calibration, ScoreCombiner combiner) : ControllerBase
{
    [HttpPost]
    public ActionResult<GradeResponseDto> Post(GradeRequestDto request)
    {
        var lens = calibration.Lenses.FirstOrDefault(lens =>
            string.Equals(lens.Id, request.LensId, StringComparison.OrdinalIgnoreCase));

        if (lens is null)
        {
            ModelState.AddModelError(
                nameof(request.LensId),
                $"No lens has the id '{request.LensId}'. GET /api/lenses lists the ones that exist.");

            return ValidationProblem(ModelState);
        }

        var check = NutrientPlausibility.Check(
            calories: request.Calories!.Value,
            protein: request.Protein!.Value,
            fat: request.Fat!.Value,
            carbohydrate: request.Carbohydrate!.Value,
            alcohol: request.Alcohol ?? 0);

        if (check is not NutrientCheck.Plausible)
        {
            ModelState.AddModelError(nameof(request.Calories), Explain(check));

            return ValidationProblem(ModelState);
        }

        var score = combiner.Combine(ToFoodInput(request), lens);

        return GradeResponseDto.From(calibration.GradeFor(score, lens), score);
    }

    private static string Explain(NutrientCheck check) => check switch
    {
        NutrientCheck.EnergyTooHighForAnyFood =>
            "More energy than 100 g of any food carries. Send kilocalories, not kilojoules.",
        NutrientCheck.EnergyDisagreesWithTheMacronutrients =>
            "The energy does not follow from the protein, fat and carbohydrate sent with it.",
        _ => throw new ArgumentOutOfRangeException(nameof(check))
    };

    private static FoodInput ToFoodInput(GradeRequestDto request) => new(
        Category: request.Category,
        Calories: request.Calories!.Value,
        Protein: request.Protein!.Value,
        Fat: request.Fat!.Value,
        Fiber: request.Fiber!.Value,
        VitaminA: request.VitaminA,
        VitaminC: request.VitaminC,
        VitaminE: request.VitaminE,
        Calcium: request.Calcium,
        Iron: request.Iron,
        Magnesium: request.Magnesium,
        Potassium: request.Potassium,
        SaturatedFat: request.SaturatedFat!.Value,
        Sodium: request.Sodium!.Value,
        VitaminD: request.VitaminD,
        Thiamine: request.Thiamine);
}