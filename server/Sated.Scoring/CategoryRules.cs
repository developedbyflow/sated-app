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
    // Which categories get a rule, and on which component, is read from calibration.json
    // (Story 1.12) — the file carries the reasoning with the pairings. The one table that stays
    // here is the empty one: "no rules" is not a calibrated value.

    /// <summary>No rules at all: every food takes the general formula.</summary>
    public static CategoryRules None { get; } = new([]);

    private readonly Dictionary<(string Category, string Lens, ScoreComponent Component),
        ComponentStrategy> _byKey = [];

    private readonly CategoryRule[] _rules;
    private readonly IReadOnlySet<string>? _catalogue;

    public CategoryRules(
        IEnumerable<CategoryRule> rules, IReadOnlySet<string>? catalogueCategories = null)
    {
        _rules = [.. rules];
        _catalogue = catalogueCategories;

        foreach (var rule in _rules)
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

    /// <summary>
    /// Every pairing the table holds, so a caller can report which ones a run exercised. A rule
    /// no food matched is not an error: the category may simply be absent from that food set.
    /// </summary>
    public IReadOnlyList<CategoryRule> All => _rules;

    /// <returns>
    /// True when this category carries any rule at all under this lens, on any component. Not the
    /// question Find answers: olive oil's rule sits on satiety, and what exempts it from the
    /// density floor is that somebody judged the category, not which component was replaced.
    /// </returns>
    public bool Has(string? category, Lens lens) =>
        category is not null && _rules.Any(rule =>
            rule.Category.Equals(category, StringComparison.OrdinalIgnoreCase) &&
            rule.LensName.Equals(lens.Name, StringComparison.OrdinalIgnoreCase));

    /// <returns>
    /// True when this category belongs to the catalogue these rules were written against. False
    /// means the food came from somewhere else, and then the absence of a rule says nothing about
    /// it: measured, olive oil carrying a European catalogue's category name falls back to E and a
    /// cola reads C, which is the whole of the damage P68 was meant to close.
    /// A table built without a catalogue cannot tell, and says yes rather than guessing.
    /// </returns>
    public bool Recognises(string category) =>
        _catalogue is null || _catalogue.Contains(category);

    /// <returns>The strategy registered for this pairing, or null to use the general formula.</returns>
    public ComponentStrategy? Find(string? category, Lens lens, ScoreComponent component) =>
        category is null
            ? null
            : _byKey.GetValueOrDefault(Key(category, lens.Name, component));

    // Categories are matched the same way ProteinCompleteness matches them: a catalogue that
    // comes back with different capitalisation must not silently stop matching its own rules.
    private static (string, string, ScoreComponent) Key(
        string category, string lensName, ScoreComponent component) =>
        (category.ToLowerInvariant(), lensName.ToLowerInvariant(), component);
}
