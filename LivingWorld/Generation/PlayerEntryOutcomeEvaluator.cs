using LivingWorld.Core;

namespace LivingWorld.Generation;

public static class PlayerEntryOutcomeEvaluator
{
    public static bool ShouldSurfaceFocalSelection(World world, WorldGenerationSettings settings, out List<string> rejectionReasons)
    {
        rejectionReasons = [];
        int fallbackCandidateCount = world.PlayerEntryCandidates.Count(candidate => candidate.IsFallbackCandidate);
        int healthyCandidateCount = world.PlayerEntryCandidates.Count(candidate => !candidate.IsFallbackCandidate && candidate.RankScore >= settings.MinimumHealthyCandidateScore);
        bool biologyWeak = world.WorldReadinessReport.FailureReasons.Contains("biology_not_ready", StringComparer.OrdinalIgnoreCase)
            || world.WorldReadinessReport.FailureReasons.Contains("biology_floor_below_minimum", StringComparer.OrdinalIgnoreCase);
        bool weakMaxAgeOutcome = world.PrehistoryStopReason is PrehistoryStopReason.MaxAgeReached or PrehistoryStopReason.ForcedFallback;

        if (biologyWeak && weakMaxAgeOutcome && fallbackCandidateCount > 0)
        {
            rejectionReasons.Add("biology_floor_not_met");
        }

        if (weakMaxAgeOutcome && fallbackCandidateCount > 0 && healthyCandidateCount < settings.MinimumHealthyCandidateCount)
        {
            rejectionReasons.Add("max_age_stop_only_produced_fallback_pool");
        }

        if (weakMaxAgeOutcome && biologyWeak && fallbackCandidateCount > 0 && world.PlayerEntryCandidates.Count < 2)
        {
            rejectionReasons.Add($"candidate_pool_too_shallow:{world.PlayerEntryCandidates.Count}");
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

        if (world.PlayerEntryCandidates.Count > 0 && healthyCandidateCount < settings.MinimumHealthyCandidateCount && fallbackCandidateCount > 0)
        {
            rejectionReasons.Add($"healthy_candidate_count_below_floor:{healthyCandidateCount}");
        }

        return rejectionReasons.Count == 0;
    }
}
