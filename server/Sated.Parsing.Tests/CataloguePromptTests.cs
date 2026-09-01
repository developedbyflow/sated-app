using Sated.Parsing;

namespace Sated.Parsing.Tests;

public class CataloguePromptTests
{
    private static readonly CatalogueEntry[] Catalogue =
    [
        new(5348, "Milk, whole"),
        new(5347, "Milk, NFS"),
        new(6435, "Watermelon, raw")
    ];

    private static readonly CatalogueEntry[] Mine = [new(9002, "Telemea de oaie")];

    [Fact]
    public void Of_AnyOrderComingOutOfTheDatabase_IsWrittenInIdOrder()
    {
        Assert.Equal(
            """
            CATALOGUE
            5347 Milk, NFS
            5348 Milk, whole
            6435 Watermelon, raw

            """.ReplaceLineEndings("\n"),
            CataloguePrompt.Of(Catalogue, []));
    }

    [Fact]
    public void Of_AUserWithFoodsOfTheirOwn_StartsWithExactlyThePromptOfAUserWithNone()
    {
        Assert.StartsWith(
            CataloguePrompt.Of(Catalogue, []),
            CataloguePrompt.Of(Catalogue, Mine),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Of_AUserWithFoodsOfTheirOwn_WritesThemUnderTheirOwnHeading()
    {
        Assert.EndsWith(
            """
            FOODS THIS PERSON ADDED
            9002 Telemea de oaie

            """.ReplaceLineEndings("\n"),
            CataloguePrompt.Of(Catalogue, Mine),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Of_AUserWithNoFoodsOfTheirOwn_WritesNoSecondHeading()
    {
        Assert.DoesNotContain("FOODS THIS PERSON ADDED", CataloguePrompt.Of(Catalogue, []));
    }
}
