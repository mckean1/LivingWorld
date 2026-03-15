using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Generation;

public sealed class PrehistoryCheckpointEvaluationAdapter : ICheckpointEvaluationAdapter
{
    private readonly PlayerEntryCandidateGenerator _candidateGenerator;
    private readonly PrehistoryObserverService _observerService;
    private readonly PrehistoryReadinessEvaluator _readinessEvaluator;

    public PrehistoryCheckpointEvaluationAdapter(WorldGenerationSettings settings)
    {
        _candidateGenerator = new PlayerEntryCandidateGenerator(settings);
        _observerService = new PrehistoryObserverService();
        _readinessEvaluator = new PrehistoryReadinessEvaluator(settings);
    }

    public PrehistoryCheckpointEvaluationResult Evaluate(World world, bool allowEmergencyFallback, IReadOnlyList<string>? regenerationReasons)
    {
        PrehistoryObserverSnapshot observerSnapshot = _observerService.Observe(world);
        IReadOnlyDictionary<int, CandidateReadinessEvaluation> candidateEvaluations = _readinessEvaluator.EvaluateCandidateReadiness(world, observerSnapshot);
        IReadOnlyList<PlayerEntryCandidateSummary> candidates = _candidateGenerator.Generate(world, candidateEvaluations, out Dictionary<int, string> rejectionReasons);
        PrehistoryReadinessEvaluation readiness = _readinessEvaluator.Evaluate(world, observerSnapshot, candidateEvaluations, candidates);
        StartupOutcomeDiagnostics diagnostics = StartupOutcomeDiagnosticsEvaluator.Evaluate(
            world,
            candidates,
            rejectionReasons,
            readiness.Report,
            regenerationReasons);

        return new PrehistoryCheckpointEvaluationResult
        {
            WorldReadinessReport = readiness.Report,
            StartupOutcomeDiagnostics = diagnostics,
            StartupDiagnostics = BuildDiagnostics(readiness.Report, regenerationReasons),
            PlayerEntryCandidates = candidates,
            CandidateRejectionReasons = rejectionReasons,
            CandidatePoolSnapshot = new PrehistoryCandidatePoolSnapshot(
                readiness.Report.CandidatePoolSummary.TotalSurfaceableCandidates,
                readiness.Report.CandidatePoolSummary.OrganicViableCandidateCount,
                readiness.Report.CandidatePoolSummary.FallbackViableCandidateCount,
                false,
                readiness.Report.CandidatePoolSummary.Summary),
            LatestObserverSnapshot = observerSnapshot
        };
    }

    private static IReadOnlyList<string> BuildDiagnostics(WorldReadinessReport report, IReadOnlyList<string>? regenerationReasons)
    {
        List<string> diagnostics =
        [
            $"checkpoint_resolution:{report.FinalCheckpointResolution}",
            $"age_gate:{report.AgeGate.Status}",
            $"viable_candidates:{report.CandidatePoolSummary.ViableCandidateCount}",
            $"weak_world:{report.IsWeakWorld}",
            $"thin_world:{report.IsThinWorld}"
        ];
        diagnostics.AddRange(report.GlobalBlockingReasons.Select(reason => $"blocker:{reason}"));
        diagnostics.AddRange(report.GlobalWarningReasons.Take(4).Select(reason => $"warning:{reason}"));
        if (regenerationReasons is not null)
        {
            diagnostics.AddRange(regenerationReasons.Select(reason => $"regen:{reason}"));
        }

        return diagnostics;
    }
}
