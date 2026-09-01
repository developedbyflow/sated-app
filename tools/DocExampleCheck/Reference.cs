using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DocExampleCheck;

public static partial class Reference
{
    public static Dictionary<string, List<JsonNode?>> Examples(string markdown)
    {
        var examples = new Dictionary<string, List<JsonNode?>>();

        foreach (var section in markdown.Split("\n## ").Skip(1))
        {
            var title = section[..section.IndexOf('\n')].Trim();
            var blocks = new List<JsonNode?>();

            foreach (Match block in JsonBlock().Matches(section))
            {
                blocks.Add(Parsed(block.Groups[1].Value));
            }

            if (blocks.Count > 0)
            {
                examples[title] = blocks;
            }
        }

        return examples;
    }

    private static JsonNode? Parsed(string block)
    {
        try
        {
            return JsonNode.Parse(block);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"```json\n(.*?)```", RegexOptions.Singleline)]
    private static partial Regex JsonBlock();
}
