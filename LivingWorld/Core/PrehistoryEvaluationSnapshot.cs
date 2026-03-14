using System;
using System.Collections.Generic;
using LivingWorld.Societies;

namespace LivingWorld.Core;

public sealed class PrehistoryEvaluationSnapshot
{
    public WorldReadinessReport WorldReadinessReport { get; set; } = WorldReadinessReport.Empty;
    public PhaseAReadinessReport PhaseAReadinessReport { get; set; } = PhaseAReadinessReport.Empty;
    public PhaseBReadinessReport PhaseBReadinessReport { get; set; } = PhaseBReadinessReport.Empty;
    public PhaseBDiagnostics PhaseBDiagnostics { get; set; } = PhaseBDiagnostics.Empty;
    public PhaseCReadinessReport PhaseCReadinessReport { get; set; } = PhaseCReadinessReport.Empty;
    public StartupOutcomeDiagnostics StartupOutcomeDiagnostics { get; set; } = StartupOutcomeDiagnostics.Empty;
    public PrehistoryCheckpointOutcome? LastCheckpointOutcome { get; set; }
    public PrehistoryObserverSnapshot? LatestObserverSnapshot { get; set; }
    public PrehistoryCandidatePoolSnapshot? CandidatePoolSnapshot { get; set; }
    public List<string> StartupDiagnostics { get; } = new();
    public List<PlayerEntryCandidateSummary> PlayerEntryCandidates { get; } = new();
    public Dictionary<int, string> CandidateRejectionReasons { get; } = new();
}

public sealed class PrehistoryObserverSnapshot
{
    public DateTime SnapshotTimeUtc { get; }
    public int WorldYear { get; }
    public string Summary { get; }
    public IReadOnlyList<string> Notes { get; }

    public PrehistoryObserverSnapshot(int worldYear, string summary, IReadOnlyList<string>? notes = null)
    {
        SnapshotTimeUtc = DateTime.UtcNow;
        WorldYear = worldYear;
        Summary = summary;
        Notes = notes ?? Array.Empty<string>();
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
