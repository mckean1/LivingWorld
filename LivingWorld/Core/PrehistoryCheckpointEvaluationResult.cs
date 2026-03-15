using LivingWorld.Societies;

namespace LivingWorld.Core;

public sealed class PrehistoryCheckpointEvaluationResult
{
    public static PrehistoryCheckpointEvaluationResult Empty { get; } = new();

    public WorldReadinessReport WorldReadinessReport { get; init; } = WorldReadinessReport.Empty;
    public StartupOutcomeDiagnostics StartupOutcomeDiagnostics { get; init; } = StartupOutcomeDiagnostics.Empty;
    public IReadOnlyList<string> StartupDiagnostics { get; init; } = Array.Empty<string>();
    public IReadOnlyList<PlayerEntryCandidateSummary> PlayerEntryCandidates { get; init; } = Array.Empty<PlayerEntryCandidateSummary>();
    public IReadOnlyDictionary<int, string> CandidateRejectionReasons { get; init; } = new Dictionary<int, string>();
    public PrehistoryCandidatePoolSnapshot? CandidatePoolSnapshot { get; init; }
    public PrehistoryObserverSnapshot? LatestObserverSnapshot { get; init; }
}
