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
        EcosystemSystem ecosystemSystem = new();

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
        Polity polity = new(10, "Riverwatch Clan", speciesId: 0, regionId: region.Id, population: 120)
        {
            SettlementStatus = SettlementStatus.Settled,
            SettlementCount = 2
        };

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
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.HuntingSuccess);
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
}
