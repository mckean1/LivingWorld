using LivingWorld.Generation;
using LivingWorld.Societies;

namespace LivingWorld.Core;

public sealed record PrehistoryCandidateDiagnostics(
    int PolityId,
    string PolityName,
    int SpeciesId,
    string SpeciesName,
    int LineageId,
    int HomeRegionId,
    string HomeRegionName,
    int? FounderSocietyId,
    string SourceIdentityPath,
    SocietyPersistenceState SocietyPersistenceState,
    CandidateSocialBackingType CandidateSocialBackingType,
    CandidateMaturityBand MaturityBand,
    SupportStabilityState SupportStability,
    DemographicViabilityState DemographicViability,
    PopulationTrendState PopulationTrend,
    MovementCoherenceState MovementCoherence,
    RootednessState Rootedness,
    ContinuityState Continuity,
    int PolityAgeYears,
    int SocietyAgeYears,
    int PeopleContinuityMonths,
    int MonthsSinceIdentityBreak,
    int IdentityBreakCountLast12Months,
    int IdentityBreakCountLast24Months,
    int SettlementPresentMonthsLast12Months,
    int EstablishedSettlementMonthsLast12Months,
    int AnchoredMonthsLast12Months,
    int StrongAnchoredMonthsLast12Months,
    double HomeClusterShareCurrent,
    double AverageHomeClusterShareLast12Months,
    double ConnectedFootprintShareCurrent,
    double RouteCoverageShareCurrent,
    double ScatterShareCurrent,
    bool SettlementDurabilityPasses,
    bool PoliticalDurabilityPasses,
    bool HasHardCurrentMonthVeto,
    IReadOnlyList<string> HardVetoReasons,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<string> WarningReasons,
    IReadOnlyList<string> FailedTruthFloors,
    bool IsViable,
    bool SupportsNormalEntry)
{
    public int SourcePeopleId { get; init; }
    public int SourcePolityId { get; init; }
    public int? SourceSocietyId { get; init; }
    public int HistoricalSocietyLineageAgeYears { get; init; }
    public bool HasActiveSocietySubstrate { get; init; }
    public bool HasHistoricalSocietyLineage { get; init; }
    public bool PolityBackedByActiveSociety { get; init; }
    public bool CandidateBackedByHistoricalLineageOnly { get; init; }
    public bool PolityOutlivingSocietySubstrate { get; init; }
    public bool PolityShellWithoutSocietySubstrate { get; init; }
    public string CandidateBackingSummary { get; init; } = string.Empty;
    public int CurrentFootprintRegionCount { get; init; }
    public int CurrentHomeClusterRegionId { get; init; }
    public double CurrentSupportAdequacy { get; init; }
    public double CurrentFootprintSupportRatio { get; init; }
    public bool CurrentMonthSupportPasses { get; init; }
    public bool CurrentMonthCoherent { get; init; }
    public bool CurrentMonthStrongCoherent { get; init; }
    public bool CurrentMonthScattered { get; init; }
    public bool CurrentMonthRooted { get; init; }
    public bool CurrentMonthDeeplyRooted { get; init; }
    public bool CurrentMonthCatastrophicScatterVeto { get; init; }
    public string SupportRuleResult { get; init; } = string.Empty;
    public string MovementRuleResult { get; init; } = string.Empty;
    public string RootednessRuleResult { get; init; } = string.Empty;
    public string ContinuityRuleResult { get; init; } = string.Empty;
    public CandidateRollingTruthSnapshot? Rollup6Months { get; init; }
    public CandidateRollingTruthSnapshot? Rollup12Months { get; init; }
    public CandidateRollingTruthSnapshot? Rollup24Months { get; init; }
    public IReadOnlyList<CandidateRuleTrace> BlockerTraces { get; init; } = Array.Empty<CandidateRuleTrace>();
    public bool FailedDueToCurrentRawState { get; init; }
    public bool FailedDueToRollingHistory { get; init; }
    public bool FailedDueToMixedTruthSources { get; init; }
    public bool TruthFloorPassed => !HasHardCurrentMonthVeto && FailedTruthFloors.Count == 0;
}

public sealed record CandidateRollingTruthSnapshot(
    int WindowMonths,
    int SupportedMonths,
    int SevereUnsupportedMonths,
    int CoherentMonths,
    int StrongCoherentMonths,
    int ScatteredMonths,
    int AnchoredMonths,
    int StrongAnchoredMonths,
    int EstablishedSettlementMonths,
    int DisplacementMonths,
    double AverageSupportAdequacy,
    double AverageHomeClusterShare,
    double AverageConnectedFootprintShare,
    double AverageRouteCoverageShare,
    double AverageScatterShare);

public sealed record CandidateRuleTrace(
    string Code,
    string Domain,
    string Source,
    string Detail);

public sealed record PrehistoryCandidateDiagnosticsSummary(
    IReadOnlyDictionary<string, int> RejectionCountsByReason,
    IReadOnlyDictionary<string, int> FailureCountsByDomain,
    IReadOnlyDictionary<string, int> SourcePathCounts)
{
    public int PassedTruthFloorButRejectedLaterCount { get; init; }
    public int FailedDueToCurrentMonthCount { get; init; }
    public int FailedDueToRollingHistoryCount { get; init; }
    public int FailedDueToMixedTruthSourcesCount { get; init; }

    public static PrehistoryCandidateDiagnosticsSummary Empty { get; } = new(
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
}
