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
public class AccountController(
    UserManager<AppUser> users,
    SignInManager<AppUser> sessions,
    Accounts accounts,
    TimeProvider clock) : ControllerBase
{
    [HttpPost("export")]
    public async Task<ActionResult<AccountExportDto>> Export(ConfirmPasswordRequestDto request)
    {
        var user = await users.GetUserAsync(User);

        if (!await Confirmed(user!, request.Password!))
        {
            return PasswordNotConfirmed();
        }

        var signed = await accounts.SignedDocuments(user!.Id);
        var exportedAt = clock.GetUtcNow();

        Response.Headers.ContentDisposition =
            $"attachment; filename=\"sated-export-{exportedAt:yyyy-MM-dd}.json\"";

        return new AccountExportDto(
            exportedAt,
            user.Email!,
            user.WeightKg,
            user.ActiveLensId,
            signed.Select(consent => new ConsentExportDto(
                consent.Document.Purpose,
                consent.Document.Version,
                consent.GivenAt,
                consent.WithdrawnAt,
                consent.Document.Text)).ToArray());
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(ConfirmPasswordRequestDto request)
    {
        var user = await users.GetUserAsync(User);

        if (!await Confirmed(user!, request.Password!))
        {
            return PasswordNotConfirmed();
        }

        await accounts.Delete(user!);
        await sessions.SignOutAsync();

        return NoContent();
    }

    private async Task<bool> Confirmed(AppUser user, string password)
    {
        var checkedPassword =
            await sessions.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        return checkedPassword.Succeeded;
    }

    private ObjectResult PasswordNotConfirmed() =>
        Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "The password was not confirmed",
            detail: "Exporting an account and deleting one both ask for the password again. "
                + "A session on its own is not enough for either.");
}
