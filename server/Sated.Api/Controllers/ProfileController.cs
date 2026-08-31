using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Sated.Api.Dtos;
using Sated.Data.Entities;
using Sated.Services;

namespace Sated.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController(
    UserManager<AppUser> users, Profiles profiles, Consents consents) : ControllerBase
{
    [HttpGet]
    public async Task<ProfileResponseDto> Get()
    {
        var userId = users.GetUserId(User)!;
        var user = await profiles.Of(userId);

        return new ProfileResponseDto(
            user.WeightKg,
            user.HeightCm,
            user.CalorieTargetKcal,
            user.ActiveLensId,
            await consents.IsGiven(userId, ConsentPurpose.HealthData));
    }

    [HttpPut("calorie-target")]
    public async Task<CalorieTargetResponseDto> PutCalorieTarget(CalorieTargetRequestDto request)
    {
        await profiles.SetCalorieTarget(users.GetUserId(User)!, request.Kcal!.Value);

        return CalorieTargetResponseDto.For(request.Kcal.Value);
    }

    [HttpDelete("calorie-target")]
    public async Task<NoContentResult> DeleteCalorieTarget()
    {
        await profiles.ClearCalorieTarget(users.GetUserId(User)!);

        return NoContent();
    }

    [HttpPut]
    public async Task<ActionResult<ProfileResponseDto>> Put(ProfileRequestDto request)
    {
        var userId = users.GetUserId(User)!;

        var update = await profiles.Update(
            userId, request.WeightKg!.Value, request.HeightCm!.Value, request.ActiveLensId);

        if (update is ProfileUpdate.ConsentMissing)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Consent is missing",
                detail: "Weight is health data and cannot be stored without explicit consent. "
                    + $"POST /api/consents/{ConsentPurpose.HealthData} first.");
        }

        if (update is ProfileUpdate.UnknownLens)
        {
            ModelState.AddModelError(
                nameof(request.ActiveLensId),
                $"No lens has the id '{request.ActiveLensId}'. GET /api/lenses lists the ones that exist.");

            return ValidationProblem(ModelState);
        }

        return await Get();
    }
}
