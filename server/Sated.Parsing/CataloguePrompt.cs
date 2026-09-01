using System.Text;

namespace Sated.Parsing;

public static class CataloguePrompt
{
    private const string Shared = "CATALOGUE";

    private const string Mine = "FOODS THIS PERSON ADDED";

    public static string Of(
        IEnumerable<CatalogueEntry> catalogue, IEnumerable<CatalogueEntry> mine)
    {
        var prompt = new StringBuilder();

        Write(prompt, Shared, catalogue);
        Write(prompt, Mine, mine);

        return prompt.ToString();
    }

    private static void Write(
        StringBuilder prompt, string heading, IEnumerable<CatalogueEntry> entries)
    {
        var ordered = entries.OrderBy(entry => entry.Id).ToArray();

        if (ordered.Length == 0)
        {
            return;
        }

        prompt.Append(heading).Append('\n');

        foreach (var entry in ordered)
        {
            prompt.Append(entry.Id).Append(' ').Append(entry.Description).Append('\n');
        }
    }
}
