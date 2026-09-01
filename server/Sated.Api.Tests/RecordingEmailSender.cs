using System.Collections.Concurrent;
using Sated.Services;

namespace Sated.Api.Tests;

public class RecordingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> sent = new();

    public Task Send(EmailMessage message, CancellationToken cancellation)
    {
        sent.Enqueue(message);

        return Task.CompletedTask;
    }

    public EmailMessage[] To(string address) =>
        [.. sent.Where(message => message.To == address)];

    public string TokenIn(EmailMessage message)
    {
        var link = message.Body.Split(' ', '\n')
            .First(word => word.Contains("token=", StringComparison.Ordinal));

        return Uri.UnescapeDataString(link[(link.IndexOf("token=", StringComparison.Ordinal) + 6)..]);
    }

    public string UserIdIn(EmailMessage message)
    {
        var link = message.Body.Split(' ', '\n')
            .First(word => word.Contains("userId=", StringComparison.Ordinal));

        var start = link.IndexOf("userId=", StringComparison.Ordinal) + 7;

        return Uri.UnescapeDataString(link[start..link.IndexOf('&', start)]);
    }
}
