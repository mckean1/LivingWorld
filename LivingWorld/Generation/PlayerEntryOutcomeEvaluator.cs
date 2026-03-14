using LivingWorld.Core;

namespace LivingWorld.Generation;

public static class PlayerEntryOutcomeEvaluator
{
    public static bool ShouldSurfaceFocalSelection(World world, WorldGenerationSettings settings, out List<string> rejectionReasons)
    {
        rejectionReasons = [];
        int fallbackCandidateCount = world.PlayerEntryCandidates.Count(candidate => candidate.IsFallbackCandidate);
        int organicCandidateCount = world.PlayerEntryCandidates.Count(candidate => !candidate.IsFallbackCandidate);
        int healthyOrganicCandidateCount = world.PlayerEntryCandidates.Count(candidate =>
            !candidate.IsFallbackCandidate
            && candidate.RankScore >= settings.MinimumHealthyCandidateScore);
        bool biologyWeak = world.WorldReadinessReport.FailureReasons.Contains("biology_not_ready", StringComparer.OrdinalIgnoreCase)
            || world.WorldReadinessReport.FailureReasons.Contains("biology_floor_below_minimum", StringComparer.OrdinalIgnoreCase);
        bool weakMaxAgeOutcome = world.PrehistoryStopReason is PrehistoryStopReason.MaxAgeReached or PrehistoryStopReason.ForcedFallback;

        if (world.PhaseCReadinessReport.OrganicPolityCount == 0)
        {
            rejectionReasons.Add("no_organic_polities_available");
        }

        if (world.PhaseCReadinessReport.OrganicViableFocalCandidateCount == 0)
        {
            rejectionReasons.Add("no_organic_focal_candidates_available");
        }

        if (organicCandidateCount < settings.MinimumViablePlayerEntryCandidates)
        {
            rejectionReasons.Add($"candidate_pool_too_shallow:{organicCandidateCount}/{settings.MinimumViablePlayerEntryCandidates}");
        }

        if (healthyOrganicCandidateCount < settings.MinimumHealthyCandidateCount)
        {
            rejectionReasons.Add($"healthy_organic_candidate_count_below_floor:{healthyOrganicCandidateCount}/{settings.MinimumHealthyCandidateCount}");
        }

        if (world.PlayerEntryCandidates.Count == 1)
        {
            rejectionReasons.Add("single_candidate_world_rejected");
        }

        if (biologyWeak && weakMaxAgeOutcome)
        {
            rejectionReasons.Add("biology_floor_not_met");
        }

        if (weakMaxAgeOutcome && organicCandidateCount < settings.MinimumViablePlayerEntryCandidates)
        {
            rejectionReasons.Add("max_age_stop_without_organic_candidate_pool");
        }

        if (weakMaxAgeOutcome && fallbackCandidateCount > 0 && healthyOrganicCandidateCount < settings.MinimumHealthyCandidateCount)
        {
            rejectionReasons.Add("max_age_stop_only_produced_fallback_pool");
        }

        if (world.PrehistoryStopReason == PrehistoryStopReason.ForcedFallback)
        {
            rejectionReasons.Add("forced_fallback_stop_rejected");
        }

        if (!settings.AllowSingleFallbackCandidateSelection
            && world.PlayerEntryCandidates.Count == 1
            && world.PlayerEntryCandidates[0].IsFallbackCandidate)
        {
            rejectionReasons.Add("single_fallback_candidate_rejected");
        }

        if (fallbackCandidateCount > settings.MaximumEmergencyFallbackCandidatesToSurface)
        {
            rejectionReasons.Add("too_many_emergency_fallback_candidates");
        }

        if (fallbackCandidateCount > 0 && organicCandidateCount == 0)
        {
            rejectionReasons.Add("fallback_only_candidate_pool");
        }

        return rejectionReasons.Count == 0;
    }
}
