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
