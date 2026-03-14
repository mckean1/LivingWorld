using LivingWorld.Core;
using LivingWorld.Generation;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;
using LivingWorld.Systems;
using Xunit;

namespace LivingWorld.Tests;

public sealed class SocialEmergenceSystemTests
{
    [Fact]
    public void SentientPopulationActivation_CreatesGroupFromCapableLineage()
    {
        World world = CreateWorld();
        SocialEmergenceSystem system = CreateSystem();

        system.UpdateYear(world);

        SentientPopulationGroup group = Assert.Single(world.SentientGroups);
        Assert.Equal(10, group.SourceLineageId);
        Assert.True(group.PopulationCount >= 28);
        Assert.Contains(world.CivilizationalHistory, evt => evt.Type == CivilizationalHistoryEventType.SentientActivation);
    }

    [Fact]
    public void PersistentGroupFormation_RegistersUnderViableConditions()
    {
        World world = CreateWorld();
        SocialEmergenceSystem system = CreateSystem();

        for (int year = 0; year < 6; year++)
        {
            system.UpdateYear(world);
            world.Time.Reset(world.Time.Year + 1, 1);
        }

        Assert.Contains(world.CivilizationalHistory, evt => evt.Type == CivilizationalHistoryEventType.PersistentGroupFormation);
    }

    [Fact]
    public void SocietyFormation_RequiresContinuityAndKnowledge()
    {
        World world = CreateWorld();
        SocialEmergenceSystem system = CreateSystem(new WorldGenerationSettings
        {
            SentientActivationMinimumPopulation = 40,
            SentientActivationMinimumSupport = 0.40,
            PersistentGroupCohesionThreshold = 0.35,
            SocietyFormationContinuityYears = 2,
            SocietyFormationIdentityThreshold = 0.20
        });

        for (int year = 0; year < 4; year++)
        {
            system.UpdateYear(world);
            world.Time.Reset(world.Time.Year + 1, 1);
        }

        EmergingSociety society = Assert.Single(world.Societies);
        Assert.Equal(10, society.LineageId);
        Assert.Contains(world.CivilizationalHistory, evt => evt.Type == CivilizationalHistoryEventType.SocietyFormation);
    }

    [Fact]
    public void CulturalKnowledge_PersistsIntoSociety()
    {
        World world = CreateWorld();
        SocialEmergenceSystem system = CreateSystem(new WorldGenerationSettings
        {
            SentientActivationMinimumPopulation = 40,
            SentientActivationMinimumSupport = 0.40,
            PersistentGroupCohesionThreshold = 0.35,
            SocietyFormationContinuityYears = 2,
            SocietyFormationIdentityThreshold = 0.20
        });

        for (int year = 0; year < 4; year++)
        {
            system.UpdateYear(world);
            world.Time.Reset(world.Time.Year + 1, 1);
        }

        EmergingSociety society = Assert.Single(world.Societies);
        Assert.True(society.CulturalKnowledge.Count >= 3);
        Assert.Contains(society.CulturalKnowledge.Values, discovery => discovery.Category == CulturalDiscoveryCategory.Geography);
    }

    [Fact]
    public void SettlementFounding_AndAbandonment_AreTracked()
    {
        World world = CreateWorld();
        SocialEmergenceSystem system = CreateSystem(new WorldGenerationSettings
        {
            SentientActivationMinimumPopulation = 40,
            SentientActivationMinimumSupport = 0.40,
            PersistentGroupCohesionThreshold = 0.35,
            SocietyFormationContinuityYears = 1,
            SocietyFormationIdentityThreshold = 0.20,
            SettlementIntentReturnThreshold = 1,
            SettlementFoundingPressureThreshold = 0.10
        });

        system.UpdateYear(world);
        world.Time.Reset(121, 1);
        system.UpdateYear(world);

        SocialSettlement settlement = Assert.Single(world.SocialSettlements);
        Assert.False(settlement.IsAbandoned);

        settlement.SettlementViability = 0.05;
        world.Time.Reset(122, 1);
        system.UpdateYear(world);

        Assert.True(settlement.IsAbandoned);
        Assert.Contains(world.CivilizationalHistory, evt => evt.Type == CivilizationalHistoryEventType.SettlementAbandoned);
    }

