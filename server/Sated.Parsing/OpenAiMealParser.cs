using System.ClientModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Sated.Parsing;

public class OpenAiMealParser(ChatClient client, ILogger<OpenAiMealParser> log) : IMealParser
{
    private const string Instructions =
        """
        You turn one sentence about a meal into foods from a fixed list.

        Every foodId must come from the list below. Never invent one, and never bend a close one
        into a match. Anything the list does not carry belongs in unrecognised, written in the
        words the person used.

        quantityGrams is grams. When the person said how much, convert it and set
        quantityEstimated to false. When they did not, estimate one ordinary portion and set
        quantityEstimated to true.

        Never return nutrition of any kind. You identify foods and amounts; everything else is
        computed elsewhere.

        One entry per food the sentence names, in the order the sentence names them.


        """;

    private static readonly ChatCompletionOptions Options = new()
    {
        ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            "parsed_meal",
            BinaryData.FromString(MealSchema.Strict()),
            "The foods and amounts one sentence names",
            jsonSchemaIsStrict: true),
        Temperature = 0
    };

    public async Task<ParsedMeal?> Parse(
        string text, string catalogue, CancellationToken cancellation)
    {
        try
        {
            var answer = await client.CompleteChatAsync(
                [
                    ChatMessage.CreateSystemMessage(Instructions + catalogue),
                    ChatMessage.CreateUserMessage(text)
                ],
                Options,
                cancellation);

            return Read(answer.Value);
        }
        catch (ClientResultException failure)
        {
            log.LogWarning(
                "The meal parser answered {Status}: {Message}", failure.Status, failure.Message);

            return null;
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            log.LogWarning("The meal parser ran out of time");

            return null;
        }
    }

    private ParsedMeal? Read(ChatCompletion completion)
    {
        Count(completion);

        if (!string.IsNullOrEmpty(completion.Refusal))
        {
            log.LogWarning("The meal parser refused: {Refusal}", completion.Refusal);

            return null;
        }

        if (completion.Content.Count == 0)
        {
            log.LogWarning("The meal parser answered {Reason} with nothing", completion.FinishReason);

            return null;
        }

        return JsonSerializer.Deserialize<ParsedMeal>(completion.Content[0].Text, MealSchema.Json);
    }

    private void Count(ChatCompletion completion) =>
        log.LogInformation(
            "Meal parsed by {Model}: {InputTokens} in, {CachedTokens} of them cached, "
            + "{OutputTokens} out",
            completion.Model,
            completion.Usage.InputTokenCount,
            completion.Usage.InputTokenDetails.CachedTokenCount,
            completion.Usage.OutputTokenCount);
}
