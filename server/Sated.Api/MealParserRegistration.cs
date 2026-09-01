using System.ClientModel;
using OpenAI;
using OpenAI.Chat;
using Sated.Parsing;

namespace Sated.Api;

public static class MealParserRegistration
{
    private const string Section = "OpenAi";

    public static IServiceCollection AddMealParser(
        this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(Section);
        var apiKey = settings["ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return services.AddSingleton<IMealParser, NotConfiguredMealParser>();
        }

        var client = new ChatClient(
            settings["Model"] ?? "gpt-5.6-luna",
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                NetworkTimeout = TimeSpan.FromSeconds(settings.GetValue("TimeoutSeconds", 20))
            });

        return services
            .AddSingleton(client)
            .AddSingleton<IMealParser, OpenAiMealParser>();
    }
}
