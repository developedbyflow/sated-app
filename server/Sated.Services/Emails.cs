using Microsoft.Extensions.Logging;

namespace Sated.Services;

public record EmailMessage(string To, string Subject, string Body);

public interface IEmailSender
{
    Task Send(EmailMessage message, CancellationToken cancellation);
}

public class LoggingEmailSender(ILogger<LoggingEmailSender> log) : IEmailSender
{
    public Task Send(EmailMessage message, CancellationToken cancellation)
    {
        log.LogInformation(
            "No email provider is configured, so this was not sent.\nTo: {To}\n{Subject}\n\n{Body}",
            message.To, message.Subject, message.Body);

        return Task.CompletedTask;
    }
}
