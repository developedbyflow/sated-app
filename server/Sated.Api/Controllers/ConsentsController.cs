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
public class ConsentsController(UserManager<AppUser> users, Consents consents) : ControllerBase
{
    [HttpGet("{purpose}")]
    public async Task<ActionResult<ConsentResponseDto>> Get(ConsentPurpose purpose)
    {
        var document = await consents.CurrentDocument(purpose);

        if (document is null)
        {
            return NotFound();
        }

        var given = await consents.Standing(users.GetUserId(User)!, purpose);

        return new ConsentResponseDto(
            document.Purpose, document.Version, document.Text, given?.GivenAt);
    }

    [HttpPost("{purpose}")]
    public async Task<ActionResult<ConsentResponseDto>> Give(
        ConsentPurpose purpose, GiveConsentRequestDto request)
    {
        var consent = await consents.Give(users.GetUserId(User)!, request.Version!, purpose);

        if (consent is null)
        {
            ModelState.AddModelError(
                nameof(request.Version),
                $"No {purpose} consent text has version '{request.Version}'. "
                + $"GET /api/consents/{purpose} returns the one in force.");

            return ValidationProblem(ModelState);
        }

        var document = await consents.CurrentDocument(purpose);

        return new ConsentResponseDto(
            purpose, document!.Version, document.Text, consent.GivenAt);
    }

    [HttpDelete("{purpose}")]
    public async Task<IActionResult> Withdraw(ConsentPurpose purpose)
    {
        var withdrawn = await consents.Withdraw(users.GetUserId(User)!, purpose);

        return withdrawn ? NoContent() : NotFound();
    }
}
