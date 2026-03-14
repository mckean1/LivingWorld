namespace LivingWorld.Core;

public sealed record PhaseBReadinessReport(
    bool IsReady,
    int MatureLineageCount,
    int SpeciationCount,
    int ExtinctLineageCount,
    int MaxAncestryDepth,
    int MatureRegionalDivergenceCount,
    int SentienceCapableLineageCount,
    int StableEcosystemRegionCount,
    IReadOnlyList<string> FailureReasons)
{
    public static PhaseBReadinessReport Empty { get; } = new(
        false,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        Array.Empty<string>());
}
