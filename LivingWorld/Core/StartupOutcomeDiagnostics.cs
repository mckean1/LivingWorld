using System.Collections.Generic;
using System.Linq;

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
    IReadOnlyList<StartupDiagnosticReasonCount> CandidateRejections,
    IReadOnlyList<StartupDiagnosticReasonCount> Bottlenecks,
    IReadOnlyList<StartupDiagnosticReason> RegenerationReasons,
    GenerationFailurePrimaryKind PrimaryFailureKind,
    GenerationZeroViableCause ZeroViableCause,
    string ReasonSummary)
{
    public IReadOnlyDictionary<string, int> CandidateRejectionCounts { get; } = CandidateRejections
        .ToDictionary(entry => entry.Code, entry => entry.Count, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> BottleneckReasons { get; } = Bottlenecks
        .Select(entry => entry.Code)
        .ToArray();

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
        Array.Empty<StartupDiagnosticReasonCount>(),
        Array.Empty<StartupDiagnosticReasonCount>(),
        Array.Empty<StartupDiagnosticReason>(),
        GenerationFailurePrimaryKind.None,
        GenerationZeroViableCause.None,
        "No startup diagnostics.");
}
