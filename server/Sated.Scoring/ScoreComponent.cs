namespace Sated.Scoring;

/// <summary>Which component of a grade a category rule replaces (FR-6).</summary>
public enum ScoreComponent
{
    Satiety,
    Density,
    ProteinQuality,

    /// <summary>
    /// How good the food's fat is. Not a replacement for satiety any more but a component of its
    /// own, computed for every food that carries fat. See <see cref="FatQuality"/>.
    /// </summary>
    FatQuality
}
