using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;
using LivingWorld.Systems;
using Xunit;

namespace LivingWorld.Tests;

public sealed class EcologyAndHuntingSystemTests
{
    [Fact]
    public void EcosystemSystem_InitializesRegionalSpeciesPopulations_ForAllSpecies()
    {
        World world = CreateWorld();
        EcosystemSystem ecosystemSystem = new();

        ecosystemSystem.InitializeRegionalPopulations(world);

        Assert.All(world.Regions, region => Assert.Equal(world.Species.Count, region.SpeciesPopulations.Count));
        Assert.Contains(world.Regions[0].SpeciesPopulations, population => population.SpeciesId == 3 && population.PopulationCount > 0);
    }

    [Fact]
    public void EcosystemSystem_CanTriggerPreyCollapse_WhenPredatorsOverrunRegion()
    {
        World world = CreateWorld();
        Region region = world.Regions[0];
        EcosystemSystem ecosystemSystem = new(new EcosystemSettings
        {
            MaxMigrationTargetsPerPopulation = 0
        });

        ecosystemSystem.InitializeRegionalPopulations(world);

        region.GetOrCreateSpeciesPopulation(3).PopulationCount = 180;
        region.GetOrCreateSpeciesPopulation(4).PopulationCount = 12;
        region.GetOrCreateSpeciesPopulation(6).PopulationCount = 55;
        region.GetOrCreateSpeciesPopulation(7).PopulationCount = 18;

        ecosystemSystem.UpdateSeason(world);

        Assert.Contains(world.Events, evt => evt.Type is WorldEventType.PreyCollapse or WorldEventType.LocalSpeciesExtinction);
        Assert.True(region.GetSpeciesPopulation(4)!.PopulationCount < 12);
    }

