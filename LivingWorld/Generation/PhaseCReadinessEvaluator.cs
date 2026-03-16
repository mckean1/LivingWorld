using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Generation;

public static class PhaseCReadinessEvaluator
{
    public static PhaseCReadinessReport Evaluate(World world, WorldGenerationSettings settings)
        => Evaluate(world, settings, candidateEvaluations: null);

    public static PhaseCReadinessReport Evaluate(
        World world,
        WorldGenerationSettings settings,
        IReadOnlyDictionary<int, CandidateReadinessEvaluation>? candidateEvaluations)
    {
        Dictionary<int, Polity> politiesById = world.Polities.ToDictionary(polity => polity.Id);

        int sentientGroupCount = world.SentientGroups.Count(group => !group.IsCollapsed);
        int organicSentientGroupCount = world.SentientGroups.Count(group => !group.IsCollapsed && !group.IsFallbackCreated);
        int fallbackSentientGroupCount = sentientGroupCount - organicSentientGroupCount;

        int persistentSocietyCount = world.Societies.Count(society => HasActiveSocietalSubstrate(world, society));
        int organicPersistentSocietyCount = world.Societies.Count(society => HasActiveSocietalSubstrate(world, society) && !society.IsFallbackCreated);
        int fallbackPersistentSocietyCount = persistentSocietyCount - organicPersistentSocietyCount;

        int settlementCount = world.SocialSettlements.Count(settlement => !settlement.IsAbandoned);
        int organicSettlementCount = world.SocialSettlements.Count(settlement => !settlement.IsAbandoned && !settlement.IsFallbackCreated);
        int fallbackSettlementCount = settlementCount - organicSettlementCount;
        int viableSettlementCount = world.SocialSettlements.Count(settlement => !settlement.IsAbandoned && settlement.SettlementViability >= 0.55);
        int organicViableSettlementCount = world.SocialSettlements.Count(settlement =>
            !settlement.IsAbandoned
            && !settlement.IsFallbackCreated
            && settlement.SettlementViability >= 0.55);

        int polityCount = world.Polities.Count(polity => polity.Population > 0);
        int organicPolityCount = world.Polities.Count(polity => polity.Population > 0 && !polity.IsFallbackCreated);
        int fallbackPolityCount = polityCount - organicPolityCount;
        int organicSocialTrajectoryCount = organicSentientGroupCount + organicPersistentSocietyCount;

        int viableCandidateCount = candidateEvaluations is null
            ? world.FocalCandidateProfiles.Count(profile => profile.IsViable)
            : candidateEvaluations.Values.Count(evaluation => evaluation.IsViable);
        int organicViableCandidateCount = candidateEvaluations is null
            ? world.FocalCandidateProfiles.Count(profile =>
                profile.IsViable
                && (!politiesById.TryGetValue(profile.PolityId, out Polity? polity) || !polity.IsFallbackCreated))
            : candidateEvaluations.Values.Count(evaluation =>
                evaluation.IsViable
                && politiesById.TryGetValue(evaluation.PolityId, out Polity? polity)
                && !polity.IsFallbackCreated);
        int fallbackViableCandidateCount = viableCandidateCount - organicViableCandidateCount;

        double averagePolityAge = organicPolityCount == 0
            ? 0.0
            : world.Polities.Where(polity => polity.Population > 0 && !polity.IsFallbackCreated).Average(polity => polity.YearsSinceFounded);
        double historicalEventDensity = world.Regions.Count == 0
            ? 0.0
            : (double)world.CivilizationalHistory.Count / world.Regions.Count;

        List<string> failures = [];
        if (organicSocialTrajectoryCount < settings.MinimumPhaseCSentientGroupCount)
        {
            failures.Add("insufficient_organic_social_trajectories");
        }

        if (organicPersistentSocietyCount < settings.MinimumPhaseCPersistentSocietyCount)
        {
            failures.Add("insufficient_organic_societies");
        }

        if (organicSettlementCount < settings.MinimumPhaseCSettlementCount)
        {
            failures.Add("insufficient_organic_settlements");
        }

        if (organicViableSettlementCount < settings.MinimumPhaseCViableSettlementCount)
        {
            failures.Add("insufficient_organic_viable_settlements");
        }

        if (organicPolityCount < settings.MinimumPhaseCPolityCount)
        {
            failures.Add("insufficient_organic_polities");
        }

        if (organicViableCandidateCount < settings.MinimumPhaseCViableFocalCandidateCount)
        {
            failures.Add("insufficient_organic_focal_candidates");
        }

        if (organicPolityCount == 0 && fallbackPolityCount > 0)
        {
            failures.Add("fallback_only_polities");
        }

        if (organicViableCandidateCount == 0 && fallbackViableCandidateCount > 0)
        {
            failures.Add("fallback_only_focal_candidates");
        }

        if (averagePolityAge < settings.MinimumPhaseCAveragePolityAge)
        {
            failures.Add("polities_too_young");
        }

        if (historicalEventDensity < settings.MinimumPhaseCHistoricalEventDensity)
        {
            failures.Add("insufficient_history_density");
        }

        return new PhaseCReadinessReport(
            failures.Count == 0,
            sentientGroupCount,
            organicSentientGroupCount,
            fallbackSentientGroupCount,
            persistentSocietyCount,
            organicPersistentSocietyCount,
            fallbackPersistentSocietyCount,
            settlementCount,
            organicSettlementCount,
            fallbackSettlementCount,
            viableSettlementCount,
            polityCount,
            organicPolityCount,
            fallbackPolityCount,
            viableCandidateCount,
            organicViableCandidateCount,
            fallbackViableCandidateCount,
            averagePolityAge,
            historicalEventDensity,
            failures);
    }

    private static bool HasActiveSocietalSubstrate(World world, EmergingSociety society)
        => !society.IsCollapsed
            || (society.FounderPolityId.HasValue && world.Polities.Any(polity => polity.Id == society.FounderPolityId.Value && polity.Population > 0));
}
