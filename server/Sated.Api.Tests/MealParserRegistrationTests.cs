using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;
using Sated.Parsing;

#pragma warning disable OPENAI001

namespace Sated.Api.Tests;

public class MealParserRegistrationTests
{
    [Fact]
    public void AddMealParser_WithNoKeyAnywhere_RegistersTheParserThatAnswersNothing()
    {
        Assert.IsType<NotConfiguredMealParser>(Built([]).GetRequiredService<IMealParser>());
    }

    [Fact]
    public void AddMealParser_WithABlankKey_IsTheSameAsNoKeyAtAll()
    {
        var built = Built(new() { ["OpenAi:ApiKey"] = "   " });

        Assert.IsType<NotConfiguredMealParser>(built.GetRequiredService<IMealParser>());
    }

    [Fact]
    public void AddMealParser_WithAKey_RegistersTheParserThatCallsOpenAi()
    {
        var built = Built(new() { ["OpenAi:ApiKey"] = "sk-a-key-that-is-never-sent" });

        Assert.IsType<OpenAiMealParser>(built.GetRequiredService<IMealParser>());
    }

    [Fact]
    public void AddMealParser_WithoutNamingAModel_UsesTheOneTheArchitectureChose()
    {
        var built = Built(new() { ["OpenAi:ApiKey"] = "sk-a-key-that-is-never-sent" });

        Assert.Equal("gpt-5.6-luna", built.GetRequiredService<ChatClient>().Model);
    }

    [Fact]
    public void AddMealParser_NamingAModel_UsesThatOne()
    {
        var built = Built(new()
        {
            ["OpenAi:ApiKey"] = "sk-a-key-that-is-never-sent",
            ["OpenAi:Model"] = "gpt-5.6-mini"
        });

        Assert.Equal("gpt-5.6-mini", built.GetRequiredService<ChatClient>().Model);
    }

    private static ServiceProvider Built(Dictionary<string, string?> settings) =>
        new ServiceCollection()
            .AddLogging()
            .AddMealParser(new ConfigurationBuilder().AddInMemoryCollection(settings).Build())
            .BuildServiceProvider();
}
