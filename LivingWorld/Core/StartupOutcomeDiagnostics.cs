namespace LivingWorld.Core;

public sealed record StartupOutcomeDiagnostics(
    int OrganicSentientGroupCount,
    int FallbackSentientGroupCount,
    int OrganicSocietyCount,
    int FallbackSocietyCount,
    int OrganicSettlementCount,
    int FallbackSettlementCount,
    int OrganicPolityCount,
    int FallbackPolityCount,
    int OrganicFocalCandidateCount,
    int FallbackFocalCandidateCount,
    int OrganicPlayerEntryCandidateCount,
    int FallbackPlayerEntryCandidateCount,
    int EmergencyAdmittedCandidateCount,
    IReadOnlyDictionary<string, int> CandidateRejectionCounts,
    IReadOnlyList<string> BottleneckReasons,
    IReadOnlyList<string> RegenerationReasons)
{
    public static StartupOutcomeDiagnostics Empty { get; } = new(
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        Array.Empty<string>(),
        Array.Empty<string>());
}