    [Fact]
    public void HuntingSystem_ReducesRegionalPopulation_AndAddsFoodAndKnowledge()
    {
        World world = CreateWorld();
        Region region = world.Regions[0];
        Polity polity = new(10, "Riverwatch Clan", speciesId: 0, regionId: region.Id, population: 120);
        polity.EstablishFirstSettlement(region.Id, $"{region.Name} Hearth");
        polity.SettlementStatus = SettlementStatus.Settled;
        polity.AddSettlement(region.Id, $"{region.Name} Outpost 2");

        world.Polities.Add(polity);

        RegionSpeciesPopulation preyPopulation = region.GetOrCreateSpeciesPopulation(4);
        preyPopulation.HabitatSuitability = 1.0;
        preyPopulation.CarryingCapacity = 120;
        preyPopulation.PopulationCount = 90;

        HuntingSystem huntingSystem = new();
        huntingSystem.UpdateSeason(world);

        Assert.True(preyPopulation.PopulationCount < 90);
        Assert.True(polity.FoodStores > 0);
        Assert.Contains(4, polity.KnownEdibleSpeciesIds);
        Assert.Contains(polity.Discoveries, discovery => discovery.Key == "species-edible:4");
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.HuntingSuccess);
    }

    [Fact]
    public void FoodSystem_GatherFood_UsesPlantBiomassOnly()
    {
        World world = CreateWorld();
        Region region = world.Regions[0];
        Polity polity = new(10, "Riverwatch Clan", speciesId: 0, regionId: region.Id, population: 80);
        polity.EstablishFirstSettlement(region.Id, $"{region.Name} Hearth");
        polity.SettlementStatus = SettlementStatus.Settled;
        world.Polities.Add(polity);

        double animalBiomassBefore = region.AnimalBiomass;
        FoodSystem foodSystem = new();

        foodSystem.GatherFood(world);

        Assert.True(polity.FoodGatheredThisMonth > 0);
        Assert.True(region.PlantBiomass < 600);
        Assert.Equal(animalBiomassBefore, region.AnimalBiomass);
    }

    [Fact]
    public void EcosystemSystem_SyncsAnimalBiomass_FromRegionalConsumerPopulations()
    {
        World world = CreateWorld();
        Region region = world.Regions[0];
        EcosystemSystem ecosystemSystem = new();

        ecosystemSystem.InitializeRegionalPopulations(world);

        region.GetOrCreateSpeciesPopulation(3).PopulationCount = 120;
        region.GetOrCreateSpeciesPopulation(4).PopulationCount = 40;
        region.GetOrCreateSpeciesPopulation(6).PopulationCount = 10;
        region.GetOrCreateSpeciesPopulation(7).PopulationCount = 5;

        ecosystemSystem.ResolveSeasonalCleanup(world);

        double expectedPlantBiomass = Math.Min(
            region.MaxPlantBiomass,
            region.SpeciesPopulations
                .Where(population => world.Species.First(species => species.Id == population.SpeciesId).TrophicRole == TrophicRole.Producer)
                .Sum(population => population.PopulationCount) * 2.2);
        double expectedAnimalBiomass = Math.Min(
            region.MaxAnimalBiomass,
            region.SpeciesPopulations
                .Where(population => world.Species.First(species => species.Id == population.SpeciesId).TrophicRole != TrophicRole.Producer)
                .Sum(population => population.PopulationCount) * 1.1);

        Assert.Equal(expectedPlantBiomass, region.PlantBiomass);
        Assert.Equal(expectedAnimalBiomass, region.AnimalBiomass);
    }

    [Fact]
    public void EcosystemSystem_CanRecolonize_EmptyNeighboringRegion()
    {
        World world = CreateWorld();
        Region sourceRegion = world.Regions[0];
        Region targetRegion = world.Regions[1];
        EcosystemSystem ecosystemSystem = new(new EcosystemSettings
        {
            HerbivoreExpansionCapacityRatioThreshold = 0.40,
            FrontierTargetSuitability = 0.50
        });

        ecosystemSystem.InitializeRegionalPopulations(world);

        RegionSpeciesPopulation sourcePopulation = sourceRegion.GetOrCreateSpeciesPopulation(4);
        RegionSpeciesPopulation targetPopulation = targetRegion.GetOrCreateSpeciesPopulation(4);
        sourcePopulation.PopulationCount = Math.Max(60, sourcePopulation.CarryingCapacity / 2);
        targetPopulation.PopulationCount = 0;

        ecosystemSystem.UpdateSeason(world);

        Assert.True(targetPopulation.PopulationCount > 0);
        Assert.Contains(world.Events, evt =>
            evt.Type == WorldEventType.SpeciesPopulationEstablished
            && evt.RegionId == targetRegion.Id
            && evt.SpeciesId == 4
            && evt.Metadata.TryGetValue("migrationKind", out string? kind)
            && kind == "recolonization");
    }

    [Fact]
    public void EcosystemSystem_PredatorsRequirePreySupport_ToMigrate()
    {
        World world = CreateWorld();
        Region sourceRegion = world.Regions[0];
        Region targetRegion = world.Regions[1];
        EcosystemSystem ecosystemSystem = new(new EcosystemSettings
        {
            PredatorExpansionCapacityRatioThreshold = 0.40,
            PredatorMinimumPreyPopulation = 20
        });

        ecosystemSystem.InitializeRegionalPopulations(world);

        RegionSpeciesPopulation predatorSource = sourceRegion.GetOrCreateSpeciesPopulation(6);
        RegionSpeciesPopulation preySource = sourceRegion.GetOrCreateSpeciesPopulation(4);
        RegionSpeciesPopulation predatorTarget = targetRegion.GetOrCreateSpeciesPopulation(6);
        RegionSpeciesPopulation preyTarget = targetRegion.GetOrCreateSpeciesPopulation(4);

        predatorSource.PopulationCount = Math.Max(30, predatorSource.CarryingCapacity);
        preySource.PopulationCount = Math.Max(80, preySource.CarryingCapacity / 2);
        predatorTarget.PopulationCount = 0;
        preyTarget.PopulationCount = 0;

        ecosystemSystem.UpdateSeason(world);

        Assert.Equal(0, predatorTarget.PopulationCount);

        preyTarget.PopulationCount = 60;
        predatorSource.PopulationCount = Math.Max(30, predatorSource.CarryingCapacity);
        ecosystemSystem.UpdateSeason(world);

        Assert.True(predatorTarget.PopulationCount > 0);
    }

    [Fact]
    public void EcosystemSystem_HerbivoresMigrateLocally_AndDoNotSkipDistantRegions()
    {
        World world = CreateThreeRegionMigrationWorld();
        EcosystemSystem ecosystemSystem = new(new EcosystemSettings
        {
            HerbivoreExpansionCapacityRatioThreshold = 0.40,
            FrontierTargetSuitability = 0.50
        });

        ecosystemSystem.InitializeRegionalPopulations(world);

        RegionSpeciesPopulation sourcePopulation = world.Regions[0].GetOrCreateSpeciesPopulation(4);
        RegionSpeciesPopulation middlePopulation = world.Regions[1].GetOrCreateSpeciesPopulation(4);
        RegionSpeciesPopulation distantPopulation = world.Regions[2].GetOrCreateSpeciesPopulation(4);
        sourcePopulation.PopulationCount = Math.Max(80, sourcePopulation.CarryingCapacity / 2);
        middlePopulation.PopulationCount = 0;
        distantPopulation.PopulationCount = 0;

        ecosystemSystem.UpdateSeason(world);

        Assert.True(middlePopulation.PopulationCount > 0);
        Assert.Equal(0, distantPopulation.PopulationCount);
    }

    [Fact]
    public void EcosystemSystem_RejectsUnsuitableRegions_ForHerbivoreMigration()
    {
        World world = CreateWorld();
        Region sourceRegion = world.Regions[0];
        Region targetRegion = world.Regions[1];
        EcosystemSystem ecosystemSystem = new(new EcosystemSettings
        {
            HerbivoreExpansionCapacityRatioThreshold = 0.40,
            FrontierTargetSuitability = 0.60
        });

        targetRegion.Fertility = 0.12;
        targetRegion.WaterAvailability = 0.10;
        targetRegion.MaxPlantBiomass = 120;
        targetRegion.MaxAnimalBiomass = 80;
        targetRegion.PlantBiomass = 80;

        ecosystemSystem.InitializeRegionalPopulations(world);

        RegionSpeciesPopulation sourcePopulation = sourceRegion.GetOrCreateSpeciesPopulation(4);
        RegionSpeciesPopulation targetPopulation = targetRegion.GetOrCreateSpeciesPopulation(4);
        sourcePopulation.PopulationCount = Math.Max(80, sourcePopulation.CarryingCapacity / 2);
        targetPopulation.PopulationCount = 0;

        ecosystemSystem.UpdateSeason(world);

        Assert.Equal(0, targetPopulation.PopulationCount);
    }

    private static World CreateWorld()
    {
        World world = new(new WorldTime(5, 3));
        Region region = new(0, "Stone Valley")
        {
            Fertility = 0.65,
            WaterAvailability = 0.60,
            PlantBiomass = 600,
            AnimalBiomass = 240,
            MaxPlantBiomass = 1000,
            MaxAnimalBiomass = 400
        };

        region.AddConnection(1);
        world.Regions.Add(region);
        world.Regions.Add(new Region(1, "Northreach")
        {
            Fertility = 0.55,
            WaterAvailability = 0.52,
            PlantBiomass = 520,
            AnimalBiomass = 180,
            MaxPlantBiomass = 1000,
            MaxAnimalBiomass = 400
        });
        world.Regions[1].AddConnection(0);

        world.Species.Add(new Species(0, "Humans", 0.8, 0.7)
        {
            IsSapient = true,
            TrophicRole = TrophicRole.Omnivore,
            FertilityPreference = 0.6,
            WaterPreference = 0.55,
            PlantBiomassAffinity = 0.4,
            AnimalBiomassAffinity = 0.45,
            BaseCarryingCapacityFactor = 0.9,
            MigrationCapability = 0.2,
            ExpansionPressure = 0.18,
            MeatYield = 10
        });
        world.Species.Add(new Species(3, "River Reed", 0.1, 0.0)
        {
            TrophicRole = TrophicRole.Producer,
            FertilityPreference = 0.7,
            WaterPreference = 0.8,
            PlantBiomassAffinity = 0.9,
            AnimalBiomassAffinity = 0.0,
            BaseCarryingCapacityFactor = 1.15,
            BaseReproductionRate = 0.16,
            BaseDeclineRate = 0.02,
            MeatYield = 0
        });
        world.Species.Add(new Species(4, "Stonehorn Elk", 0.2, 0.3)
        {
            TrophicRole = TrophicRole.Herbivore,
            FertilityPreference = 0.58,
            WaterPreference = 0.55,
            PlantBiomassAffinity = 0.75,
            AnimalBiomassAffinity = 0.10,
            BaseCarryingCapacityFactor = 1.0,
            MigrationCapability = 0.22,
            ExpansionPressure = 0.20,
            MeatYield = 22,
            HuntingDifficulty = 0.30,
            HuntingDanger = 0.24
        });
        world.Species.Add(new Species(6, "Ashfang Wolf", 0.1, 0.4)
        {
            TrophicRole = TrophicRole.Predator,
            FertilityPreference = 0.45,
            WaterPreference = 0.38,
            PlantBiomassAffinity = 0.22,
            AnimalBiomassAffinity = 0.72,
            BaseCarryingCapacityFactor = 0.75,
            MigrationCapability = 0.32,
            ExpansionPressure = 0.24,
            MeatYield = 14,
            HuntingDifficulty = 0.42,
            HuntingDanger = 0.40
        });
        world.Species.Add(new Species(7, "Ridge Lion", 0.18, 0.2)
        {
            TrophicRole = TrophicRole.Apex,
            FertilityPreference = 0.36,
            WaterPreference = 0.30,
            PlantBiomassAffinity = 0.16,
            AnimalBiomassAffinity = 0.80,
            BaseCarryingCapacityFactor = 0.56,
            MigrationCapability = 0.34,
            ExpansionPressure = 0.28,
            MeatYield = 30,
            HuntingDifficulty = 0.56,
            HuntingDanger = 0.72
        });

        world.Species.First(species => species.Id == 0).DietSpeciesIds.AddRange([3, 4, 6]);
        world.Species.First(species => species.Id == 4).DietSpeciesIds.Add(3);
        world.Species.First(species => species.Id == 6).DietSpeciesIds.AddRange([4, 7]);
        world.Species.First(species => species.Id == 7).DietSpeciesIds.AddRange([4, 6]);

        return world;
    }

    private static World CreateThreeRegionMigrationWorld()
    {
        World world = CreateWorld();
        Region middleRegion = world.Regions[1];
        Region distantRegion = new(2, "Far Meadow")
        {
            Fertility = 0.60,
            WaterAvailability = 0.58,
            PlantBiomass = 560,
            AnimalBiomass = 120,
            MaxPlantBiomass = 1000,
            MaxAnimalBiomass = 400
        };

        middleRegion.AddConnection(2);
        distantRegion.AddConnection(1);
        world.Regions.Add(distantRegion);

        return world;
    }
}