    [Fact]
    public void EarlySubsistenceTransitions_RespondToKnowledgeAndSettlement()
    {
        World world = CreateWorld();
        EmergingSociety society = SeedSociety(world);
        society.SurvivalKnowledge = 0.80;
        society.CulturalKnowledge["species:2:edible"] = new CulturalDiscovery("species:2:edible", "Wild grain is edible", CulturalDiscoveryCategory.FoodSafety, 2, 0);
        society.CulturalKnowledge["species:3:edible"] = new CulturalDiscovery("species:3:edible", "Root bulbs are edible", CulturalDiscoveryCategory.FoodSafety, 3, 0);
        society.CulturalKnowledge["species:4:prey"] = new CulturalDiscovery("species:4:prey", "Herd beasts are good prey", CulturalDiscoveryCategory.SpeciesUse, 4, 0);
        world.SocialSettlements.Add(new SocialSettlement(1)
        {
            FounderSocietyId = society.Id,
            FounderLineageId = society.LineageId,
            RegionId = 0,
            FoundingYear = 120,
            Population = 40,
            SettlementViability = 0.8
        });
        society.SettlementIds.Add(1);

        SocialEmergenceSystem system = CreateSystem();
        system.UpdateYear(world);

        Assert.Equal(SubsistenceMode.FarmingEmergent, society.SubsistenceMode);
    }

    [Fact]
    public void PolityFormation_CreatesEarlyPolityFromViableSociety()
    {
        World world = CreateWorld();
        EmergingSociety society = SeedSociety(world);
        society.Population = 220;
        society.SocialComplexity = 0.72;
        society.SurvivalKnowledge = 0.78;
        society.CulturalKnowledge["a"] = new CulturalDiscovery("a", "Water route", CulturalDiscoveryCategory.Geography, RegionId: 0);
        society.CulturalKnowledge["b"] = new CulturalDiscovery("b", "Safe roots", CulturalDiscoveryCategory.FoodSafety, SpeciesId: 2, RegionId: 0);
        society.CulturalKnowledge["c"] = new CulturalDiscovery("c", "Dangerous predator", CulturalDiscoveryCategory.AnimalBehavior, SpeciesId: 5, RegionId: 0);
        society.CulturalKnowledge["d"] = new CulturalDiscovery("d", "Fertile basin", CulturalDiscoveryCategory.Environment, RegionId: 0);
        world.SocialSettlements.Add(new SocialSettlement(1)
        {
            FounderSocietyId = society.Id,
            FounderLineageId = society.LineageId,
            RegionId = 0,
            FoundingYear = 120,
            Population = 60,
            SettlementViability = 0.82,
            StorageLevel = 0.5
        });
        society.SettlementIds.Add(1);

        SocialEmergenceSystem system = CreateSystem(new WorldGenerationSettings
        {
            PolityFormationMinimumPopulation = 140,
            PolityFormationMinimumKnowledgeCount = 4,
            PolityFormationComplexityThreshold = 0.55
        });
        system.UpdateYear(world);

        Polity polity = Assert.Single(world.Polities);
        Assert.Equal(society.Id, polity.FounderSocietyId);
        Assert.True(polity.HasSettlements);
    }

    [Fact]
    public void Fragmentation_PreservesSocietyContinuity()
    {
        World world = CreateWorld();
        EmergingSociety society = SeedSociety(world);
        society.Population = 260;
        society.Cohesion = 0.30;

        SocialEmergenceSystem system = CreateSystem();
        system.UpdateYear(world);

        Assert.True(world.Societies.Count >= 2);
        Assert.Contains(world.Societies, candidate => candidate.ParentSocietyId == society.Id);
        Assert.Contains(world.CivilizationalHistory, evt => evt.Type == CivilizationalHistoryEventType.Fragmentation);
    }

