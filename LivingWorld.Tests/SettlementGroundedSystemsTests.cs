using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;
using LivingWorld.Systems;
using Xunit;

namespace LivingWorld.Tests;

public sealed class SettlementGroundedSystemsTests
{
    [Fact]
    public void Hunting_UsesActualSettlementRegions_InsteadOfMultiplyingHomeRegion()
    {
        World world = CreateWorld();
        Region north = world.Regions[0];
        Region south = world.Regions[1];

        RegionSpeciesPopulation elk = north.GetOrCreateSpeciesPopulation(4);
        elk.PopulationCount = 70;
        elk.CarryingCapacity = 90;
        elk.HabitatSuitability = 1.0;

        RegionSpeciesPopulation boar = south.GetOrCreateSpeciesPopulation(5);
        boar.PopulationCount = 60;
        boar.CarryingCapacity = 80;
        boar.HabitatSuitability = 1.0;

        Polity polity = CreateSettledPolity(world, 9, "Riverwatch Clan", 140, north.Id);
        polity.AddSettlement(south.Id, "Southfen Outpost");
        world.Polities.Add(polity);

        HuntingSystem system = new();
        system.UpdateSeason(world);

        Assert.True(elk.PopulationCount < 70);
        Assert.True(boar.PopulationCount < 60);
        Assert.True(polity.FoodStores > 0);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.HuntingSuccess && evt.RegionId == north.Id);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.HuntingSuccess && evt.RegionId == south.Id);
    }

    [Fact]
    public void Agriculture_RespectsRegionalCapacityAcrossMultipleSettlements()
    {
        World world = CreateWorld();
        Region region = world.Regions[0];

        Polity first = CreateFarmerPolity(world, 10, "Amber Reach Folk", 120, region.Id);
        first.AddSettlement(region.Id, "Amber Reach Outpost");
        Polity second = CreateFarmerPolity(world, 11, "Stonewater Kin", 95, region.Id);
        second.AddSettlement(region.Id, "Stonewater Outpost");

        world.Polities.Add(first);
        world.Polities.Add(second);

        AgricultureSystem system = new();
        system.ProduceFarmFood(world);

        double regionalCapacity = CalculateRegionalArableCapacity(region);
        double allocatedLand = world.Polities.SelectMany(polity => polity.Settlements)
            .Where(settlement => settlement.RegionId == region.Id)
            .Sum(settlement => settlement.CultivatedLand);

        Assert.True(allocatedLand <= regionalCapacity + 0.001);
        Assert.True(first.CultivatedLand > 0);
        Assert.True(second.CultivatedLand > 0);
        Assert.True(first.FoodFarmedThisMonth > 0);
        Assert.True(second.FoodFarmedThisMonth > 0);
    }

    [Fact]
    public void Agriculture_UsesSettlementRegions_WhenASettlementNetworkSpansRegions()
    {
        World world = CreateWorld();
        Region fertile = world.Regions[0];
        Region poor = world.Regions[1];
        poor.Fertility = 0.18;
        poor.WaterAvailability = 0.16;

        Polity polity = CreateFarmerPolity(world, 12, "Marshfield Circle", 140, fertile.Id);
        Settlement poorSettlement = polity.AddSettlement(poor.Id, "Drystep Camp");
        world.Polities.Add(polity);

        AgricultureSystem system = new();
        system.ProduceFarmFood(world);

        Settlement fertileSettlement = polity.GetPrimarySettlementInRegion(fertile.Id)!;
        Assert.True(fertileSettlement.CultivatedLand > poorSettlement.CultivatedLand);
        Assert.True(polity.FoodFarmedThisMonth > 0);
    }

    [Fact]
    public void Trade_TransfersSurplus_AndHandlesMissingPartnersWithoutCrashing()
    {
        World world = CreateWorld();
        Region sourceRegion = world.Regions[0];
        Region targetRegion = world.Regions[1];

        Polity exporter = CreateSettledPolity(world, 20, "Northhold", 100, sourceRegion.Id);
        Polity importer = CreateSettledPolity(world, 21, "Southhold", 100, targetRegion.Id);
        exporter.FoodStores = 180;
        importer.FoodStores = 10;
        exporter.FoodNeededThisMonth = 60;
        importer.FoodNeededThisMonth = 60;

        world.Polities.Add(exporter);
        world.Polities.Add(importer);

        TradeSystem system = new();
        system.UpdateTrade(world);

        Assert.True(importer.FoodStores > 10);
        Assert.True(exporter.FoodStores < 180);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.TradeTransfer);

        world.Polities.Remove(importer);

        for (int month = 0; month < 18; month++)
        {
            system.UpdateTrade(world);
            world.Time.AdvanceOneMonth();
        }

        system.UpdateAnnualTrade(world);
    }

    [Fact]
    public void SettlementFoodState_UsesMonthlyBalanceThresholds()
    {
        Settlement surplus = new(1001, 1, 0, "Granary");
        surplus.FoodProduced = 80;
        surplus.FoodStored = 40;
        surplus.FoodRequired = 90;

        Settlement stable = new(1002, 1, 0, "Commons");
        stable.FoodProduced = 70;
        stable.FoodStored = 10;
        stable.FoodRequired = 85;

        Settlement deficit = new(1003, 1, 0, "Leanfield");
        deficit.FoodProduced = 40;
        deficit.FoodStored = 10;
        deficit.FoodRequired = 70;

        Settlement starving = new(1004, 1, 0, "Bleakhold");
        starving.FoodProduced = 20;
        starving.FoodStored = 5;
        starving.FoodRequired = 60;

        Assert.Equal(FoodState.Surplus, surplus.CalculateFoodState());
        Assert.Equal(FoodState.Stable, stable.CalculateFoodState());
        Assert.Equal(FoodState.Deficit, deficit.CalculateFoodState());
        Assert.Equal(FoodState.Starving, starving.CalculateFoodState());
    }

    [Fact]
    public void SettlementRedistribution_PrioritizesSameRegionBeforeNeighbors_AndAppliesTransportLoss()
    {
        World world = CreateWorld();
        Region distant = new(2, "High Ridge")
        {
            Fertility = 0.30,
            WaterAvailability = 0.28,
            PlantBiomass = 300,
            AnimalBiomass = 120,
            MaxPlantBiomass = 700,
            MaxAnimalBiomass = 240
        };
        world.Regions[1].AddConnection(distant.Id);
        distant.AddConnection(world.Regions[1].Id);
        world.Regions.Add(distant);

        Polity polity = CreateSettledPolity(world, 80, "Breadbasket League", 140, world.Regions[0].Id);
        Settlement sender = polity.Settlements[0];
        sender.CultivatedLand = 20;
        Settlement sameRegionReceiver = polity.AddSettlement(world.Regions[0].Id, "Near Camp");
        Settlement neighboringReceiver = polity.AddSettlement(world.Regions[1].Id, "Hill Camp");

        polity.FoodNeededThisMonth = 140;
        polity.FoodFarmedThisMonth = 200;
        polity.FoodGatheredThisMonth = 0;
        polity.FoodStores = 0;
        world.Polities.Add(polity);

        SettlementFoodRedistributionSystem system = new();
        system.UpdateMonthlyFoodStatesAndRedistribution(world);

        Assert.Equal(FoodState.Stable, sameRegionReceiver.FoodState);
        Assert.True(sameRegionReceiver.LastAidReceived >= 19.9);
        Assert.True(neighboringReceiver.LastAidReceived > 0);
        Assert.True(neighboringReceiver.LastAidReceived < 5.0);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.FoodAidSent && evt.SettlementId == sameRegionReceiver.Id);
    }

    [Fact]
    public void SettlementRedistribution_EmitsFamineRelief_WhenAidPreventsStarvation()
    {
        World world = CreateWorld();
        Polity polity = CreateSettledPolity(world, 81, "Stone Valley", 140, world.Regions[0].Id);
        Settlement sender = polity.Settlements[0];
        sender.CultivatedLand = 22;
        Settlement starvingReceiver = polity.AddSettlement(world.Regions[1].Id, "Hill Camp");

        polity.FoodNeededThisMonth = 140;
        polity.FoodFarmedThisMonth = 220;
        polity.FoodGatheredThisMonth = 0;
        polity.FoodStores = 0;
        world.Polities.Add(polity);

        SettlementFoodRedistributionSystem system = new();
        system.UpdateMonthlyFoodStatesAndRedistribution(world);

        Assert.NotEqual(FoodState.Starving, starvingReceiver.FoodState);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.FamineRelief && evt.SettlementId == starvingReceiver.Id);
    }

    [Fact]
    public void SettlementRedistribution_AidFailure_IsOnlyEmittedWhenStarvationBegins()
    {
        World world = CreateWorld();
        Polity polity = CreateSettledPolity(world, 82, "Stonefen Kin", 140, world.Regions[0].Id);
        Settlement sender = polity.Settlements[0];
        sender.CultivatedLand = 18;
        Settlement starvingReceiver = polity.AddSettlement(world.Regions[1].Id, "Stonefen Hearth");

        polity.FoodNeededThisMonth = 140;
        polity.FoodFarmedThisMonth = 80;
        polity.FoodGatheredThisMonth = 0;
        polity.FoodStores = 0;
        world.Polities.Add(polity);

        SettlementFoodRedistributionSystem system = new();
        system.UpdateMonthlyFoodStatesAndRedistribution(world);
        system.UpdateMonthlyFoodStatesAndRedistribution(world);

        Assert.Equal(1, world.Events.Count(evt => evt.Type == WorldEventType.AidFailed && evt.SettlementId == starvingReceiver.Id));
    }

    [Fact]
    public void SettlementRedistribution_AidFailure_CanEscalateWhenStarvationWorsens()
    {
        World world = CreateWorld();
        Polity polity = CreateSettledPolity(world, 83, "Stonefen Kin", 140, world.Regions[1].Id);
        Settlement starvingSettlement = polity.Settlements[0];
        world.Polities.Add(polity);

        polity.FoodNeededThisMonth = 140;
        polity.FoodFarmedThisMonth = 70;
        polity.FoodGatheredThisMonth = 0;
        polity.FoodStores = 0;

        SettlementFoodRedistributionSystem system = new();
        system.UpdateMonthlyFoodStatesAndRedistribution(world);

        polity.FoodFarmedThisMonth = 10;
        system.UpdateMonthlyFoodStatesAndRedistribution(world);

        Assert.Contains(world.Events, evt =>
            evt.Type == WorldEventType.AidFailed
            && evt.SettlementId == starvingSettlement.Id
            && evt.Reason == "settlement_starvation_worsened_unaided");
    }

    [Fact]
    public void SettlementRedistribution_EmitsRecovery_WhenSettlementLeavesStarvationWithoutNewAid()
    {
        World world = CreateWorld();
        Polity polity = CreateSettledPolity(world, 84, "Stonefen Kin", 140, world.Regions[1].Id);
        Settlement starvingSettlement = polity.Settlements[0];
        world.Polities.Add(polity);

        polity.FoodNeededThisMonth = 140;
        polity.FoodFarmedThisMonth = 70;
        polity.FoodGatheredThisMonth = 0;
        polity.FoodStores = 0;

        SettlementFoodRedistributionSystem system = new();
        system.UpdateMonthlyFoodStatesAndRedistribution(world);

        polity.FoodFarmedThisMonth = 180;
        starvingSettlement.FoodStored = 0;
        system.UpdateMonthlyFoodStatesAndRedistribution(world);

        Assert.Contains(world.Events, evt =>
            evt.Type == WorldEventType.FamineRelief
            && evt.SettlementId == starvingSettlement.Id
            && evt.Reason == "settlement_starvation_recovered");
    }

    [Fact]
    public void SettlementRedistribution_DoesNotEmitDuplicateRecovery_InSameYear()
    {
        World world = CreateWorld();
        Polity polity = CreateSettledPolity(world, 85, "Gloam Fen Kin", 140, world.Regions[1].Id);
        Settlement starvingSettlement = polity.Settlements[0];
        world.Polities.Add(polity);

        polity.FoodNeededThisMonth = 140;
        polity.FoodFarmedThisMonth = 70;
        polity.FoodGatheredThisMonth = 0;
        polity.FoodStores = 0;

        SettlementFoodRedistributionSystem system = new();
        system.UpdateMonthlyFoodStatesAndRedistribution(world);

        polity.FoodFarmedThisMonth = 180;
        system.UpdateMonthlyFoodStatesAndRedistribution(world);

        polity.FoodFarmedThisMonth = 70;
        system.UpdateMonthlyFoodStatesAndRedistribution(world);

        polity.FoodFarmedThisMonth = 180;
        system.UpdateMonthlyFoodStatesAndRedistribution(world);

        Assert.Equal(1, world.Events.Count(evt =>
            evt.Type == WorldEventType.FamineRelief
            && evt.SettlementId == starvingSettlement.Id
            && evt.Reason == "settlement_starvation_recovered"));
    }

    [Fact]
    public void Migration_RelocatesSettlementNetworkWithThePolity()
    {
        World world = CreateWorld();
        Region origin = world.Regions[0];
        Region destination = world.Regions[1];
        destination.Fertility = 0.95;
        destination.WaterAvailability = 0.95;
        destination.PlantBiomass = 900;
        destination.AnimalBiomass = 360;

        Polity polity = CreateSettledPolity(world, 30, "Trail Kin", 90, origin.Id);
        polity.AddSettlement(origin.Id, "Trail Kin Outpost");
        polity.FoodStores = 2;
        polity.FoodNeededThisMonth = 90;
        polity.FoodSatisfactionThisMonth = 0.20;
        polity.EventDrivenMigrationPressureBonus = 1.0;
        origin.PlantBiomass = 60;
        origin.AnimalBiomass = 20;

        world.Polities.Add(polity);

        MigrationSystem system = new(seed: 2);
        system.UpdateMigration(world);

        Assert.Equal(destination.Id, polity.RegionId);
        Assert.All(polity.Settlements, settlement => Assert.Equal(destination.Id, settlement.RegionId));
    }

    [Fact]
    public void SettlementSystem_CreatesARealSettlementRecord_WhenFoundingOccurs()
    {
        World world = CreateWorld(month: 12);
        Region region = world.Regions[0];
        Polity polity = new(40, "Field Clan", 0, region.Id, 95)
        {
            FoodStores = 180,
            AnnualFoodNeeded = 120,
            AnnualFoodConsumed = 132,
            YearsSinceFounded = 6,
            YearsInCurrentRegion = 4
        };
        polity.LearnAdvancement(AdvancementId.Agriculture);
        polity.LearnAdvancement(AdvancementId.BasicConstruction);
        polity.LearnAdvancement(AdvancementId.FoodStorage);
        world.Polities.Add(polity);

        SettlementSystem system = new(seed: 1);
        system.UpdateSettlements(world);

        Assert.True(polity.HasSettlements);
        Assert.Equal(region.Id, polity.Settlements[0].RegionId);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.SettlementFounded);
    }

    [Fact]
    public void Fragmentation_CreatesChildPolity_WithoutBrokenSettlementReferences()
    {
        World world = CreateWorld(month: 12);
        Region home = world.Regions[0];
        Polity parent = CreateSettledPolity(world, 50, "Old Vale", 180, home.Id);
        parent.YearsSinceFounded = 8;
        parent.FoodStressYears = 4;
        parent.StarvationMonthsThisYear = 6;
        parent.AnnualFoodNeeded = 100;
        parent.AnnualFoodConsumed = 55;
        world.Polities.Add(parent);

        FragmentationSystem system = new(seed: 1);

        for (int attempt = 0; attempt < 8 && world.Polities.Count == 1; attempt++)
        {
            system.UpdateFragmentation(world);
        }

        Polity child = Assert.Single(world.Polities, polity => polity.Id != parent.Id);
        Assert.Equal(parent.Id, child.ParentPolityId);
        Assert.False(child.HasSettlements);
        Assert.All(parent.Settlements, settlement => Assert.Equal(parent.RegionId, settlement.RegionId));
    }

    [Fact]
    public void PopulationCollapse_ClearsSettlementState()
    {
        World world = CreateWorld(month: 12);
        Polity polity = CreateSettledPolity(world, 60, "Fading Hearth", 2, world.Regions[0].Id);
        polity.AnnualFoodNeeded = 100;
        polity.AnnualFoodConsumed = 0;
        polity.StarvationMonthsThisYear = 8;
        world.Polities.Add(polity);

        PopulationSystem system = new();
        system.UpdatePopulation(world);

        Assert.Equal(0, polity.SettlementCount);
        Assert.False(polity.HasSettlements);
    }

    [Fact]
    public void WorldLookup_ReturnsValidObjects_AndThrowsClearErrorsForMissingIds()
    {
        World world = CreateWorld();
        Polity polity = CreateSettledPolity(world, 70, "Lookup Clan", 75, world.Regions[0].Id);
        world.Polities.Add(polity);

        WorldLookup lookup = new(world);

        Assert.True(lookup.TryGetRegion(world.Regions[0].Id, out Region? region));
        Assert.Equal(world.Regions[0].Name, region!.Name);
        Assert.True(lookup.TryGetPolity(polity.Id, out Polity? resolvedPolity));
        Assert.Equal(polity.Name, resolvedPolity!.Name);
        Assert.Contains("missing region 999", Assert.Throws<InvalidOperationException>(() => lookup.GetRequiredRegion(999, "Lookup test")).Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing species 999", Assert.Throws<InvalidOperationException>(() => lookup.GetRequiredSpecies(999, "Lookup test")).Message, StringComparison.OrdinalIgnoreCase);
    }

    private static World CreateWorld(int month = 3)
    {
        World world = new(new WorldTime(5, month));

        Region north = new(0, "Amber Reach")
        {
            Fertility = 0.75,
            WaterAvailability = 0.72,
            PlantBiomass = 680,
            AnimalBiomass = 260,
            MaxPlantBiomass = 1100,
            MaxAnimalBiomass = 420
        };
        Region south = new(1, "Southfen")
        {
            Fertility = 0.58,
            WaterAvailability = 0.64,
            PlantBiomass = 540,
            AnimalBiomass = 210,
            MaxPlantBiomass = 980,
            MaxAnimalBiomass = 380
        };

        north.AddConnection(south.Id);
        south.AddConnection(north.Id);

        world.Regions.Add(north);
        world.Regions.Add(south);

        world.Species.Add(new Species(0, "Humans", 0.82, 0.74)
        {
            IsSapient = true,
            TrophicRole = TrophicRole.Omnivore,
            FertilityPreference = 0.58,
            WaterPreference = 0.56,
            PlantBiomassAffinity = 0.40,
            AnimalBiomassAffinity = 0.45,
            BaseCarryingCapacityFactor = 0.95,
            MigrationCapability = 0.24,
            ExpansionPressure = 0.18,
            MeatYield = 10
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
            HuntingDifficulty = 0.28,
            HuntingDanger = 0.22
        });
        world.Species.Add(new Species(5, "Fen Boar", 0.18, 0.22)
        {
            TrophicRole = TrophicRole.Herbivore,
            FertilityPreference = 0.54,
            WaterPreference = 0.60,
            PlantBiomassAffinity = 0.66,
            AnimalBiomassAffinity = 0.08,
            BaseCarryingCapacityFactor = 0.92,
            MigrationCapability = 0.18,
            ExpansionPressure = 0.17,
            MeatYield = 18,
            HuntingDifficulty = 0.24,
            HuntingDanger = 0.18
        });

        world.Species.First(species => species.Id == 0).DietSpeciesIds.AddRange([4, 5]);

        return world;
    }

    private static Polity CreateSettledPolity(World world, int id, string name, int population, int regionId)
    {
        Polity polity = new(id, name, 0, regionId, population)
        {
            AnnualFoodNeeded = population,
            AnnualFoodConsumed = population,
            YearsSinceFounded = 6,
            YearsInCurrentRegion = 4
        };
        polity.EstablishFirstSettlement(regionId, $"{world.Regions.First(region => region.Id == regionId).Name} Hearth");
        polity.SettlementStatus = SettlementStatus.Settled;
        return polity;
    }

    private static Polity CreateFarmerPolity(World world, int id, string name, int population, int regionId)
    {
        Polity polity = CreateSettledPolity(world, id, name, population, regionId);
        polity.LearnAdvancement(AdvancementId.Agriculture);
        polity.LearnAdvancement(AdvancementId.BasicConstruction);
        polity.LearnAdvancement(AdvancementId.FoodStorage);
        return polity;
    }

    private static double CalculateRegionalArableCapacity(Region region)
    {
        double quality = (region.Fertility * 0.70) + (region.WaterAvailability * 0.30);
        return region.CarryingCapacity * quality * 0.60;
    }
}
