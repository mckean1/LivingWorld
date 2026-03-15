using LivingWorld.Core;
using LivingWorld.Presentation;
using LivingWorld.Societies;

namespace LivingWorld.Generation;

public sealed class PrehistoryCheckpointEvaluationAdapter : ICheckpointEvaluationAdapter
{
    private readonly PrehistoryCandidateSelectionEvaluator _candidateSelectionEvaluator;
    private readonly PrehistoryObserverService _observerService;
    private readonly PrehistoryReadinessEvaluator _readinessEvaluator;

    public PrehistoryCheckpointEvaluationAdapter(WorldGenerationSettings settings)
    {
        _candidateSelectionEvaluator = new PrehistoryCandidateSelectionEvaluator(settings);
        _observerService = new PrehistoryObserverService();
        _readinessEvaluator = new PrehistoryReadinessEvaluator(settings);
    }

    public PrehistoryCheckpointEvaluationResult Evaluate(World world, bool allowEmergencyFallback, IReadOnlyList<string>? regenerationReasons)
    {
        PrehistoryObserverSnapshot observerSnapshot = _observerService.Observe(world);
        IReadOnlyDictionary<int, CandidateReadinessEvaluation> candidateEvaluations = _readinessEvaluator.EvaluateCandidateReadiness(world, observerSnapshot);
        PrehistoryCandidateSelectionResult candidateSelection = _candidateSelectionEvaluator.Evaluate(world, observerSnapshot, candidateEvaluations);
        IReadOnlyList<PlayerEntryCandidateSummary> candidates = candidateSelection.Candidates;
        PrehistoryReadinessEvaluation readiness = _readinessEvaluator.Evaluate(world, observerSnapshot, candidateEvaluations, candidateSelection.AllViableCandidates, candidates);
        StartupOutcomeDiagnostics diagnostics = StartupOutcomeDiagnosticsEvaluator.Evaluate(
            world,
            candidates,
            candidateSelection.RejectionReasons,
            readiness.Report,
            regenerationReasons);

        return new PrehistoryCheckpointEvaluationResult
        {
            WorldReadinessReport = readiness.Report,
            StartupOutcomeDiagnostics = diagnostics,
            StartupDiagnostics = BuildDiagnostics(diagnostics, readiness.Report, regenerationReasons),
            PlayerEntryCandidates = candidates,
            CandidateRejectionReasons = candidateSelection.RejectionReasons,
            CandidatePoolSnapshot = new PrehistoryCandidatePoolSnapshot(
                readiness.Report.CandidatePoolSummary.TotalSurfacedCandidates,
                candidates.Count(candidate => !candidate.IsFallbackCandidate),
                candidates.Count(candidate => candidate.IsFallbackCandidate),
                readiness.Report.CandidatePoolSummary.TotalViableCandidatesDiscovered,
                false,
                readiness.Report.CandidatePoolSummary.Summary),
            LatestObserverSnapshot = observerSnapshot
        };
    }

    private static IReadOnlyList<string> BuildDiagnostics(
        StartupOutcomeDiagnostics diagnostics,
        WorldReadinessReport report,
        IReadOnlyList<string>? regenerationReasons)
    {
        List<string> summary = WorldGenerationDiagnosticsFormatter.BuildCheckpointDiagnostics(diagnostics, report).ToList();
        if (regenerationReasons is not null)
        {
            summary.AddRange(regenerationReasons.Select(reason => $"regen:{reason}"));
        }

        return summary;
    }
}
