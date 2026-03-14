using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Generation;

public static class PhaseCReadinessEvaluator
{
    public static PhaseCReadinessReport Evaluate(World world, WorldGenerationSettings settings)
    {
        int sentientGroupCount = world.SentientGroups.Count(group => !group.IsCollapsed);
        int persistentSocietyCount = world.Societies.Count(society => !society.IsCollapsed);
        int settlementCount = world.SocialSettlements.Count(settlement => !settlement.IsAbandoned);
        int viableSettlementCount = world.SocialSettlements.Count(settlement => !settlement.IsAbandoned && settlement.SettlementViability >= 0.55);
        int polityCount = world.Polities.Count(polity => polity.Population > 0);
        int viableCandidateCount = world.FocalCandidateProfiles.Count(profile => profile.IsViable);
        double averagePolityAge = polityCount == 0
            ? 0.0
            : world.Polities.Where(polity => polity.Population > 0).Average(polity => polity.YearsSinceFounded);
        double historicalEventDensity = world.Regions.Count == 0
            ? 0.0
            : (double)world.CivilizationalHistory.Count / world.Regions.Count;

        List<string> failures = [];
        if (sentientGroupCount < settings.MinimumPhaseCSentientGroupCount)
        {
            failures.Add("insufficient_sentient_groups");
        }

        if (persistentSocietyCount < settings.MinimumPhaseCPersistentSocietyCount)
        {
            failures.Add("insufficient_societies");
        }

        if (settlementCount < settings.MinimumPhaseCSettlementCount)
        {
            failures.Add("insufficient_settlements");
        }

        if (viableSettlementCount < settings.MinimumPhaseCViableSettlementCount)
        {
            failures.Add("insufficient_viable_settlements");
        }

        if (polityCount < settings.MinimumPhaseCPolityCount)
        {
            failures.Add("insufficient_polities");
        }

        if (viableCandidateCount < settings.MinimumPhaseCViableFocalCandidateCount)
        {
            failures.Add("insufficient_focal_candidates");
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
            persistentSocietyCount,
            settlementCount,
            viableSettlementCount,
            polityCount,
            viableCandidateCount,
            averagePolityAge,
            historicalEventDensity,
            failures);
    }
}
