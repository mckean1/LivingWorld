using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;
using LivingWorld.Systems;
using Xunit;

namespace LivingWorld.Tests;

public sealed class MutationSystemTests
{
    [Fact]
    public void SeasonalSimulation_UsesSameSeasonSpeciesExchange_ForMutationInputs()
    {
        World world = CreateWorld();
        using Simulation simulation = new(world, new Presentation.SimulationOptions
        {
            OutputMode = Presentation.OutputMode.Debug
        });

        Region targetRegion = world.Regions[1];
        RegionSpeciesPopulation sourcePopulation = GetElkPopulation(world);
        RegionSpeciesPopulation targetPopulation = targetRegion.GetSpeciesPopulation(4)!;

        sourcePopulation.PopulationCount = 150;
        sourcePopulation.RecentFoodStress = 1.0;
        targetPopulation.PopulationCount = 0;
        targetPopulation.CarryingCapacity = 80;

        simulation.RunMonths(1);

        Assert.True(targetPopulation.PopulationCount > 0);
        Assert.True(targetPopulation.ReceivedMigrantsThisSeason);
        Assert.True(targetPopulation.HabitatMismatchMutationPressure >= 0.10);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.SpeciesPopulationEstablished && evt.RegionId == targetRegion.Id);
    }

    [Fact]
    public void EcosystemSeason_ResetPreventsStaleMigrationFlags_FromDrivingLaterMutation()
    {
        World world = CreateWorld(withConnectedPopulation: false);
        RegionSpeciesPopulation population = world.Regions[1].GetOrCreateSpeciesPopulation(4);
        MutationSystem mutationSystem = new(seed: 9);
        EcosystemSystem ecosystemSystem = new();

        population.PopulationCount = 18;
        population.ReceivedMigrantsThisSeason = true;
        population.SentMigrantsThisSeason = true;
        population.EstablishedThisSeason = true;

        ecosystemSystem.UpdateSeason(world);
        mutationSystem.UpdateSeason(world);

        Assert.False(population.ReceivedMigrantsThisSeason);
        Assert.False(population.SentMigrantsThisSeason);
        Assert.False(population.EstablishedThisSeason);
        Assert.True(population.HabitatMismatchMutationPressure < 0.05);
    }

    [Fact]
    public void SingleSeasonPressureSpike_AccumulatesPressureWithoutImmediateMutation()
    {
        World world = CreateWorld();
        RegionSpeciesPopulation population = GetElkPopulation(world);
        MutationSystem mutationSystem = new(seed: 11);

        population.RecentFoodStress = 1.0;

        mutationSystem.UpdateSeason(world);

        Assert.True(population.FoodStressMutationPressure > 0.40);
        Assert.Equal(0, population.MinorMutationCount);
        Assert.Equal(0, population.MajorMutationCount);
        Assert.DoesNotContain(world.Events, evt => evt.Type == WorldEventType.SpeciesPopulationMutated);
    }

    [Fact]
    public void IsolationBuildsPressure_AndEmitsIsolationEvent()
    {
        World world = CreateWorld(withConnectedPopulation: false);
        RegionSpeciesPopulation population = GetElkPopulation(world);
        MutationSystem mutationSystem = new(seed: 7);

        population.IsolationSeasons = 7;

        mutationSystem.UpdateSeason(world);

        Assert.Equal(8, population.IsolationSeasons);
        Assert.True(population.IsolationMutationPressure > 0);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.SpeciesPopulationIsolated);
    }

    [Fact]
    public void SustainedModeratePressure_CanCreateMinorMutation()
    {
        World world = CreateWorld();
        RegionSpeciesPopulation population = GetElkPopulation(world);
        MutationSystem mutationSystem = new(seed: 23);

        for (int season = 0; season < 18 && population.MinorMutationCount == 0; season++)
        {
            population.RecentFoodStress = 0.85;
            population.RecentHuntingPressure = 0.55;
            population.RecentPredationPressure = 0.35;
            population.HabitatSuitability = 0.68;

            mutationSystem.UpdateSeason(world);
        }

        Assert.True(population.MinorMutationCount > 0);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.SpeciesPopulationMutated);
        Assert.True(population.DivergenceScore > 0.20);
    }

    [Fact]
    public void SustainedExtremePressure_CanCreateMajorMutation()
    {
        World world = CreateWorld(withConnectedPopulation: false);
        RegionSpeciesPopulation population = GetElkPopulation(world);
        MutationSystem mutationSystem = new(seed: 1);

        population.IsolationSeasons = 12;
        population.DivergenceScore = 2.2;

        for (int season = 0; season < 160 && population.MajorMutationCount == 0; season++)
        {
            population.RecentFoodStress = 1.0;
            population.RecentHuntingPressure = 0.95;
            population.RecentPredationPressure = 0.90;
            population.HabitatSuitability = 0.54;
            population.PopulationCount = Math.Min(population.CarryingCapacity, Math.Max(population.PopulationCount, (int)Math.Round(population.CarryingCapacity * 0.97)));

            mutationSystem.UpdateSeason(world);
        }

        Assert.True(population.MajorMutationCount > 0);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.SpeciesPopulationMajorMutation);
        Assert.True(population.DivergenceScore >= 2.4);
    }

    [Fact]
    public void MigrationShock_IncreasesHabitatMismatchPressure_InSameSeason()
    {
        World withExchangeWorld = CreateWorld();
        World noExchangeWorld = CreateWorld();
        MutationSystem mutationSystem = new(seed: 14);
        EcosystemSystem ecosystemSystem = new();

        RegionSpeciesPopulation exchangeSource = GetElkPopulation(withExchangeWorld);
        RegionSpeciesPopulation exchangeTarget = withExchangeWorld.Regions[1].GetSpeciesPopulation(4)!;
        exchangeSource.PopulationCount = 150;
        exchangeSource.RecentFoodStress = 1.0;
        exchangeTarget.PopulationCount = 0;
        exchangeTarget.BaseHabitatSuitability = 0.56;
        exchangeTarget.HabitatSuitability = 0.58;

        RegionSpeciesPopulation noExchangeTarget = noExchangeWorld.Regions[1].GetSpeciesPopulation(4)!;
        noExchangeTarget.PopulationCount = 18;
        noExchangeTarget.BaseHabitatSuitability = 0.56;
        noExchangeTarget.HabitatSuitability = 0.58;

        ecosystemSystem.UpdateSeason(withExchangeWorld);
        mutationSystem.UpdateSeason(withExchangeWorld);

        mutationSystem.UpdateSeason(noExchangeWorld);

        Assert.True(exchangeTarget.ReceivedMigrantsThisSeason);
        Assert.True(exchangeTarget.HabitatMismatchMutationPressure > noExchangeTarget.HabitatMismatchMutationPressure);
        Assert.True(exchangeTarget.HabitatMismatchMutationPressure >= 0.10);
    }

    [Fact]
    public void ExchangeReducesIsolation_WhenPopulationRejoinsRegionalFlow()
    {
        World world = CreateWorld();
        MutationSystem mutationSystem = new(seed: 19);
        EcosystemSystem ecosystemSystem = new();
        RegionSpeciesPopulation sourcePopulation = GetElkPopulation(world);
        RegionSpeciesPopulation targetPopulation = world.Regions[1].GetSpeciesPopulation(4)!;

        sourcePopulation.PopulationCount = 150;
        sourcePopulation.IsolationSeasons = 10;
        targetPopulation.PopulationCount = 0;
        targetPopulation.IsolationSeasons = 10;

        ecosystemSystem.UpdateSeason(world);
        mutationSystem.UpdateSeason(world);

        Assert.True(sourcePopulation.SentMigrantsThisSeason || targetPopulation.ReceivedMigrantsThisSeason);
        Assert.Equal(7, targetPopulation.IsolationSeasons);
    }

    [Fact]
    public void AdaptationMilestone_IsReachable_ForLongRunMismatchRecovery()
    {
        World world = CreateWorld(withConnectedPopulation: false);
        RegionSpeciesPopulation population = GetElkPopulation(world);
        MutationSystem mutationSystem = new(seed: 31);

        population.BaseHabitatSuitability = 0.61;
        population.HabitatSuitability = 0.80;
        population.HabitatMismatchMutationPressure = 0.72;
        population.DivergenceScore = 1.20;
        population.ClimateToleranceOffset = 0.14;
        population.DietFlexibilityOffset = 0.12;
        population.PopulationCount = 28;
        population.CarryingCapacity = 80;

        mutationSystem.UpdateSeason(world);

        Assert.True(population.RegionAdaptationRecorded);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.SpeciesPopulationAdaptedToRegion);
    }

    [Fact]
    public void AdaptationMilestone_DoesNotSpam_AfterBeingRecorded()
    {
        World world = CreateWorld(withConnectedPopulation: false);
        RegionSpeciesPopulation population = GetElkPopulation(world);
        MutationSystem mutationSystem = new(seed: 37);

        population.BaseHabitatSuitability = 0.61;
        population.HabitatSuitability = 0.80;
        population.HabitatMismatchMutationPressure = 0.72;
        population.DivergenceScore = 1.10;
        population.ClimateToleranceOffset = 0.14;
        population.DietFlexibilityOffset = 0.12;
        population.PopulationCount = 28;
        population.CarryingCapacity = 80;

        mutationSystem.UpdateSeason(world);
        mutationSystem.UpdateSeason(world);
        mutationSystem.UpdateSeason(world);

        Assert.Equal(1, world.Events.Count(evt => evt.Type == WorldEventType.SpeciesPopulationAdaptedToRegion));
    }

    [Fact]
    public void AdaptationMilestone_RequiresRealAncestralMismatch_AndTraitDrivenRecovery()
    {
        World world = CreateWorld(withConnectedPopulation: false);
        RegionSpeciesPopulation population = GetElkPopulation(world);
        MutationSystem mutationSystem = new(seed: 41);

        population.BaseHabitatSuitability = 0.86;
        population.HabitatSuitability = 0.93;
        population.HabitatMismatchMutationPressure = 0.85;
        population.DivergenceScore = 1.30;
        population.ClimateToleranceOffset = 0.02;
        population.DietFlexibilityOffset = 0.02;
        population.PopulationCount = 35;
        population.CarryingCapacity = 80;

        mutationSystem.UpdateSeason(world);

        Assert.False(population.RegionAdaptationRecorded);
        Assert.DoesNotContain(world.Events, evt => evt.Type == WorldEventType.SpeciesPopulationAdaptedToRegion);
    }

    [Fact]
    public void MutatedRegionalTraits_ChangeHuntingOutcome()
    {
        World baselineWorld = CreateWorld();
        World evolvedWorld = CreateWorld();
        HuntingSystem huntingSystem = new();

        Region baselineRegion = baselineWorld.Regions[0];
        Region evolvedRegion = evolvedWorld.Regions[0];
        Polity baselinePolity = CreateSettledPolity(baselineWorld, baselineRegion);
        Polity evolvedPolity = CreateSettledPolity(evolvedWorld, evolvedRegion);
        baselineWorld.Polities.Add(baselinePolity);
        evolvedWorld.Polities.Add(evolvedPolity);

        RegionSpeciesPopulation baselinePopulation = GetElkPopulation(baselineWorld);
        RegionSpeciesPopulation evolvedPopulation = GetElkPopulation(evolvedWorld);
        baselineRegion.GetOrCreateSpeciesPopulation(6).PopulationCount = 0;
        evolvedRegion.GetOrCreateSpeciesPopulation(6).PopulationCount = 0;
        baselinePopulation.PopulationCount = 90;
        evolvedPopulation.PopulationCount = 90;
        baselinePopulation.CarryingCapacity = 120;
        evolvedPopulation.CarryingCapacity = 120;
        baselinePopulation.HabitatSuitability = 1.0;
        evolvedPopulation.HabitatSuitability = 1.0;

        evolvedPopulation.EnduranceOffset = 0.24;
        evolvedPopulation.SocialityOffset = 0.18;
        evolvedPopulation.AggressionOffset = 0.16;
        evolvedPopulation.IntelligenceOffset = 0.10;

        huntingSystem.UpdateSeason(baselineWorld);
        huntingSystem.UpdateSeason(evolvedWorld);

        Assert.True(evolvedPopulation.PopulationCount > baselinePopulation.PopulationCount);
        Assert.True(evolvedPolity.FoodStores < baselinePolity.FoodStores);
    }

    private static World CreateWorld(bool withConnectedPopulation = true)
    {
        World world = new(new WorldTime(5, 3));
        Region home = new(0, "Stone Valley")
        {
            Fertility = 0.65,
            WaterAvailability = 0.60,
            PlantBiomass = 600,
            AnimalBiomass = 240,
            MaxPlantBiomass = 1000,
            MaxAnimalBiomass = 400
        };
        Region neighbor = new(1, "Northreach")
        {
            Fertility = 0.42,
            WaterAvailability = 0.34,
            PlantBiomass = 420,
            AnimalBiomass = 160,
            MaxPlantBiomass = 900,
            MaxAnimalBiomass = 360
        };

        if (withConnectedPopulation)
        {
            home.AddConnection(neighbor.Id);
            neighbor.AddConnection(home.Id);
        }

        world.Regions.Add(home);
        world.Regions.Add(neighbor);

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

        world.Species.First(species => species.Id == 0).DietSpeciesIds.AddRange([3, 4, 6]);
        world.Species.First(species => species.Id == 4).DietSpeciesIds.Add(3);
        world.Species.First(species => species.Id == 6).DietSpeciesIds.Add(4);

        EcosystemSystem ecosystemSystem = new();
        ecosystemSystem.InitializeRegionalPopulations(world);

        RegionSpeciesPopulation homeElk = home.GetOrCreateSpeciesPopulation(4);
        homeElk.PopulationCount = 96;
        homeElk.CarryingCapacity = 120;
        homeElk.HabitatSuitability = 0.92;

        RegionSpeciesPopulation neighborElk = neighbor.GetOrCreateSpeciesPopulation(4);
        neighborElk.PopulationCount = withConnectedPopulation ? 42 : 0;
        neighborElk.CarryingCapacity = 80;
        neighborElk.HabitatSuitability = 0.56;

        return world;
    }

    private static RegionSpeciesPopulation GetElkPopulation(World world)
        => world.Regions[0].GetSpeciesPopulation(4)!;

    private static Polity CreateSettledPolity(World world, Region region)
        => new(10, "Riverwatch Clan", speciesId: 0, regionId: region.Id, population: 120)
        {
            SettlementStatus = SettlementStatus.Settled,
            SettlementCount = 2
        };
}
