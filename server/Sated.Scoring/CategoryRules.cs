namespace Sated.Scoring;

/// <summary>One replacement: which component of which food, under which lens (FR-6).</summary>
public sealed record CategoryRule(
    string Category,
    string LensName,
    ScoreComponent Component,
    ComponentStrategy Strategy
);

/// <summary>
/// The dispatch table: (Category, Lens) → which strategy computes each component.
/// It holds pairings, never a calculation and never a grade — a rule changes how a component is
/// computed, so a category can never be handed its letter by decree.
/// </summary>
public sealed class CategoryRules
{
    // Architecture §Category Rules ca date puts this table in versioned JSON. It is in code for
    // now, alongside the lens weights, the percentile breakpoints and the letter thresholds:
    // moving one of the four out while the other three stay would make half the engine
    // recalibrable without a deploy and the other half not, which recalibrates nothing.
    // The acceptance criterion "an unknown strategy name crashes at startup" cannot be met
    // until then — in code, an unknown name is a compile error instead.

    /// <summary>No rules at all: every food takes the general formula.</summary>
    public static CategoryRules None { get; } = new([]);

    // The four WWEIA categories that are pure fat. "Salad dressings and vegetable oils" also
    // holds dressings, which the rule treats the same way and which is correct: a dressing is
    // oil plus sodium, and both are what the strategy reads.
    private static readonly string[] FatCategories =
        ["Salad dressings and vegetable oils", "Butter and animal fats", "Margarine", "Mayonnaise"];

    private const string NutCategory = "Nuts and seeds";

    /// <summary>The rules this catalogue needs today.</summary>
    // Two categories, two different axes, because the component that fails them is not the same.
    // Olive oil scores 0.0 on satiety — the Fullness Factor floor catches anything that is
    // almost entirely fat — while this rule was already handing it a density of 84.5, and it
    // still graded E: satiety carries 50% of the Weight Loss lens against density's 30%, so the
    // rule was replacing the component that was not the problem.
    // Nuts fail the other way round. Walnuts score 1.4 on satiety and 32.3 on density, because
    // NRF9.2 counts nutrients per calorie and cannot see that the calories are unsaturated fat.
    // Measured in 04_delivery/11.fat-rule-axis-report: olive oil 25.3 → 48.7 and walnuts
    // 30.4 → 48.1, with the top 30, the bottom 30 and all seven ordering pairs unmoved.
    // The cost is in the same report and is not small: a light Italian dressing falls 39.0 → 26.7,
    // because the category holds foods whose fat is a sixth of their calories and whose satiety
    // was real. The category is a proxy for "mostly fat", and report 8 measured that it is a
    // loose one.
    public static CategoryRules Standard { get; } = new([
        .. Replace(ScoreComponent.Satiety, FatCategories),
        .. Replace(ScoreComponent.Density, [NutCategory])]);

    private static IEnumerable<CategoryRule> Replace(
        ScoreComponent component, string[] categories) =>
        from category in categories
        from lens in new[] { Lens.WeightLoss, Lens.Fitness }
        select new CategoryRule(category, lens.Name, component, FatQuality.UnsaturatedShare);

    private readonly Dictionary<(string Category, string Lens, ScoreComponent Component),
        ComponentStrategy> _byKey = [];

    public CategoryRules(IEnumerable<CategoryRule> rules)
    {
        foreach (var rule in rules)
        {
            var key = Key(rule.Category, rule.LensName, rule.Component);

            // Two rules over the same component would leave the winner decided by file order.
            if (!_byKey.TryAdd(key, rule.Strategy))
            {
                throw new ArgumentException(
                    $"Two rules both claim {rule.Component} for {rule.Category} " +
                    $"under {rule.LensName}.",
                    nameof(rules));
            }
        }
    }

    /// <returns>The strategy registered for this pairing, or null to use the general formula.</returns>
    public ComponentStrategy? Find(string category, Lens lens, ScoreComponent component) =>
        _byKey.GetValueOrDefault(Key(category, lens.Name, component));

    // Categories are matched the same way ProteinCompleteness matches them: a catalogue that
    // comes back with different capitalisation must not silently stop matching its own rules.
    private static (string, string, ScoreComponent) Key(
        string category, string lensName, ScoreComponent component) =>
        (category.ToLowerInvariant(), lensName.ToLowerInvariant(), component);
}
