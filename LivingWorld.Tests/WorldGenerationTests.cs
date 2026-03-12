using LivingWorld.Generation;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;
using LivingWorld.Systems;
using Xunit;

namespace LivingWorld.Tests;

public sealed class WorldGenerationTests
{
    [Fact]
    public void WorldGenerator_UsesFullerDefaultWorldScale()
    {
        WorldGenerator generator = new(seed: 7);

        var world = generator.Generate();

        Assert.Equal(36, world.Regions.Count);
        Assert.Equal(28, world.Species.Count);
        Assert.Equal(10, world.Polities.Count);
    }

    [Fact]
    public void WorldGenerator_DistributesStartingPolitiesAcrossDistinctViableRegions()
    {
        WorldGenerator generator = new(seed: 9);

        var world = generator.Generate();

        int distinctRegions = world.Polities.Select(polity => polity.RegionId).Distinct().Count();
        Assert.True(distinctRegions >= 8);
        Assert.All(world.Polities, polity =>
        {
            Region home = world.Regions.First(region => region.Id == polity.RegionId);
            Assert.True(home.Fertility >= 0.30 || home.WaterAvailability >= 0.45);
        });
    }

    [Fact]
    public void WorldGenerator_GivesEachStartingPolity_AHomeSettlementAnchor()
    {
        WorldGenerator generator = new(seed: 10);

        var world = generator.Generate();

        Assert.All(world.Polities, polity =>
        {
            Assert.True(polity.HasSettlements);
            Assert.Equal(SettlementStatus.SemiSettled, polity.SettlementStatus);
            Assert.Equal(polity.RegionId, polity.Settlements[0].RegionId);
        });
    }

    [Fact]
    public void EcosystemInitialization_SeedsSpeciesIntoSubsetsOfRegions()
    {
        WorldGenerator generator = new(seed: 11);
        var world = generator.Generate();
        EcosystemSystem ecosystemSystem = new();

        ecosystemSystem.InitializeRegionalPopulations(world);

        Assert.All(world.Species, species =>
        {
            int occupiedRegions = world.Regions.Count(region => region.GetSpeciesPopulation(species.Id)?.PopulationCount > 0);
            Assert.True(occupiedRegions > 0);
            Assert.True(occupiedRegions < world.Regions.Count);
        });

        Assert.Contains(world.Regions, region => region.Biome == RegionBiome.RiverValley);
        Assert.Contains(world.Regions, region => region.Biome == RegionBiome.Coast);
        Assert.Contains(world.Regions, region => region.Biome == RegionBiome.Mountains);
    }

    [Fact]
    public void FullerWorld_ProvidesEarlyFoodWebCoverageAcrossRegions()
    {
        WorldGenerator generator = new(seed: 13);
        var world = generator.Generate();
        EcosystemSystem ecosystemSystem = new();

        ecosystemSystem.InitializeRegionalPopulations(world);

        int regionsWithProducer = 0;
        int regionsWithHerbivore = 0;
        int regionsWithPredator = 0;

        foreach (Region region in world.Regions)
        {
            if (HasRole(region, world.Species, TrophicRole.Producer))
            {
                regionsWithProducer++;
            }

            if (HasRole(region, world.Species, TrophicRole.Herbivore))
            {
                regionsWithHerbivore++;
            }

            if (HasRole(region, world.Species, TrophicRole.Predator) || HasRole(region, world.Species, TrophicRole.Apex))
            {
                regionsWithPredator++;
            }
        }

        Assert.True(regionsWithProducer >= 24);
        Assert.True(regionsWithHerbivore >= 18);
        Assert.True(regionsWithPredator >= 10);
    }

    [Fact]
    public void EcosystemInitialization_SeedsMeaningfulHerbivoreBaselines_InFertileRegions()
    {
        WorldGenerator generator = new(seed: 13);
        var world = generator.Generate();
        EcosystemSystem ecosystemSystem = new();

        ecosystemSystem.InitializeRegionalPopulations(world);

        List<Region> fertileRegions = world.Regions
            .Where(region => region.Fertility >= 0.55 && region.WaterAvailability >= 0.50)
            .ToList();

        double averageHerbivoresInFertileRegions = fertileRegions.Average(region => region.SpeciesPopulations
            .Where(population => population.PopulationCount > 0
                && world.Species.First(species => species.Id == population.SpeciesId).TrophicRole == TrophicRole.Herbivore)
            .Sum(population => population.PopulationCount));
        int fertileRegionsWithMultipleConsumers = fertileRegions.Count(region => region.SpeciesPopulations.Count(population =>
            population.PopulationCount > 0
            && world.Species.First(species => species.Id == population.SpeciesId).TrophicRole != TrophicRole.Producer) >= 2);

        Assert.True(averageHerbivoresInFertileRegions >= 85);
        Assert.True(fertileRegionsWithMultipleConsumers >= Math.Max(6, fertileRegions.Count / 2));
    }

    [Fact]
    public void SeasonalEcosystem_AllowsHerbivoresToExpand_WhenPlantsAreAbundant()
    {
        WorldGenerator generator = new(seed: 13);
        var world = generator.Generate();
        EcosystemSystem ecosystemSystem = new();

        ecosystemSystem.InitializeRegionalPopulations(world);

        List<Region> fertileRegions = world.Regions
            .Where(region => region.Fertility >= 0.55 && region.WaterAvailability >= 0.50)
            .ToList();
        double initialAverageHerbivores = fertileRegions.Average(region => region.SpeciesPopulations
            .Where(population => population.PopulationCount > 0
                && world.Species.First(species => species.Id == population.SpeciesId).TrophicRole == TrophicRole.Herbivore)
            .Sum(population => population.PopulationCount));

        for (int season = 0; season < 20; season++)
        {
            ecosystemSystem.UpdateSeason(world);
            ecosystemSystem.ResolveSeasonalCleanup(world);
        }

        double laterAverageHerbivores = fertileRegions.Average(region => region.SpeciesPopulations
            .Where(population => population.PopulationCount > 0
                && world.Species.First(species => species.Id == population.SpeciesId).TrophicRole == TrophicRole.Herbivore)
            .Sum(population => population.PopulationCount));

        Assert.True(laterAverageHerbivores >= initialAverageHerbivores * 0.90);
        Assert.Contains(fertileRegions, region => region.AnimalBiomass >= 140);
    }

    [Fact]
    public void StartingPolities_HaveAccessibleSupportSpeciesNearTheirHomelands()
    {
        WorldGenerator generator = new(seed: 17);
        var world = generator.Generate();
        EcosystemSystem ecosystemSystem = new();

        ecosystemSystem.InitializeRegionalPopulations(world);

        Assert.All(world.Polities, polity =>
        {
            HashSet<int> accessibleRegionIds = [polity.RegionId];
            Region home = world.Regions.First(region => region.Id == polity.RegionId);
            foreach (int neighborId in home.ConnectedRegionIds)
            {
                accessibleRegionIds.Add(neighborId);
            }

            int supportSpecies = world.Species.Count(species =>
                !species.IsSapient
                && (species.TrophicRole == TrophicRole.Producer || species.MeatYield > 0)
                && accessibleRegionIds.Any(regionId => world.Regions[regionId].GetSpeciesPopulation(species.Id)?.PopulationCount > 0));

            Assert.True(supportSpecies >= 3);
        });
    }

    private static bool HasRole(Region region, IReadOnlyCollection<Species> speciesCatalog, TrophicRole role)
    {
        return region.SpeciesPopulations.Any(population =>
            population.PopulationCount > 0
            && speciesCatalog.First(species => species.Id == population.SpeciesId).TrophicRole == role);
    }
}
