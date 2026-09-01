using System.Text.Json;
using System.Text.Json.Nodes;
using Sated.Parsing;

namespace Sated.Parsing.Tests;

public class MealSchemaTests
{
    private static readonly JsonObject Schema =
        (JsonObject)JsonNode.Parse(MealSchema.Strict())!;

    [Fact]
    public void Strict_EveryObjectInTheSchema_ForbidsFieldsNobodyAskedFor()
    {
        Assert.All(Objects(Schema), node => Assert.False((bool)node["additionalProperties"]!));
    }

    [Fact]
    public void Strict_EveryObjectInTheSchema_RequiresEveryFieldItDeclares()
    {
        Assert.All(Objects(Schema), node => Assert.Equal(
            ((JsonObject)node["properties"]!).Select(property => property.Key),
            ((JsonArray)node["required"]!).Select(name => name!.GetValue<string>())));
    }

    [Fact]
    public void Strict_TheAnswerItself_IsAnObjectAndNeverNull()
    {
        Assert.Equal("object", Schema["type"]!.GetValue<string>());
    }

    [Fact]
    public void Strict_AFoodTheParserCouldNotName_IsANullableIntegerRatherThanAMissingField()
    {
        Assert.Equal(
            ["integer", "null"],
            Types(Item()["foodId"]!));
    }

    [Fact]
    public void Strict_AQuantity_IsANumberAndNotAlsoAString()
    {
        Assert.Equal(["number"], Types(Item()["quantityGrams"]!));
    }

    [Fact]
    public void Strict_TheWordsThatMatchedNothing_AreRequiredEvenWhenEmpty()
    {
        Assert.Contains(
            "unrecognised",
            ((JsonArray)Schema["required"]!).Select(name => name!.GetValue<string>()));
    }

    private static JsonObject Item() =>
        (JsonObject)Schema["properties"]!["items"]!["items"]!["properties"]!;

    private static string[] Types(JsonNode node) =>
        node["type"] is JsonArray many
            ? [.. many.Select(type => type!.GetValue<string>())]
            : [node["type"]!.GetValue<string>()];

    private static IEnumerable<JsonObject> Objects(JsonNode node)
    {
        if (node is JsonObject entry)
        {
            if (entry["properties"] is JsonObject)
            {
                yield return entry;
            }

            foreach (var child in entry)
            {
                foreach (var found in Objects(child.Value ?? new JsonObject()))
                {
                    yield return found;
                }
            }
        }
    }
}
