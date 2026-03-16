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
    bool SupportsNormalEntry);

public sealed record PrehistoryCandidateDiagnosticsSummary(
    IReadOnlyDictionary<string, int> RejectionCountsByReason,
    IReadOnlyDictionary<string, int> FailureCountsByDomain,
    IReadOnlyDictionary<string, int> SourcePathCounts)
{
    public static PrehistoryCandidateDiagnosticsSummary Empty { get; } = new(
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
}
