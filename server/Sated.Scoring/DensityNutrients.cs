namespace Sated.Scoring;

/// <summary>
/// One nutrient in the density formula: how much of it a food carries per 100 g, and the Daily
/// Value that turns that amount into a percentage. The amount is null when the food does not say.
/// </summary>
public sealed record DensityNutrient(Func<DensityInput, double?> AmountPer100g, double DailyValue);

/// <summary>
/// Which nutrients a density score counts (FR-26). A lens is not only three weights: the GLP-1
/// lens is defined by this list, because the treatment depletes vitamin D and thiamine and a
/// score blind to them cannot tell a user which foods replace what they are losing.
/// </summary>
/// <param name="Name">
/// The name calibration.json uses to ask for this set. It also keys the percentile scale: two
/// sets produce two raw distributions, and normalising one against the other's ranks would rank
/// every food against a formula it was never computed with.
/// </param>
public sealed record DensityNutrients(
    string Name,
    IReadOnlyList<DensityNutrient> Encouraged,
    IReadOnlyList<DensityNutrient> Limited
);
