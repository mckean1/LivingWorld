using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Generation;

public sealed class LegacyCheckpointCompatibilityAdapter : ICheckpointEvaluationAdapter
{
    private readonly WorldGenerationSettings _settings;
    private readonly PlayerEntryCandidateGenerator _candidateGenerator;

    public LegacyCheckpointCompatibilityAdapter(WorldGenerationSettings settings)
    {
        _settings = settings;
        _candidateGenerator = new PlayerEntryCandidateGenerator(settings);
    }

    public void Evaluate(World world, bool allowEmergencyFallback, IReadOnlyList<string>? regenerationReasons)
    {
        IReadOnlyList<PlayerEntryCandidateSummary> candidates = _candidateGenerator.Generate(world, allowEmergencyFallback, out Dictionary<int, string> rejectionReasons);
        ApplyCandidates(world, candidates, rejectionReasons);
        world.WorldReadinessReport = WorldReadinessEvaluator.Evaluate(world, _settings);
        world.StartupOutcomeDiagnostics = StartupOutcomeDiagnosticsEvaluator.Evaluate(world, regenerationReasons);
        RefreshStartupDiagnostics(world, regenerationReasons);
        UpdateCandidatePoolSnapshot(world, allowEmergencyFallback);
    }

    private static void ApplyCandidates(
        World world,
        IReadOnlyList<PlayerEntryCandidateSummary> candidates,
        IReadOnlyDictionary<int, string> rejectionReasons)
    {
        world.PrehistoryEvaluation.PlayerEntryCandidates.Clear();
        world.PrehistoryEvaluation.PlayerEntryCandidates.AddRange(candidates);
        world.PrehistoryEvaluation.CandidateRejectionReasons.Clear();
        foreach ((int polityId, string reason) in rejectionReasons)
        {
            world.PrehistoryEvaluation.CandidateRejectionReasons[polityId] = reason;
        }
    }

    private static void UpdateCandidatePoolSnapshot(World world, bool emergencyFallback)
    {
        int total = world.PrehistoryEvaluation.PlayerEntryCandidates.Count;
        int fallback = world.PrehistoryEvaluation.PlayerEntryCandidates.Count(candidate => candidate.IsFallbackCandidate);
        int organic = total - fallback;
        string summary = emergencyFallback ? "Emergency fallback candidate pool" : "Organic candidate pool";
        world.PrehistoryEvaluation.CandidatePoolSnapshot = new PrehistoryCandidatePoolSnapshot(
            total,
            organic,
            fallback,
            emergencyFallback,
            $"{summary} at year {world.Time.Year}");
        world.PrehistoryEvaluation.LatestObserverSnapshot = new(world.Time.Year, $"Candidate pool {total} entries", new[] { summary });
    }

    private static void RefreshStartupDiagnostics(World world, IReadOnlyList<string>? regenerationReasons)
    {
        StartupOutcomeDiagnostics diagnostics = world.StartupOutcomeDiagnostics;
        world.StartupDiagnostics.Clear();
        world.StartupDiagnostics.Add($"startup_attempt:{world.StartupGenerationAttempt}");
        world.StartupDiagnostics.Add(
            $"organic_counts:groups={diagnostics.OrganicSentientGroupCount},societies={diagnostics.OrganicSocietyCount},settlements={diagnostics.OrganicSettlementCount},polities={diagnostics.OrganicPolityCount},focal_candidates={diagnostics.OrganicFocalCandidateCount},entry_candidates={diagnostics.OrganicPlayerEntryCandidateCount}");
        world.StartupDiagnostics.Add(
            $"fallback_counts:groups={diagnostics.FallbackSentientGroupCount},societies={diagnostics.FallbackSocietyCount},settlements={diagnostics.FallbackSettlementCount},polities={diagnostics.FallbackPolityCount},focal_candidates={diagnostics.FallbackFocalCandidateCount},entry_candidates={diagnostics.FallbackPlayerEntryCandidateCount},emergency={diagnostics.EmergencyAdmittedCandidateCount}");
        world.StartupDiagnostics.Add(
            $"phase_b_diagnostics:avg_depth={world.PhaseBDiagnostics.AverageAncestryDepth:F2},branching={world.PhaseBDiagnostics.BranchingLineageCount},deep={world.PhaseBDiagnostics.DeepLineageCount},diverged={world.PhaseBDiagnostics.MatureDivergenceLineageCount},adapted_biomes={world.PhaseBDiagnostics.AdaptedBiomeSpan},local_ext={world.PhaseBDiagnostics.LocalExtinctionEventCount},recolonized={world.PhaseBDiagnostics.RecolonizationEventCount},sentient_roots={world.PhaseBDiagnostics.SentienceCapableRootBranchCount}");

        foreach (string weakness in world.PhaseBDiagnostics.WeaknessReasons)
        {
            world.StartupDiagnostics.Add($"phase_b_weakness:{weakness}");
        }

        foreach ((string reason, int count) in diagnostics.CandidateRejectionCounts
                     .OrderByDescending(entry => entry.Value)
                     .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            world.StartupDiagnostics.Add($"candidate_rejection:{reason}:{count}");
        }

        foreach (string reason in diagnostics.BottleneckReasons)
        {
            world.StartupDiagnostics.Add($"bottleneck:{reason}");
        }

        foreach (string reason in regenerationReasons ?? Array.Empty<string>())
        {
            world.StartupDiagnostics.Add($"regeneration_reason:{reason}");
        }
    }
}
