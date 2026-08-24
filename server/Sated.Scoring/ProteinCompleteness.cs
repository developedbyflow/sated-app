namespace Sated.Scoring;

/// <summary>
/// The leucine a food carries, for the case the catalogue could not supply it (FR-6).
/// </summary>
public static class ProteinCompleteness
{
    // Leucine as a share of protein, measured rather than borrowed: the median across the 2,286
    // FNDDS foods whose own recipes resolve into SR Legacy amino acid data, joined through the
    // ingredient codes each survey food carries (tools/LeucineJoinQuery).
    // It replaces an animal 8.8% / plant 7.1% split from Gorissen et al. 2018 and the twenty-five
    // category names that decided which half applied. Measured on this catalogue the two groups
    // sit 0.31 points apart — 7.59% against 7.28% — not the 1.7 the paper reports, and swapping
    // the split for this one number changes no letter in the benchmark's 138 rows.
    // A share, not an amount: it survives a food whose recipe only partly resolved. See P46.

    private const double LeucineShareOfProtein = 0.0752;

    /// <returns>Grams of leucine per 100 g of food, estimated rather than measured.</returns>
    public static double EstimateLeucinePer100g(double proteinPer100g)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(proteinPer100g);

        return proteinPer100g * LeucineShareOfProtein;
    }
}
