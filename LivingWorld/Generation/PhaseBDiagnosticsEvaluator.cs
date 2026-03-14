using LivingWorld.Core;
using LivingWorld.Life;

namespace LivingWorld.Generation;

public static class PhaseBDiagnosticsEvaluator
{
    public static PhaseBDiagnostics Evaluate(World world, WorldGenerationSettings settings)
    {
        List<EvolutionaryLineage> activeLineages = world.EvolutionaryLineages
            .Where(lineage => !lineage.IsExtinct)
            .ToList();
        List<EvolutionaryLineage> allLineages = world.EvolutionaryLineages.ToList();
        double averageAncestryDepth = allLineages.Count == 0
            ? 0.0
            : allLineages.Average(lineage => lineage.AncestryDepth);
        int branchingLineageCount = allLineages.Count(lineage => lineage.DescendantLineageIds.Count > 0);
        int deepLineageCount = allLineages.Count(lineage => lineage.AncestryDepth >= 2);
        int matureDivergenceLineageCount = activeLineages.Count(lineage => lineage.MaxObservedDivergenceMilestone >= 2);
        int adaptedBiomeSpan = activeLineages.SelectMany(lineage => lineage.AdaptedBiomeIds).Distinct().Count();
        int localExtinctionEventCount = world.EvolutionaryHistory.Count(evt => evt.Type == EvolutionaryHistoryEventType.LocalExtinction);
        int globalExtinctionEventCount = world.EvolutionaryHistory.Count(evt => evt.Type == EvolutionaryHistoryEventType.GlobalExtinction);
        int recolonizationEventCount = world.Events.Count(evt => evt.Type == WorldEventType.SpeciesPopulationRecolonized);
        HashSet<int> extinctSpeciesIds = world.LocalPopulationExtinctions
            .Select(record => record.SpeciesId)
            .ToHashSet();
        int replacementLineageCount = world.Species.Count(species =>
            !species.IsGloballyExtinct
            && species.ParentSpeciesId is not null
            && extinctSpeciesIds.Contains(species.ParentSpeciesId.Value));
        int sentienceCapableRootBranchCount = activeLineages
            .Where(lineage => lineage.SentienceCapability == SentienceCapabilityState.Capable)
            .Select(lineage => lineage.RootAncestorLineageId)
            .Distinct()
            .Count();

        List<string> weaknessReasons = [];
        if (branchingLineageCount < Math.Max(1, settings.MinimumPhaseBSpeciationCount))
        {
            weaknessReasons.Add("low_branching_lineage_count");
        }

        if (deepLineageCount < settings.MinimumPhaseBAncestryDepth)
        {
            weaknessReasons.Add("thin_lineage_depth");
        }

        if (matureDivergenceLineageCount < Math.Max(1, settings.MinimumPhaseBMatureRegionalDivergenceCount / 2))
        {
            weaknessReasons.Add("thin_divergence_maturity");
        }

        if (adaptedBiomeSpan < 3)
        {
            weaknessReasons.Add("narrow_adaptation_spread");
        }

        if ((localExtinctionEventCount + recolonizationEventCount) < settings.MinimumPhaseBExtinctLineageCount)
        {
            weaknessReasons.Add("thin_extinction_replacement_texture");
        }

        if (sentienceCapableRootBranchCount < settings.MinimumPhaseBSentienceCapableLineageCount)
        {
            weaknessReasons.Add("narrow_sentience_branch_distribution");
        }

        return new PhaseBDiagnostics(
            averageAncestryDepth,
            branchingLineageCount,
            deepLineageCount,
            matureDivergenceLineageCount,
            adaptedBiomeSpan,
            localExtinctionEventCount,
            globalExtinctionEventCount,
            recolonizationEventCount,
            replacementLineageCount,
            sentienceCapableRootBranchCount,
            weaknessReasons);
    }
}
