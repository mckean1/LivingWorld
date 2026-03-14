using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;

namespace LivingWorld.Generation;

public static class PhaseAReadinessEvaluator
{
    public static PhaseAReadinessReport Evaluate(World world, WorldGenerationSettings settings)
    {
        int totalRegions = world.Regions.Count;
        if (totalRegions == 0)
        {
            return PhaseAReadinessReport.Empty;
        }

        int occupiedRegions = world.Regions.Count(region => region.SpeciesPopulations.Any(population => population.PopulationCount > 0));
        int producerCoveredRegions = world.Regions.Count(region => HasRole(region, world.Species, TrophicRole.Producer));
        int consumerCoveredRegions = world.Regions.Count(region => region.SpeciesPopulations.Any(population =>
            population.PopulationCount > 0
            && world.Species.First(species => species.Id == population.SpeciesId).TrophicRole is TrophicRole.Herbivore or TrophicRole.Omnivore));
        int predatorCoveredRegions = world.Regions.Count(region => HasRole(region, world.Species, TrophicRole.Predator) || HasRole(region, world.Species, TrophicRole.Apex));
        int biodiversityCount = world.Species.Count(species => world.Regions.Any(region => region.GetSpeciesPopulation(species.Id)?.PopulationCount > 0));
        int stableRegionCount = world.Regions.Count(region => IsStableRegion(region, world.Species));
        int collapsingRegionCount = world.Regions.Count(region => region.SpeciesPopulations.Any(population =>
            population.PopulationCount > 0
            && population.Trend is PopulationTrend.Collapsing or PopulationTrend.Extinct
            && population.StressScore >= 0.70));

        double occupiedRegionPercentage = (double)occupiedRegions / totalRegions;
        double producerCoverage = (double)producerCoveredRegions / totalRegions;
        double consumerCoverage = (double)consumerCoveredRegions / totalRegions;
        double predatorCoverage = (double)predatorCoveredRegions / totalRegions;

        List<string> failureReasons = [];
        if (occupiedRegionPercentage < settings.MinimumPhaseAOccupiedRegionPercentage)
        {
            failureReasons.Add($"occupied_regions_below_target:{occupiedRegionPercentage:F2}");
        }

        if (producerCoverage < settings.MinimumPhaseAProducerCoverage)
        {
            failureReasons.Add($"producer_coverage_below_target:{producerCoverage:F2}");
        }

        if (consumerCoverage < settings.MinimumPhaseAConsumerCoverage)
        {
            failureReasons.Add($"consumer_coverage_below_target:{consumerCoverage:F2}");
        }

        if (predatorCoverage < settings.MinimumPhaseAPredatorCoverage)
        {
            failureReasons.Add($"predator_coverage_below_target:{predatorCoverage:F2}");
        }

        if (stableRegionCount < Math.Max(4, totalRegions / 3))
        {
            failureReasons.Add($"stable_regions_too_low:{stableRegionCount}");
        }

        if (collapsingRegionCount > Math.Max(3, totalRegions / 5))
        {
            failureReasons.Add($"too_many_collapsing_regions:{collapsingRegionCount}");
        }

        if (biodiversityCount < Math.Max(4, world.Species.Count - 2))
        {
            failureReasons.Add($"biodiversity_too_thin:{biodiversityCount}");
        }

        return new PhaseAReadinessReport(
            totalRegions,
            occupiedRegions,
            occupiedRegionPercentage,
            producerCoveredRegions,
            producerCoverage,
            consumerCoveredRegions,
            consumerCoverage,
            predatorCoveredRegions,
            predatorCoverage,
            biodiversityCount,
            stableRegionCount,
            collapsingRegionCount,
            failureReasons.Count == 0,
            failureReasons);
    }

    private static bool HasRole(Region region, IReadOnlyCollection<Species> speciesCatalog, TrophicRole role)
        => region.SpeciesPopulations.Any(population =>
            population.PopulationCount > 0
            && speciesCatalog.First(species => species.Id == population.SpeciesId).TrophicRole == role);

    private static bool IsStableRegion(Region region, IReadOnlyCollection<Species> speciesCatalog)
    {
        bool producerPresent = HasRole(region, speciesCatalog, TrophicRole.Producer);
        if (!producerPresent)
        {
            return false;
        }

        List<RegionSpeciesPopulation> activePopulations = region.SpeciesPopulations
            .Where(population => population.PopulationCount > 0)
            .ToList();
        if (activePopulations.Count == 0)
        {
            return false;
        }

        double averageStress = activePopulations.Average(population => population.StressScore);
        return averageStress < 0.54
            && activePopulations.Count(population => population.Trend == PopulationTrend.Collapsing) <= 1;
    }
}
