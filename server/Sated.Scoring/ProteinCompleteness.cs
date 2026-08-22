namespace Sated.Scoring;

/// <summary>
/// Estimates a food's leucine from its protein and the protein class of its category (FR-6).
/// The catalogue carries no amino acid data, so the class stands in for a measurement.
/// </summary>
public static class ProteinCompleteness
{
    // Leucine as a share of protein: animal 8.8% ± 0.7, plant 7.1% ± 0.8.
    // Gorissen et al., Amino Acids 2018 — https://www.ncbi.nlm.nih.gov/pmc/articles/PMC6245118/
    // The two shares differ by 24%, while protein content differs between foods by an order of
    // magnitude, so the class barely moves a grade. What moves it is the component existing at
    // all: on FNDDS every food is missing leucine, so without this the third axis is empty and
    // the lenses land on the same letter for 87.6% of the catalogue.

    private const double AnimalLeucineShare = 0.088;
    private const double PlantLeucineShare = 0.071;

    // Complete is the default and this is the exception list (P29). WWEIA categories name
    // dishes, not ingredients: a whitelist of complete-protein categories classified only 54.6%
    // of protein-rich foods, and the 45.4% it missed were pizza, burgers, deli sandwiches and
    // meat dishes — foods whose protein is complete. Inverting the default covers the whole
    // catalogue instead of denying the axis to anything that arrives cooked.
    // Every name here was checked against FNDDS: a typo would disable a rule in silence.

    private static readonly HashSet<string> PlantProteinCategories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Bean, pea, legume dishes",
            "Beans, peas, legumes",
            "Nuts and seeds",
            "Peanut butter and jelly sandwiches",
            "Yeast breads",
            "Rolls and buns",
            "Bagels and English muffins",
            "Biscuits, muffins, quick breads",
            "Tortillas",
            "Rice",
            "Pasta, noodles, cooked grains",
            "Oatmeal",
            "Grits and other cooked cereals",
            "Ready-to-eat cereal, higher sugar (>21.2g/100g)",
            "Ready-to-eat cereal, lower sugar (=<21.2g/100g)",
            "Crackers, excludes saltines",
            "Saltine crackers",
            "Popcorn",
            "Pretzels/snack mix",
            "Corn",
            "Plant-based milk",
            "Plant-based yogurt",
            "Soy and meat-alternative products",
            "Vegetable dishes",
            "Vegetable sandwiches/burgers"
        };

    public static bool IsPlantProtein(string category) =>
        PlantProteinCategories.Contains(category);

    /// <returns>Grams of leucine per 100 g of food, estimated rather than measured.</returns>
    public static double EstimateLeucinePer100g(double proteinPer100g, string category)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(proteinPer100g);

        return proteinPer100g *
            (IsPlantProtein(category) ? PlantLeucineShare : AnimalLeucineShare);
    }
}