    [Fact]
    public void CandidateViabilityTracking_FlagsStablePolity()
    {
        World world = CreateWorld();
        Polity polity = new(1, "Stone Basin People", 1, 0, 180, lineageId: 10)
        {
            FounderSocietyId = 1,
            YearsSinceFounded = 6,
            CurrentPressureSummary = "stable growth"
        };
        polity.EstablishFirstSettlement(0, "Stone Basin Hearth");
        polity.AddDiscovery(new CulturalDiscovery("route", "River route", CulturalDiscoveryCategory.Geography, RegionId: 0));
        world.Polities.Add(polity);

        SocialEmergenceSystem system = CreateSystem();
        system.UpdateYear(world);

        FocalCandidateProfile candidate = Assert.Single(world.FocalCandidateProfiles);
        Assert.True(candidate.IsViable);
        Assert.Equal(StabilityBand.Strong, candidate.StabilityBand);
    }

    [Fact]
    public void PhaseCReadinessReport_RequiresSocialAndPoliticalMaturity()
    {
        World world = CreateWorld();
        world.SentientGroups.Add(new SentientPopulationGroup(1) { SourceLineageId = 10, CurrentRegionId = 0, PopulationCount = 40 });
        EmergingSociety society = SeedSociety(world);
        world.SocialSettlements.Add(new SocialSettlement(1)
        {
            FounderSocietyId = society.Id,
            FounderLineageId = society.LineageId,
            RegionId = 0,
            FoundingYear = 120,
            Population = 50,
            SettlementViability = 0.80
        });
        society.SettlementIds.Add(1);
        Polity polity = new(1, "Stone Basin People", 1, 0, 190, lineageId: 10)
        {
            FounderSocietyId = 1,
            YearsSinceFounded = 8,
            CurrentPressureSummary = "stable growth"
        };
        polity.EstablishFirstSettlement(0, "Stone Basin Hearth");
        world.Polities.Add(polity);
        world.FocalCandidateProfiles.Add(new FocalCandidateProfile(1, 10, 1, 8, 1, "solid", 0, "stable growth", "water route", "formed", StabilityBand.Stable, true));
        world.AddCivilizationalHistoryEvent(CivilizationalHistoryEventType.PolityFormation, 10, 0, "Polity formed", polityId: 1);
        world.AddCivilizationalHistoryEvent(CivilizationalHistoryEventType.SocietyFormation, 10, 0, "Society formed", societyId: 1);

        PhaseCReadinessReport report = PhaseCReadinessEvaluator.Evaluate(world, new WorldGenerationSettings
        {
            MinimumPhaseCSentientGroupCount = 1,
            MinimumPhaseCPersistentSocietyCount = 1,
            MinimumPhaseCSettlementCount = 1,
            MinimumPhaseCViableSettlementCount = 1,
            MinimumPhaseCPolityCount = 1,
            MinimumPhaseCViableFocalCandidateCount = 1,
            MinimumPhaseCAveragePolityAge = 4,
            MinimumPhaseCHistoricalEventDensity = 0.1
        });

        Assert.True(report.IsReady);
    }

    private static SocialEmergenceSystem CreateSystem(WorldGenerationSettings? settings = null)
        => new(77, settings ?? new WorldGenerationSettings());

