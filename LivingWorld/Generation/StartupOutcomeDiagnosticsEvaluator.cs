using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Generation;

public static class StartupOutcomeDiagnosticsEvaluator
{
    public static StartupOutcomeDiagnostics Evaluate(World world, IReadOnlyList<string>? regenerationReasons = null)
    {
        Dictionary<int, Polity> politiesById = world.Polities.ToDictionary(polity => polity.Id);

        int organicFocalCandidateCount = world.FocalCandidateProfiles.Count(profile =>
            profile.IsViable
            && (!politiesById.TryGetValue(profile.PolityId, out Polity? polity) || !polity.IsFallbackCreated));
        int fallbackFocalCandidateCount = world.FocalCandidateProfiles.Count(profile =>
            profile.IsViable
            && politiesById.TryGetValue(profile.PolityId, out Polity? polity)
            && polity.IsFallbackCreated);
        Dictionary<string, int> candidateRejectionCounts = world.CandidateRejectionReasons.Values
            .GroupBy(reason => reason, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        List<string> bottlenecks = [];
        bottlenecks.AddRange(world.PhaseBReadinessReport.FailureReasons.Select(reason => $"phase_b:{reason}"));
        bottlenecks.AddRange(world.PhaseBDiagnostics.WeaknessReasons.Select(reason => $"phase_b_diag:{reason}"));
        bottlenecks.AddRange(world.PhaseCReadinessReport.FailureReasons.Select(reason => $"phase_c:{reason}"));
        bottlenecks.AddRange(world.WorldReadinessReport.FailureReasons.Select(reason => $"entry:{reason}"));

        if (world.Polities.All(polity => polity.IsFallbackCreated || polity.Population <= 0))
        {
            bottlenecks.Add("no_organic_polities");
        }

        if (world.PlayerEntryCandidates.All(candidate => candidate.IsFallbackCandidate))
        {
            bottlenecks.Add("no_organic_player_entry_candidates");
        }

        if (world.PlayerEntryCandidates.Count < 2)
        {
            bottlenecks.Add($"candidate_pool_size:{world.PlayerEntryCandidates.Count}");
        }

        return new StartupOutcomeDiagnostics(
            world.SentientGroups.Count(group => !group.IsCollapsed && !group.IsFallbackCreated),
            world.SentientGroups.Count(group => !group.IsCollapsed && group.IsFallbackCreated),
            world.Societies.Count(society => !society.IsCollapsed && !society.IsFallbackCreated),
            world.Societies.Count(society => !society.IsCollapsed && society.IsFallbackCreated),
            world.SocialSettlements.Count(settlement => !settlement.IsAbandoned && !settlement.IsFallbackCreated),
            world.SocialSettlements.Count(settlement => !settlement.IsAbandoned && settlement.IsFallbackCreated),
            world.Polities.Count(polity => polity.Population > 0 && !polity.IsFallbackCreated),
            world.Polities.Count(polity => polity.Population > 0 && polity.IsFallbackCreated),
            organicFocalCandidateCount,
            fallbackFocalCandidateCount,
            world.PlayerEntryCandidates.Count(candidate => !candidate.IsFallbackCandidate),
            world.PlayerEntryCandidates.Count(candidate => candidate.IsFallbackCandidate),
            world.PlayerEntryCandidates.Count(candidate => candidate.IsEmergencyAdmitted),
            candidateRejectionCounts,
            bottlenecks
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(reason => reason, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            regenerationReasons ?? Array.Empty<string>());
    }
}
