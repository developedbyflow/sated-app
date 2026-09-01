using Sated.Services;

namespace Sated.Api;

public static class EmailSenderRegistration
{
    private const string Section = "Email";

    public static IServiceCollection AddEmailSender(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSingleton(new RecoveryLinks(
            configuration.GetValue("App:BaseUrl", "http://localhost:3000")!.TrimEnd('/')));

        if (!string.IsNullOrWhiteSpace(configuration.GetSection(Section)["ApiKey"]))
        {
            throw new InvalidOperationException(
                "Email:ApiKey is set, but no provider has been written yet. Remove it, or finish "
                + "the second half of Story 2.4 before setting it.");
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "No email provider is configured. Password reset and confirmation cannot work "
                + "without one, and a sender that only writes to the log is not delivery. "
                + "Configure Email:ApiKey, or run this outside Production.");
        }

        return services.AddSingleton<IEmailSender, LoggingEmailSender>();
    }
}
