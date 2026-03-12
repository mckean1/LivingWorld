using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;
using LivingWorld.Systems;
using Xunit;

namespace LivingWorld.Tests;

public sealed class DomesticationSystemTests
{
    [Fact]
    public void RepeatedHuntingAndCompatibleTraits_CreateAnimalDomesticationCandidate()
    {
        World world = CreateWorld();
        Polity polity = CreatePolity(world);
        Settlement settlement = polity.Settlements[0];
        Species herdAnimal = world.Species.First(species => species.Id == 2);

        polity.RecordSuccessfulHunt(herdAnimal.Id);
        polity.RecordSuccessfulHunt(herdAnimal.Id);
        polity.IncreaseDomesticationInterest(herdAnimal.Id, 0.24);
        settlement.YearsEstablished = 2;

        DomesticationSystem system = new(new DomesticationSettings
        {
            AnimalCandidateSuitabilityThreshold = 0.45,
            MinimumSuccessfulHuntsForCandidate = 2
        });

        system.UpdateMonthlyKnowledgeAndSources(world);

        Assert.True(polity.HasDiscovery($"species-domestication-candidate:{herdAnimal.Id}"));
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.SpeciesDomesticationCandidateIdentified);
    }

    [Fact]
    public void UnsuitablePredators_DoNotBecomeDomesticationCandidates()
    {
        World world = CreateWorld();
        Polity polity = CreatePolity(world);
        Settlement settlement = polity.Settlements[0];
        Species predator = world.Species.First(species => species.Id == 3);

        polity.RecordSuccessfulHunt(predator.Id);
        polity.RecordSuccessfulHunt(predator.Id);
        polity.IncreaseDomesticationInterest(predator.Id, 0.40);
        settlement.YearsEstablished = 3;

        DomesticationSystem system = new(new DomesticationSettings
        {
            AnimalCandidateSuitabilityThreshold = 0.54,
            MinimumSuccessfulHuntsForCandidate = 2
        });

        system.UpdateMonthlyKnowledgeAndSources(world);

        Assert.False(polity.HasDiscovery($"species-domestication-candidate:{predator.Id}"));
        Assert.DoesNotContain(world.Events, evt =>
            evt.Type == WorldEventType.SpeciesDomesticationCandidateIdentified
            && evt.Metadata.TryGetValue("targetSpeciesId", out string? speciesId)
            && speciesId == predator.Id.ToString());
    }

    [Fact]
    public void FamiliarUsefulPlant_CanBecomeCultivableCrop_AndImprovesManagedFood()
    {
        World world = CreateWorld();
        Polity polity = CreatePolity(world);
        Settlement settlement = polity.Settlements[0];
        Species cropPlant = world.Species.First(species => species.Id == 4);

        polity.LearnAdvancement(AdvancementId.Agriculture);
        polity.IncreaseCultivationFamiliarity(cropPlant.Id, 0.45);
        settlement.YearsEstablished = 2;
        settlement.CultivatedLand = 1.2;

        DomesticationSystem system = new(new DomesticationSettings
        {
            PlantDiscoveryThreshold = 0.20,
            CropEstablishmentThreshold = 0.30
        });

        system.UpdateMonthlyKnowledgeAndSources(world);

        Assert.True(polity.HasDiscovery($"species-cultivable:{cropPlant.Id}"));
        Assert.NotNull(settlement.GetCultivatedCrop(cropPlant.Id));
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.PlantCultivationDiscovered);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.CropEstablished);

        AgricultureSystem agriculture = new(system);
        double foodBefore = polity.FoodStores;

        agriculture.ProduceFarmFood(world);

        Assert.True(polity.FoodStores > foodBefore);
        Assert.True(settlement.ManagedCropFoodThisMonth > 0);
        Assert.True(polity.AnnualFoodManaged > 0);
    }

    [Fact]
    public void ManagedHerds_ProduceFoodAfterDomesticationSucceeds()
    {
        World world = CreateWorld();
        Polity polity = CreatePolity(world);
        Settlement settlement = polity.Settlements[0];
        Species herdAnimal = world.Species.First(species => species.Id == 2);

        polity.RecordSuccessfulHunt(herdAnimal.Id);
        polity.RecordSuccessfulHunt(herdAnimal.Id);
        polity.RecordSuccessfulHunt(herdAnimal.Id);
        polity.IncreaseDomesticationInterest(herdAnimal.Id, 0.40);
        polity.LearnAdvancement(AdvancementId.FoodStorage);
        settlement.YearsEstablished = 2;

        DomesticationSystem system = new(new DomesticationSettings
        {
            AnimalCandidateSuitabilityThreshold = 0.45,
            HerdEstablishmentSuitabilityThreshold = 0.50,
            HerdEstablishmentInterestThreshold = 0.30,
            MinimumSuccessfulHuntsForCandidate = 2,
            MinimumSuccessfulHuntsForHerd = 3
        });

        system.UpdateMonthlyKnowledgeAndSources(world);
        double foodBefore = polity.FoodStores;
        system.ProduceManagedAnimalFood(world);

        Assert.NotEmpty(settlement.ManagedHerds);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.AnimalDomesticated);
        Assert.True(settlement.ManagedAnimalFoodThisMonth > 0);
        Assert.True(polity.FoodStores > foodBefore);
    }

    [Fact]
    public void AnnualManagedFoodShare_CanEmitFoodStabilityTurningPoint()
    {
        World world = CreateWorld(month: 12);
        Polity polity = CreatePolity(world);
        Settlement settlement = polity.Settlements[0];

        settlement.ManagedHerds.Add(new ManagedHerd(2, "Domestic River Goat", 5, 1, 10, 0.75, 1.0, 0.80, 0.20));
        settlement.CultivatedCrops.Add(new CultivatedCrop(4, "Tallgrass grain", 5, 1, 0.15, 0.10, 0.08));
        polity.AnnualFoodConsumed = 100;
        polity.AnnualFoodManaged = 28;
        polity.StarvationMonthsThisYear = 0;

        DomesticationSystem system = new(new DomesticationSettings
        {
            AnnualManagedFoodStabilityShare = 0.18
        });

        system.UpdateAnnualManagedFood(world);

        WorldEvent stabilityEvent = Assert.Single(world.Events, evt => evt.Type == WorldEventType.AgricultureStabilizedFoodSupply);
        Assert.Equal("managed_food_supply_established", stabilityEvent.Reason);
        Assert.Equal("River Clan established managed crops and herds.", stabilityEvent.Narrative);
        Assert.True(polity.ManagedFoodSupplyEstablished);
    }

    [Fact]
    public void AnnualManagedFoodShare_OnlyEmitsOnTransitionIntoEstablishedState()
    {
        World world = CreateWorld(month: 12);
        Polity polity = CreatePolity(world);
        Settlement settlement = polity.Settlements[0];
        settlement.ManagedHerds.Add(new ManagedHerd(2, "Domestic River Goat", 5, 1, 10, 0.75, 1.0, 0.80, 0.20));
        settlement.CultivatedCrops.Add(new CultivatedCrop(4, "Tallgrass grain", 5, 1, 0.15, 0.10, 0.08));
        polity.AnnualFoodConsumed = 100;
        polity.AnnualFoodManaged = 28;

        DomesticationSystem system = new(new DomesticationSettings
        {
            AnnualManagedFoodStabilityShare = 0.18
        });

        system.UpdateAnnualManagedFood(world);
        system.UpdateAnnualManagedFood(world);

        Assert.Equal(1, world.Events.Count(evt => evt.Type == WorldEventType.AgricultureStabilizedFoodSupply));
    }

    [Fact]
    public void AnnualManagedFoodShare_CanEmitAgainAfterLosingAndReestablishingState()
    {
        World world = CreateWorld(month: 12);
        Polity polity = CreatePolity(world);
        Settlement settlement = polity.Settlements[0];
        settlement.ManagedHerds.Add(new ManagedHerd(2, "Domestic River Goat", 5, 1, 10, 0.75, 1.0, 0.80, 0.20));
        settlement.CultivatedCrops.Add(new CultivatedCrop(4, "Tallgrass grain", 5, 1, 0.15, 0.10, 0.08));

        DomesticationSystem system = new(new DomesticationSettings
        {
            AnnualManagedFoodStabilityShare = 0.18
        });

        polity.AnnualFoodConsumed = 100;
        polity.AnnualFoodManaged = 28;
        system.UpdateAnnualManagedFood(world);

        polity.AnnualFoodManaged = 6;
        system.UpdateAnnualManagedFood(world);
        Assert.False(polity.ManagedFoodSupplyEstablished);

        for (int month = 0; month < 12; month++)
        {
            world.Time.AdvanceOneMonth();
        }
        polity.AnnualFoodManaged = 30;
        system.UpdateAnnualManagedFood(world);

        Assert.Equal(2, world.Events.Count(evt => evt.Type == WorldEventType.AgricultureStabilizedFoodSupply));
    }

    private static World CreateWorld(int month = 6)
    {
        World world = new(new WorldTime(5, month));
        Region valley = new(0, "Red Valley")
        {
            Fertility = 0.78,
            WaterAvailability = 0.72,
            PlantBiomass = 700,
            AnimalBiomass = 220,
            MaxPlantBiomass = 1000,
            MaxAnimalBiomass = 400
        };

        world.Regions.Add(valley);
        world.Species.Add(new Species(1, "Humans", 0.8, 0.7)
        {
            IsSapient = true,
            TrophicRole = TrophicRole.Omnivore
        });
        world.Species.Add(new Species(2, "River Goat", 0.2, 0.3)
        {
            TrophicRole = TrophicRole.Herbivore,
            MeatYield = 14,
            DomesticationAffinity = 0.72,
            MigrationCapability = 0.12,
            FertilityPreference = 0.64,
            WaterPreference = 0.58
        });
        world.Species.Add(new Species(3, "Ridge Lion", 0.1, 0.1)
        {
            TrophicRole = TrophicRole.Apex,
            MeatYield = 30,
            DomesticationAffinity = 0.06,
            MigrationCapability = 0.36,
            FertilityPreference = 0.30,
            WaterPreference = 0.28
        });
        world.Species.Add(new Species(4, "Tallgrass", 0.05, 0.02)
        {
            TrophicRole = TrophicRole.Producer,
            CultivationAffinity = 0.82,
            FertilityPreference = 0.76,
            WaterPreference = 0.52
        });

        RegionSpeciesPopulation goatPopulation = valley.GetOrCreateSpeciesPopulation(2);
        goatPopulation.PopulationCount = 40;
        goatPopulation.CarryingCapacity = 90;

        RegionSpeciesPopulation lionPopulation = valley.GetOrCreateSpeciesPopulation(3);
        lionPopulation.PopulationCount = 18;
        lionPopulation.CarryingCapacity = 28;

        RegionSpeciesPopulation grassPopulation = valley.GetOrCreateSpeciesPopulation(4);
        grassPopulation.PopulationCount = 180;
        grassPopulation.CarryingCapacity = 260;

        return world;
    }

    private static Polity CreatePolity(World world)
    {
        Polity polity = new(7, "River Clan", 1, 0, 120);
        polity.EstablishFirstSettlement(0, "Hill Camp");
        polity.SettlementStatus = SettlementStatus.Settled;
        polity.LearnAdvancement(AdvancementId.SeasonalPlanning);
        world.Polities.Add(polity);
        return polity;
    }
}
