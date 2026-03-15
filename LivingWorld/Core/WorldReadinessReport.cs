using System.Collections.ObjectModel;

namespace LivingWorld.Core;

public enum ReadinessAssessmentStatus
{
    Pass,
    Warning,
    Blocker
}

public enum ReadinessCategoryStrictness
{
    Strict,
    Medium,
    Soft
}

public enum WorldReadinessCategoryKind
{
    BiologicalReadiness,
    SocialEmergenceReadiness,
    WorldStructureReadiness,
    CandidateReadiness,
    VarietyReadiness,
    AgencyReadiness
}

public enum PrehistoryAgeGateStatus
{
    BeforeMinimumAge,
    MinimumAgeReached,
    TargetAgeReached,
    MaximumAgeReached
}

public sealed record WorldAgeGateReport(
    int WorldAgeYears,
    int MinPrehistoryYears,
    int TargetPrehistoryYears,
    int MaxPrehistoryYears,
    PrehistoryAgeGateStatus Status)
{
    public bool MinimumAgeReached => WorldAgeYears >= MinPrehistoryYears;
    public bool TargetAgeReached => WorldAgeYears >= TargetPrehistoryYears;
    public bool MaximumAgeReached => WorldAgeYears >= MaxPrehistoryYears;
}

public sealed record WorldReadinessCategoryReport(
    WorldReadinessCategoryKind Category,
    ReadinessAssessmentStatus Status,
    ReadinessCategoryStrictness Strictness,
    string Summary,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings)
{
    public bool IsPass => Status == ReadinessAssessmentStatus.Pass;
    public bool IsBlocker => Status == ReadinessAssessmentStatus.Blocker;
}

public sealed record CandidatePoolReadinessSummary(
    int TotalSurfaceableCandidates,
    int ViableCandidateCount,
    int NormalReadyCandidateCount,
    int OrganicViableCandidateCount,
    int FallbackViableCandidateCount,
    int DistinctSpeciesCount,
    int DistinctLineageCount,
    int DistinctHomeRegionCount,
    int DistinctSubsistenceStyleCount,
    bool IsThinWorld,
    string Summary);

public sealed record WorldReadinessSummaryData(
    string Headline,
    string CandidateHeadline,
    string WorldConditionHeadline,
    int PassingCategoryCount,
    int WarningCategoryCount,
    int BlockingCategoryCount);

public sealed record WorldReadinessReport(
    WorldAgeGateReport AgeGate,
    PrehistoryCheckpointOutcomeKind FinalCheckpointResolution,
    IReadOnlyList<WorldReadinessCategoryReport> CategoryResults,
    CandidatePoolReadinessSummary CandidatePoolSummary,
    IReadOnlyList<string> GlobalBlockingReasons,
    IReadOnlyList<string> GlobalWarningReasons,
    bool IsWeakWorld,
    bool IsThinWorld,
    WorldReadinessSummaryData SummaryData)
{
    public bool IsReady => FinalCheckpointResolution is PrehistoryCheckpointOutcomeKind.EnterFocalSelection or PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection;
    public int WorldAgeYears => AgeGate.WorldAgeYears;
    public int ViableCandidateCount => CandidatePoolSummary.ViableCandidateCount;
    public IReadOnlyList<string> FailureReasons => GlobalBlockingReasons;

    public WorldReadinessCategoryReport GetCategory(WorldReadinessCategoryKind category)
        => CategoryResults.First(report => report.Category == category);

    public static WorldReadinessReport Empty { get; } = new(
        new WorldAgeGateReport(0, 0, 0, 0, PrehistoryAgeGateStatus.BeforeMinimumAge),
        PrehistoryCheckpointOutcomeKind.ContinuePrehistory,
        [
            new(WorldReadinessCategoryKind.BiologicalReadiness, ReadinessAssessmentStatus.Warning, ReadinessCategoryStrictness.Medium, "No biological readiness data.", Array.Empty<string>(), ["not_evaluated"]),
            new(WorldReadinessCategoryKind.SocialEmergenceReadiness, ReadinessAssessmentStatus.Warning, ReadinessCategoryStrictness.Strict, "No social readiness data.", Array.Empty<string>(), ["not_evaluated"]),
            new(WorldReadinessCategoryKind.WorldStructureReadiness, ReadinessAssessmentStatus.Warning, ReadinessCategoryStrictness.Strict, "No world-structure readiness data.", Array.Empty<string>(), ["not_evaluated"]),
            new(WorldReadinessCategoryKind.CandidateReadiness, ReadinessAssessmentStatus.Warning, ReadinessCategoryStrictness.Strict, "No candidate readiness data.", Array.Empty<string>(), ["not_evaluated"]),
            new(WorldReadinessCategoryKind.VarietyReadiness, ReadinessAssessmentStatus.Warning, ReadinessCategoryStrictness.Soft, "No variety readiness data.", Array.Empty<string>(), ["not_evaluated"]),
            new(WorldReadinessCategoryKind.AgencyReadiness, ReadinessAssessmentStatus.Warning, ReadinessCategoryStrictness.Soft, "No agency readiness data.", Array.Empty<string>(), ["not_evaluated"])
        ],
        new CandidatePoolReadinessSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, true, "No candidate pool."),
        Array.Empty<string>(),
        Array.Empty<string>(),
        false,
        true,
        new WorldReadinessSummaryData("No readiness data.", "0 viable starts", "Prehistory has not been evaluated yet.", 0, 0, 0));

    public static IReadOnlyList<string> Freeze(params string[] values)
        => new ReadOnlyCollection<string>(values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray());
}
