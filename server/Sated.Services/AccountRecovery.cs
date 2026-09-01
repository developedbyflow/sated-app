using Microsoft.AspNetCore.Identity;
using Sated.Data.Entities;

namespace Sated.Services;

public record RecoveryLinks(string BaseUrl)
{
    public string Confirmation(string userId, string token) =>
        $"{BaseUrl}/confirm-email?userId={Uri.EscapeDataString(userId)}"
        + $"&token={Uri.EscapeDataString(token)}";

    public string Reset(string userId, string token) =>
        $"{BaseUrl}/reset-password?userId={Uri.EscapeDataString(userId)}"
        + $"&token={Uri.EscapeDataString(token)}";
}

public class AccountRecovery(
    UserManager<AppUser> users, IEmailSender email, RecoveryLinks links)
{
    public async Task SendConfirmation(AppUser user, CancellationToken cancellation)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);

        await email.Send(
            AccountEmails.Confirm(user.Email!, links.Confirmation(user.Id, token)), cancellation);
    }

    public async Task SendReset(string address, CancellationToken cancellation)
    {
        var user = await users.FindByEmailAsync(address);

        if (user is null)
        {
            return;
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);

        await email.Send(
            AccountEmails.Reset(user.Email!, links.Reset(user.Id, token)), cancellation);
    }

    public async Task WarnAboutAttempts(AppUser user, TimeSpan blockedFor, CancellationToken cancellation) =>
        await email.Send(AccountEmails.TooManyAttempts(user.Email!, blockedFor), cancellation);

    public async Task<bool> Reset(string userId, string token, string password)
    {
        var user = await users.FindByIdAsync(userId);

        if (user is null || !(await users.ResetPasswordAsync(user, token, password)).Succeeded)
        {
            return false;
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;

            await users.UpdateAsync(user);
        }

        return true;
    }

    public async Task<bool> Confirm(string userId, string token)
    {
        var user = await users.FindByIdAsync(userId);

        return user is not null && (await users.ConfirmEmailAsync(user, token)).Succeeded;
    }
}
