namespace LivingWorld.Core;

public sealed record PhaseAReadinessReport(
    int TotalRegions,
    int OccupiedRegions,
    double OccupiedRegionPercentage,
    int ProducerCoveredRegions,
    double ProducerCoverage,
    int ConsumerCoveredRegions,
    double ConsumerCoverage,
    int PredatorCoveredRegions,
    double PredatorCoverage,
    int BiodiversityCount,
    int StableRegionCount,
    int CollapsingRegionCount,
    bool IsReady,
    IReadOnlyList<string> FailureReasons)
{
    public static PhaseAReadinessReport Empty { get; } = new(
        0,
        0,
        0.0,
        0,
        0.0,
        0,
        0.0,
        0,
        0.0,
        0,
        0,
        0,
        false,
        []);
}
