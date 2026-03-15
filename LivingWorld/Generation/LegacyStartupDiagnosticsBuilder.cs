using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;

namespace LivingWorld.Generation;

internal static class LegacyStartupDiagnosticsBuilder
{
    public static IReadOnlyList<string> Build(World world, StartupOutcomeDiagnostics diagnostics, IReadOnlyList<string>? regenerationReasons)
    {
        List<string> lines = [];
        lines.Add($"startup_attempt:{world.StartupGenerationAttempt}");
        lines.Add(
            $"organic_counts:groups={diagnostics.OrganicSentientGroupCount},societies={diagnostics.OrganicSocietyCount},settlements={diagnostics.OrganicSettlementCount},polities={diagnostics.OrganicPolityCount},focal_candidates={diagnostics.OrganicFocalCandidateCount},entry_candidates={diagnostics.OrganicPlayerEntryCandidateCount}");
        lines.Add(
            $"fallback_counts:groups={diagnostics.FallbackSentientGroupCount},societies={diagnostics.FallbackSocietyCount},settlements={diagnostics.FallbackSettlementCount},polities={diagnostics.FallbackPolityCount},focal_candidates={diagnostics.FallbackFocalCandidateCount},entry_candidates={diagnostics.FallbackPlayerEntryCandidateCount},emergency={diagnostics.EmergencyAdmittedCandidateCount}");
        lines.Add(
            $"phase_b_diagnostics:avg_depth={world.PhaseBDiagnostics.AverageAncestryDepth:F2},branching={world.PhaseBDiagnostics.BranchingLineageCount},deep={world.PhaseBDiagnostics.DeepLineageCount},diverged={world.PhaseBDiagnostics.MatureDivergenceLineageCount},adapted_biomes={world.PhaseBDiagnostics.AdaptedBiomeSpan},local_ext={world.PhaseBDiagnostics.LocalExtinctionEventCount},recolonized={world.PhaseBDiagnostics.RecolonizationEventCount},sentient_roots={world.PhaseBDiagnostics.SentienceCapableRootBranchCount}");

        foreach (string weakness in world.PhaseBDiagnostics.WeaknessReasons)
        {
            lines.Add($"phase_b_weakness:{weakness}");
        }

        foreach ((string reason, int count) in diagnostics.CandidateRejectionCounts
                     .OrderByDescending(entry => entry.Value)
                     .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"candidate_rejection:{reason}:{count}");
        }

        foreach (string reason in diagnostics.BottleneckReasons)
        {
            lines.Add($"bottleneck:{reason}");
        }

        foreach (string reason in regenerationReasons ?? Array.Empty<string>())
        {
            lines.Add($"regeneration_reason:{reason}");
        }

        return lines;
    }
}
