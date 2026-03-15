using System.Collections.Generic;
using LivingWorld.Societies;

namespace LivingWorld.Core;

public sealed class PrehistoryEvaluationSnapshot
{
    public PrehistoryLegacyEvaluationArtifacts LegacyCompatibility { get; } = new();
    public PrehistoryCandidateSelectionState CandidateSelection { get; } = new();
    public PrehistoryObserverSnapshot? LatestObserverSnapshot { get; set; }

    public WorldReadinessReport WorldReadinessReport
    {
        get => LegacyCompatibility.WorldReadinessReport;
        set => LegacyCompatibility.WorldReadinessReport = value;
    }

    public PhaseAReadinessReport PhaseAReadinessReport
    {
        get => LegacyCompatibility.PhaseAReadinessReport;
        set => LegacyCompatibility.PhaseAReadinessReport = value;
    }

    public PhaseBReadinessReport PhaseBReadinessReport
    {
        get => LegacyCompatibility.PhaseBReadinessReport;
        set => LegacyCompatibility.PhaseBReadinessReport = value;
    }

    public PhaseBDiagnostics PhaseBDiagnostics
    {
        get => LegacyCompatibility.PhaseBDiagnostics;
        set => LegacyCompatibility.PhaseBDiagnostics = value;
    }

    public PhaseCReadinessReport PhaseCReadinessReport
    {
        get => LegacyCompatibility.PhaseCReadinessReport;
        set => LegacyCompatibility.PhaseCReadinessReport = value;
    }

    public StartupOutcomeDiagnostics StartupOutcomeDiagnostics
    {
        get => LegacyCompatibility.StartupOutcomeDiagnostics;
        set => LegacyCompatibility.StartupOutcomeDiagnostics = value;
    }

    public List<string> StartupDiagnostics => LegacyCompatibility.StartupDiagnostics;
    public PrehistoryCandidatePoolSnapshot? CandidatePoolSnapshot => CandidateSelection.CandidatePoolSnapshot;
    public List<PlayerEntryCandidateSummary> PlayerEntryCandidates => CandidateSelection.PlayerEntryCandidates;
    public Dictionary<int, string> CandidateRejectionReasons => CandidateSelection.CandidateRejectionReasons;

    public void ApplyCheckpointEvaluation(PrehistoryCheckpointEvaluationResult result)
    {
        WorldReadinessReport = result.WorldReadinessReport;
        StartupOutcomeDiagnostics = result.StartupOutcomeDiagnostics;
        LegacyCompatibility.ReplaceStartupDiagnostics(result.StartupDiagnostics);
        CandidateSelection.Replace(result.PlayerEntryCandidates, result.CandidateRejectionReasons, result.CandidatePoolSnapshot);
        LatestObserverSnapshot = result.LatestObserverSnapshot;
    }
}

public sealed class PrehistoryLegacyEvaluationArtifacts
{
    public WorldReadinessReport WorldReadinessReport { get; set; } = WorldReadinessReport.Empty;
    public PhaseAReadinessReport PhaseAReadinessReport { get; set; } = PhaseAReadinessReport.Empty;
    public PhaseBReadinessReport PhaseBReadinessReport { get; set; } = PhaseBReadinessReport.Empty;
    public PhaseBDiagnostics PhaseBDiagnostics { get; set; } = PhaseBDiagnostics.Empty;
    public PhaseCReadinessReport PhaseCReadinessReport { get; set; } = PhaseCReadinessReport.Empty;
    public StartupOutcomeDiagnostics StartupOutcomeDiagnostics { get; set; } = StartupOutcomeDiagnostics.Empty;
    public List<string> StartupDiagnostics { get; } = new();

    public void ReplaceStartupDiagnostics(IEnumerable<string> diagnostics)
    {
        StartupDiagnostics.Clear();
        StartupDiagnostics.AddRange(diagnostics);
    }
}

public sealed class PrehistoryCandidateSelectionState
{
    public PrehistoryCandidatePoolSnapshot? CandidatePoolSnapshot { get; private set; }
    public List<PlayerEntryCandidateSummary> PlayerEntryCandidates { get; } = new();
    public Dictionary<int, string> CandidateRejectionReasons { get; } = new();

    public void Replace(
        IEnumerable<PlayerEntryCandidateSummary> candidates,
        IEnumerable<KeyValuePair<int, string>> rejectionReasons,
        PrehistoryCandidatePoolSnapshot? candidatePoolSnapshot)
    {
        PlayerEntryCandidates.Clear();
        PlayerEntryCandidates.AddRange(candidates);

        CandidateRejectionReasons.Clear();
        foreach ((int polityId, string reason) in rejectionReasons)
        {
            CandidateRejectionReasons[polityId] = reason;
        }

        CandidatePoolSnapshot = candidatePoolSnapshot;
    }

    public void Clear()
    {
        PlayerEntryCandidates.Clear();
        CandidateRejectionReasons.Clear();
        CandidatePoolSnapshot = null;
    }
}

public sealed record PrehistoryCandidatePoolSnapshot(
    int TotalCandidates,
    int OrganicCandidates,
    int FallbackCandidates,
    bool EmergencyFallbackUsed,
    string Summary)
{
    public bool HasFallbackCandidates => FallbackCandidates > 0;
}
