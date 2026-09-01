using Sated.Parsing;

namespace Sated.Parsing.Tests;

public class SlugTests
{
    [Fact]
    public void From_ADescriptionWithRunsOfPunctuation_CollapsesEachRunIntoOneHyphen()
    {
        var slug = Slug.From("Chicken thigh, fried, coated, skin / coating not eaten");

        Assert.Equal("chicken-thigh-fried-coated-skin-coating-not-eaten", slug);
    }

    [Fact]
    public void From_ADescriptionThatEndsInAPercentSign_EndsInTheNumberInstead()
    {
        var slug = Slug.From("Apple juice, 100%");

        Assert.Equal("apple-juice-100", slug);
    }

    [Fact]
    public void From_CapitalsAnywhereInTheDescription_ComeBackLowercase()
    {
        var slug = Slug.From("Rice, wild, 100%, cooked, NS as to fat");

        Assert.Equal("rice-wild-100-cooked-ns-as-to-fat", slug);
    }

    [Fact]
    public void From_AHyphenInsideAWord_StaysASingleHyphen()
    {
        var slug = Slug.From("Chicken thigh, baked, from pre-cooked");

        Assert.Equal("chicken-thigh-baked-from-pre-cooked", slug);
    }
}
