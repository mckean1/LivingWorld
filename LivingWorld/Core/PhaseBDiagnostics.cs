namespace LivingWorld.Core;

public sealed record PhaseBDiagnostics(
    double AverageAncestryDepth,
    int BranchingLineageCount,
    int DeepLineageCount,
    int MatureDivergenceLineageCount,
    int AdaptedBiomeSpan,
    int LocalExtinctionEventCount,
    int GlobalExtinctionEventCount,
    int RecolonizationEventCount,
    int ReplacementLineageCount,
    int SentienceCapableRootBranchCount,
    IReadOnlyList<string> WeaknessReasons)
{
    public static PhaseBDiagnostics Empty { get; } = new(
        0.0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        Array.Empty<string>());
}
