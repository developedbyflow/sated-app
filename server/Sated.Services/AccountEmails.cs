namespace Sated.Services;

public static class AccountEmails
{
    public static EmailMessage Confirm(string to, string link) => new(
        to,
        "Confirm your email address",
        $"""
        Somebody created a Sated account with this address.

        Confirm it here: {link}

        The link works for two hours. Your account already works without it — confirming an
        address is what lets you get back in if you ever forget your password.

        If this was not you, ignore this message. Nothing was sent to you before now, and
        nothing else will be.
        """);

    public static EmailMessage Reset(string to, string link) => new(
        to,
        "Reset your Sated password",
        $"""
        Somebody asked to reset the password on the Sated account at this address.

        Set a new one here: {link}

        The link works for two hours, and only once.

        If this was not you, nothing has happened. Your password has not changed and nobody has
        been let in. You can ignore this message.
        """);

    public static EmailMessage TooManyAttempts(string to, TimeSpan blockedFor) => new(
        to,
        "Somebody tried to sign in to your Sated account",
        $"""
        Five wrong passwords were tried in a row on the Sated account at this address, so signing
        in is blocked for the next {blockedFor.TotalMinutes:0} minutes.

        Nobody got in. This message is about attempts that failed.

        If it was you, wait it out and try again — or reset your password if you no longer
        remember it. If it was not you, somebody is guessing, and the password is worth changing
        once you can sign in.
        """);
}
