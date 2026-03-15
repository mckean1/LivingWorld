using System.Collections.ObjectModel;

namespace LivingWorld.Core;

public enum StartupDiagnosticReasonKind
{
    PhaseBReadiness,
    PhaseBDiagnostics,
    PhaseCReadiness,
    CandidateReadiness,
    Regeneration,
    Inferred
}

[Flags]
public enum GenerationZeroViableCause
{
    None = 0,
    NoDurableSocialWorld = 1,
    CandidateTruthFloorCollapse = 2,
    BiologyOrSocialReadinessNeverMaturedEnough = 4
}

public enum GenerationFailurePrimaryKind
{
    None,
    PhaseBBiologicalReadinessStall,
    PhaseCSocialEmergenceBottleneck,
    FinalCandidateViabilityCollapse,
    MixedOrInconclusive
}

public enum GenerationFailurePatternKind
{
    SingleAttempt,
    StablePattern,
    VariedPattern
}

public sealed record StartupDiagnosticReason(
    StartupDiagnosticReasonKind Kind,
    string Code);

public sealed record StartupDiagnosticReasonCount(
    StartupDiagnosticReasonKind Kind,
    string Code,
    int Count);

public sealed record GenerationAggregateReasonCount(
    StartupDiagnosticReasonKind Kind,
    string Code,
    int TotalCount,
    int AttemptCount);

public sealed record GenerationAttemptPopulationSnapshot(
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
    int EmergencyAdmittedCandidateCount);

public sealed record GenerationAttemptDiagnosticsSummary(
    int AttemptNumber,
    int WorldAgeYears,
    PrehistoryRuntimePhase FinalPhase,
    string FinalSubphase,
    PrehistoryCheckpointOutcomeKind Outcome,
    int TotalViableCandidatesDiscovered,
    int SurfacedCandidateCount,
    int NormalReadyCandidateCount,
    GenerationAttemptPopulationSnapshot Population,
    GenerationFailurePrimaryKind PrimaryFailureKind,
    GenerationZeroViableCause ZeroViableCause,
    string ReasonSummary,
    IReadOnlyList<StartupDiagnosticReasonCount> RankedBottlenecks,
    IReadOnlyList<StartupDiagnosticReasonCount> RankedCandidateRejections,
    IReadOnlyList<StartupDiagnosticReason> RegenerationReasons);

public sealed record GenerationFailurePostmortem(
    string ShortSummary,
    string HonestFailureStatement,
    GenerationFailurePrimaryKind PrimaryFailureKind,
    GenerationZeroViableCause ZeroViableCause,
    GenerationAttemptDiagnosticsSummary FinalAttempt,
    IReadOnlyList<GenerationAggregateReasonCount> RepeatedBottlenecks,
    IReadOnlyList<GenerationAggregateReasonCount> RepeatedCandidateRejections,
    GenerationFailurePatternKind FailurePattern,
    string FailurePatternSummary);

public sealed class WorldGenerationDiagnosticsState
{
    private readonly List<GenerationAttemptDiagnosticsSummary> _attemptHistory = [];

    public IReadOnlyList<GenerationAttemptDiagnosticsSummary> AttemptHistory
        => new ReadOnlyCollection<GenerationAttemptDiagnosticsSummary>(_attemptHistory);

    public GenerationFailurePostmortem? FinalFailurePostmortem { get; private set; }

    public void ReplaceAttemptHistory(IEnumerable<GenerationAttemptDiagnosticsSummary> summaries)
    {
        _attemptHistory.Clear();
        _attemptHistory.AddRange(summaries);
    }

    public void SetFinalFailurePostmortem(GenerationFailurePostmortem? postmortem)
        => FinalFailurePostmortem = postmortem;
}
