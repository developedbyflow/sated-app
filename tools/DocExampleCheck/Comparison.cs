using System.Globalization;
using System.Text.Json.Nodes;

namespace DocExampleCheck;

public static class Comparison
{
    private static readonly string[] Volatile =
        [".id", ".email", ".exportedAt", ".givenAt", ".loggedAt", ".withdrawnAt", ".version"];

    public static Dictionary<string, JsonNode?> Leaves(JsonNode? node, string path = "")
    {
        var leaves = new Dictionary<string, JsonNode?>();

        switch (node)
        {
            case JsonObject entry:
                foreach (var (name, value) in entry)
                {
                    foreach (var (key, leaf) in Leaves(value, $"{path}.{name}"))
                    {
                        leaves[key] = leaf;
                    }
                }

                break;

            case JsonArray items:
                foreach (var (key, leaf) in Leaves(items.FirstOrDefault(), path + "[]"))
                {
                    leaves[key] = leaf;
                }

                break;

            default:
                leaves[path] = node;
                break;
        }

        return leaves;
    }

    public static IReadOnlyList<string> Differences(
        JsonNode? documented, JsonNode? actual, bool fragment)
    {
        var want = Leaves(documented);
        var got = Leaves(actual);
        var differences = new List<string>();

        foreach (var (key, value) in want)
        {
            if (!got.TryGetValue(key, out var mine))
            {
                differences.Add($"documented, absent from the response: {key}");
                continue;
            }

            if (!Volatile.Any(key.EndsWith) && !Same(value, mine))
            {
                differences.Add($"different value at {key}: doc={Short(value)} api={Short(mine)}");
            }
        }

        if (!fragment)
        {
            differences.AddRange(got.Keys
                .Where(key => !want.ContainsKey(key))
                .Select(key => $"in the response, absent from the documentation: {key}"));
        }

        return differences;
    }

    private static bool Same(JsonNode? documented, JsonNode? actual)
    {
        if (documented is null || actual is null)
        {
            return documented?.ToJsonString() == actual?.ToJsonString();
        }

        var doc = documented.ToJsonString();
        var mine = actual.ToJsonString();

        if (doc == mine)
        {
            return true;
        }

        if (double.TryParse(documented.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var wanted)
            && double.TryParse(actual.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var measured))
        {
            var decimals = Decimals(documented.ToString());

            return Math.Round(measured, decimals) == wanted;
        }

        return Truncated(documented.ToString(), actual.ToString());
    }

    private static int Decimals(string number) =>
        number.Contains('.') ? number.Length - number.IndexOf('.') - 1 : 0;

    private static bool Truncated(string documented, string actual)
    {
        var written = documented.TrimEnd('.', '…', ' ');

        if (written.Length == documented.Length || written.Length < 20)
        {
            return false;
        }

        return Flat(actual).StartsWith(Flat(written), StringComparison.Ordinal);
    }

    private static string Flat(string text) =>
        text.Replace("\\n", " ").Replace('\n', ' ').Replace("  ", " ");

    private static string Short(JsonNode? node)
    {
        var text = node?.ToJsonString() ?? "null";

        return text.Length <= 70 ? text : text[..70] + "…";
    }
}