    private static World CreateWorld()
    {
        World world = new(new WorldTime(120, 1), WorldSimulationPhase.Bootstrap);
        Region basin = new(0, "Stone Basin")
        {
            Biome = RegionBiome.RiverValley,
            Fertility = 0.82,
            WaterAvailability = 0.78,
            PlantBiomass = 900,
            AnimalBiomass = 280,
            MaxPlantBiomass = 1200,
            MaxAnimalBiomass = 360
        };
        Region frontier = new(1, "North Shelf")
        {
            Biome = RegionBiome.Plains,
            Fertility = 0.60,
            WaterAvailability = 0.50,
            PlantBiomass = 680,
            AnimalBiomass = 220,
            MaxPlantBiomass = 1000,
            MaxAnimalBiomass = 320
        };
        basin.EcologyProfile = RegionEcologyProfileBuilder.Build(basin);
        frontier.EcologyProfile = RegionEcologyProfileBuilder.Build(frontier);
        basin.AddConnection(frontier.Id);
        frontier.AddConnection(basin.Id);
        world.Regions.Add(basin);
        world.Regions.Add(frontier);

        Species sentient = new(1, "Stonefolk", 0.48, 0.42)
        {
            TrophicRole = TrophicRole.Omnivore,
            SentienceCapability = SentienceCapabilityState.Capable,
            SentiencePotential = 0.85,
            EcologyNiche = "adaptive omnivore",
            TemperaturePreference = 0.55,
            TemperatureTolerance = 0.36,
            MoisturePreference = 0.58,
            MoistureTolerance = 0.34,
            FertilityPreference = 0.72,
            WaterPreference = 0.74,
            PlantBiomassAffinity = 0.48,
            AnimalBiomassAffinity = 0.42,
            LineageId = 10
        };
        Species grain = new(2, "River Grain", 0.05, 0.0)
        {
            TrophicRole = TrophicRole.Producer,
            EcologyNiche = "seed grass"
        };
        Species roots = new(3, "Marsh Root", 0.05, 0.0)
        {
            TrophicRole = TrophicRole.Producer,
            EcologyNiche = "edible root"
        };
        Species herd = new(4, "Plain Runner", 0.10, 0.18)
        {
            TrophicRole = TrophicRole.Herbivore,
            MeatYield = 18
        };
        Species predator = new(5, "Ash Claw", 0.10, 0.16)
        {
            TrophicRole = TrophicRole.Predator,
            MeatYield = 10
        };
        world.Species.AddRange([sentient, grain, roots, herd, predator]);
        world.EvolutionaryLineages.Add(new EvolutionaryLineage(10, 1, sentient.EcologyNiche, sentient.TrophicRole)
        {
            OriginRegionId = 0,
            OriginYear = 50,
            Stage = LineageStage.SentienceCapable,
            SentienceCapability = SentienceCapabilityState.Capable
        });

        RegionSpeciesPopulation sentientPopulation = basin.GetOrCreateSpeciesPopulation(1);
        sentientPopulation.PopulationCount = 150;
        sentientPopulation.HabitatSuitability = 0.86;
        sentientPopulation.StressScore = 0.10;
        RegionSpeciesPopulation grainPopulation = basin.GetOrCreateSpeciesPopulation(2);
        grainPopulation.PopulationCount = 200;
        grainPopulation.HabitatSuitability = 0.90;
        RegionSpeciesPopulation rootsPopulation = basin.GetOrCreateSpeciesPopulation(3);
        rootsPopulation.PopulationCount = 160;
        rootsPopulation.HabitatSuitability = 0.88;
        RegionSpeciesPopulation herdPopulation = basin.GetOrCreateSpeciesPopulation(4);
        herdPopulation.PopulationCount = 80;
        herdPopulation.HabitatSuitability = 0.70;
        RegionSpeciesPopulation predatorPopulation = basin.GetOrCreateSpeciesPopulation(5);
        predatorPopulation.PopulationCount = 20;
        predatorPopulation.HabitatSuitability = 0.60;

        return world;
    }

    private static EmergingSociety SeedSociety(World world)
    {
        EmergingSociety society = new(1)
        {
            LineageId = 10,
            SpeciesId = 1,
            OriginRegionId = 0,
            FoundingYear = 110,
            Population = 120,
            MobilityMode = MobilityMode.SemiSedentary,
            SubsistenceMode = SubsistenceMode.MixedHunterForager,
            Cohesion = 0.68,
            IdentityStrength = 0.60,
            SocialComplexity = 0.62,
            SurvivalKnowledge = 0.60,
            SedentismPressure = 0.72,
            PressureSummary = "anchoring on rich ground",
            ContinuityYears = 10
        };
        society.RegionIds.Add(0);
        world.Societies.Add(society);
        return society;
    }
}
