using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;

namespace LivingWorld.Generation;

public static class PhaseBReadinessEvaluator
{
    public static PhaseBReadinessReport Evaluate(World world, WorldGenerationSettings settings)
    {
        List<EvolutionaryLineage> activeLineages = world.EvolutionaryLineages
            .Where(lineage => !lineage.IsExtinct)
            .ToList();
        int matureLineageCount = activeLineages.Count(lineage => lineage.Stage is LineageStage.EstablishedSpecies or LineageStage.SentienceCapable);
        int speciationCount = world.EvolutionaryHistory.Count(evt => evt.Type == EvolutionaryHistoryEventType.Speciation);
        int globallyExtinctLineageCount = world.EvolutionaryLineages.Count(lineage => lineage.IsExtinct);
        int localExtinctionCount = world.EvolutionaryHistory.Count(evt => evt.Type == EvolutionaryHistoryEventType.LocalExtinction);
        int recolonizationCount = world.Events.Count(evt => evt.Type == WorldEventType.SpeciesPopulationRecolonized);
        int extinctLineageCount = Math.Max(globallyExtinctLineageCount, localExtinctionCount + recolonizationCount);
        int maxAncestryDepth = world.EvolutionaryLineages.Count == 0 ? 0 : world.EvolutionaryLineages.Max(lineage => lineage.AncestryDepth);
        int matureRegionalDivergenceCount = world.Regions.Sum(region => region.SpeciesPopulations.Count(population => population.DivergenceScore >= settings.PhaseBMatureRegionalDivergenceThreshold));
        int sentienceCapableLineageCount = activeLineages.Count(lineage => lineage.SentienceCapability == SentienceCapabilityState.Capable);
        int stableEcosystemRegionCount = world.Regions.Count(region => region.SpeciesPopulations.Any(population => population.PopulationCount > 0 && population.Trend != PopulationTrend.Collapsing));

        List<string> failures = [];
        if (matureLineageCount < settings.MinimumPhaseBMatureLineageCount)
        {
            failures.Add("insufficient_mature_lineages");
        }

        if (speciationCount < settings.MinimumPhaseBSpeciationCount)
        {
            failures.Add("insufficient_speciation");
        }

        if (extinctLineageCount < settings.MinimumPhaseBExtinctLineageCount)
        {
            failures.Add("insufficient_extinction_history");
        }

        if (maxAncestryDepth < settings.MinimumPhaseBAncestryDepth)
        {
            failures.Add("shallow_lineage_depth");
        }

        if (matureRegionalDivergenceCount < settings.MinimumPhaseBMatureRegionalDivergenceCount)
        {
            failures.Add("insufficient_regional_divergence");
        }

        if (sentienceCapableLineageCount < settings.MinimumPhaseBSentienceCapableLineageCount)
        {
            failures.Add("no_sentience_capable_branch");
        }

        if (stableEcosystemRegionCount < settings.MinimumPhaseBStableRegionCount)
        {
            failures.Add("ecosystem_instability");
        }

        return new PhaseBReadinessReport(
            failures.Count == 0,
            matureLineageCount,
            speciationCount,
            extinctLineageCount,
            maxAncestryDepth,
            matureRegionalDivergenceCount,
            sentienceCapableLineageCount,
            stableEcosystemRegionCount,
            failures);
    }
}
