using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace Sated.Parsing;

public static class MealSchema
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private static readonly JsonSchemaExporterOptions Strictly = new()
    {
        TreatNullObliviousAsNonNullable = true,
        TransformSchemaNode = (_, schema) =>
        {
            if (schema is JsonObject node && node["properties"] is JsonObject)
            {
                node["additionalProperties"] = false;
            }

            return schema;
        }
    };

    public static string Strict() =>
        JsonSchemaExporter.GetJsonSchemaAsNode(Json, typeof(ParsedMeal), Strictly).ToJsonString();
}
