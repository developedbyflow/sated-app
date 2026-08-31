using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sated.Api.Dtos;
using Sated.Data.Entities;

namespace Sated.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(UserManager<AppUser> users, SignInManager<AppUser> sessions)
    : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<CurrentUserDto>> Register(RegisterRequestDto request)
    {
        var user = new AppUser { UserName = request.Email, Email = request.Email };

        var created = await users.CreateAsync(user, request.Password!);

        if (!created.Succeeded)
        {
            foreach (var failure in created.Errors)
            {
                ModelState.AddModelError(failure.Code, failure.Description);
            }

            return ValidationProblem(ModelState);
        }

        await sessions.SignInAsync(user, isPersistent: false);

        return new CurrentUserDto(user.Id, user.Email!);
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<CurrentUserDto>> Login(LoginRequestDto request)
    {
        var signedIn = await sessions.PasswordSignInAsync(
            request.Email!, request.Password!, isPersistent: false, lockoutOnFailure: true);

        if (!signedIn.Succeeded)
        {
            return Unauthorized();
        }

        var user = await users.FindByEmailAsync(request.Email!);

        return new CurrentUserDto(user!.Id, user.Email!);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await sessions.SignOutAsync();

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CurrentUserDto>> Me()
    {
        var user = await users.GetUserAsync(User);

        return new CurrentUserDto(user!.Id, user.Email!);
    }
}
