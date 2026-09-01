using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Sated.Api.Dtos;
using Sated.Data.Entities;
using Sated.Services;

namespace Sated.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    UserManager<AppUser> users,
    SignInManager<AppUser> sessions,
    AccountRecovery recovery,
    IOptions<IdentityOptions> identity) : ControllerBase
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

        await recovery.SendConfirmation(user, HttpContext.RequestAborted);
        await sessions.SignInAsync(user, isPersistent: false);

        return new CurrentUserDto(user.Id, user.Email!);
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<CurrentUserDto>> Login(LoginRequestDto request)
    {
        var user = await users.FindByEmailAsync(request.Email!);
        var blockedAlready = user is not null && await users.IsLockedOutAsync(user);

        var signedIn = await sessions.PasswordSignInAsync(
            request.Email!, request.Password!, isPersistent: false, lockoutOnFailure: true);

        if (!signedIn.Succeeded)
        {
            if (user is not null && signedIn.IsLockedOut && !blockedAlready)
            {
                await recovery.WarnAboutAttempts(
                    user, identity.Value.Lockout.DefaultLockoutTimeSpan, HttpContext.RequestAborted);
            }

            return Unauthorized();
        }

        return new CurrentUserDto(user!.Id, user.Email!);
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("email")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequestDto request)
    {
        await recovery.SendReset(request.Email!, HttpContext.RequestAborted);

        return Accepted();
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("email")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequestDto request)
    {
        if (!await recovery.Reset(request.UserId!, request.Token!, request.Password!))
        {
            ModelState.AddModelError(
                nameof(request.Token),
                "This link no longer works. It expires after two hours and can only be used once. "
                + "Ask for a new one.");

            return ValidationProblem(ModelState);
        }

        return NoContent();
    }

    [HttpPost("confirm-email")]
    [EnableRateLimiting("email")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequestDto request)
    {
        if (!await recovery.Confirm(request.UserId!, request.Token!))
        {
            ModelState.AddModelError(
                nameof(request.Token),
                "This link no longer works. It expires after two hours. Ask for a new one by "
                + "resetting your password.");

            return ValidationProblem(ModelState);
        }

        return NoContent();
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
