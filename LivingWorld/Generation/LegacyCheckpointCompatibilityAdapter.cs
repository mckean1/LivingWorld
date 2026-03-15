using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Generation;

/// <summary>
/// Transitional bridge that lets the PR-1 checkpoint coordinator consume the legacy readiness and
/// candidate-generation stack as a pure evaluation result, without letting that stack drive runtime flow.
/// </summary>
public sealed class LegacyCheckpointCompatibilityAdapter : ICheckpointEvaluationAdapter
{
    private readonly WorldGenerationSettings _settings;
    private readonly PlayerEntryCandidateGenerator _candidateGenerator;
    private readonly PrehistoryObserverService _observerService;

    public LegacyCheckpointCompatibilityAdapter(WorldGenerationSettings settings)
    {
        _settings = settings;
        _candidateGenerator = new PlayerEntryCandidateGenerator(settings);
        _observerService = new PrehistoryObserverService();
    }

    public PrehistoryCheckpointEvaluationResult Evaluate(World world, bool allowEmergencyFallback, IReadOnlyList<string>? regenerationReasons)
    {
        PrehistoryObserverSnapshot observerSnapshot = _observerService.Observe(world);
        IReadOnlyList<PlayerEntryCandidateSummary> candidates = _candidateGenerator.Generate(world, allowEmergencyFallback, out Dictionary<int, string> rejectionReasons);
        WorldReadinessReport readinessReport = WorldReadinessEvaluator.Evaluate(world, _settings);
        StartupOutcomeDiagnostics diagnostics = StartupOutcomeDiagnosticsEvaluator.Evaluate(
            world,
            candidates: candidates,
            candidateRejectionReasons: rejectionReasons,
            worldReadinessReport: readinessReport,
            regenerationReasons: regenerationReasons);
        int total = candidates.Count;
        int fallback = candidates.Count(candidate => candidate.IsFallbackCandidate);
        int organic = total - fallback;
        string summary = allowEmergencyFallback ? "Emergency fallback candidate pool" : "Organic candidate pool";
        return new PrehistoryCheckpointEvaluationResult
        {
            WorldReadinessReport = readinessReport,
            StartupOutcomeDiagnostics = diagnostics,
            StartupDiagnostics = LegacyStartupDiagnosticsBuilder.Build(world, diagnostics, regenerationReasons),
            PlayerEntryCandidates = candidates,
            CandidateRejectionReasons = rejectionReasons,
            CandidatePoolSnapshot = new PrehistoryCandidatePoolSnapshot(
                total,
                organic,
                fallback,
                allowEmergencyFallback,
                $"{summary} at year {world.Time.Year}"),
            LatestObserverSnapshot = observerSnapshot
        };
    }
}
