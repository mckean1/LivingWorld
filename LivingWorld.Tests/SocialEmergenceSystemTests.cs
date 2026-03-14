using LivingWorld.Core;
using LivingWorld.Generation;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;
using LivingWorld.Systems;
using System.Reflection;
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
    public void SentientGroups_GrowUnderGoodConditions()
    {
        World world = CreateWorld();
        SentientPopulationGroup group = SeedGroup(world, regionId: 0, population: 72);
        SocialEmergenceSystem system = CreateSystem();

        int initialPopulation = group.PopulationCount;
        system.UpdateYear(world);

        Assert.True(group.PopulationCount > initialPopulation);
        Assert.True(group.FoodSecurity > 0.50);
    }

    [Fact]
    public void SentientGroups_DeclineUnderBadConditions()
    {
        World world = CreateWorld();
        MakeRegionHarsh(world.Regions[1]);
        SentientPopulationGroup group = SeedGroup(world, regionId: 1, population: 96);
        group.Cohesion = 0.22;
        group.SurvivalKnowledge = 0.18;
        group.Stress = 0.72;
        group.FoodSecurity = 0.18;
        group.StorageSupport = 0.04;
        group.MigrationPressure = 0.18;
        world.Regions[1].ConnectedRegionIds.Clear();
        world.Regions[0].ConnectedRegionIds.Clear();

        Species species = world.Species.First(candidate => candidate.Id == 1);
        RegionSpeciesPopulation harshPopulation = world.Regions[1].GetOrCreateSpeciesPopulation(species.Id);
        harshPopulation.PopulationCount = 40;
        harshPopulation.HabitatSuitability = 0.28;
        harshPopulation.StressScore = 0.88;
        harshPopulation.RecentFoodStress = 0.78;
        harshPopulation.RecentPredationPressure = 0.44;

        SocialEmergenceSystem system = CreateSystem();
        int initialPopulation = group.PopulationCount;

        system.UpdateYear(world);

        Assert.True(group.IsCollapsed || group.PopulationCount < initialPopulation);
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
        EmergingSociety society = SeedSociety(world);
        society.Population = 220;
        society.SedentismPressure = 1.00;
        society.FoodSecurity = 0.86;
        society.StorageSupport = 0.72;
        society.SettlementSupport = 0.84;
        society.LocalCarryingSupport = 0.88;
        society.CulturalKnowledge["basin"] = new CulturalDiscovery("basin", "The basin holds year-round water", CulturalDiscoveryCategory.Geography, RegionId: 0);
        society.CulturalKnowledge["grain"] = new CulturalDiscovery("grain", "Wild grain stores well", CulturalDiscoveryCategory.FoodSafety, SpeciesId: 2, RegionId: 0);
        society.CulturalKnowledge["herd"] = new CulturalDiscovery("herd", "Herd beasts cross the basin", CulturalDiscoveryCategory.SpeciesUse, SpeciesId: 4, RegionId: 0);
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

        typeof(SocialEmergenceSystem)
            .GetMethod("FoundSettlement", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(system, new object[] { world, society, world.Regions[0] });

        SocialSettlement settlement = Assert.Single(world.SocialSettlements);
        Assert.False(settlement.IsAbandoned);

        settlement.SettlementViability = 0.05;
        world.Time.Reset(world.Time.Year + 1, 1);
        system.UpdateYear(world);

        Assert.True(settlement.IsAbandoned);
        Assert.Contains(world.CivilizationalHistory, evt => evt.Type == CivilizationalHistoryEventType.SettlementAbandoned);
    }

    [Fact]
    public void Societies_DeclineUnderBadConditions()
    {
        World world = CreateWorld();
        MakeRegionHarsh(world.Regions[0]);
        EmergingSociety society = SeedSociety(world);
        society.OriginRegionId = 0;
        society.RegionIds.Clear();
        society.RegionIds.Add(0);
        society.Population = 160;
        society.Cohesion = 0.26;
        society.IdentityStrength = 0.24;
        society.SurvivalKnowledge = 0.20;
        society.SedentismPressure = 0.14;
        society.FoodSecurity = 0.18;
        society.StorageSupport = 0.06;
        society.SettlementSupport = 0.10;
        society.LocalCarryingSupport = 0.18;
        society.MigrationPressure = 0.56;
        society.FragmentationPressure = 0.62;
        world.Regions[0].ConnectedRegionIds.Clear();
        RegionSpeciesPopulation harshPopulation = world.Regions[0].GetOrCreateSpeciesPopulation(1);
        harshPopulation.PopulationCount = 34;
        harshPopulation.HabitatSuitability = 0.24;
        harshPopulation.StressScore = 0.90;
        harshPopulation.RecentFoodStress = 0.82;

        SocialEmergenceSystem system = CreateSystem();
        int initialPopulation = society.Population;

        system.UpdateYear(world);

        Assert.True(society.IsCollapsed || society.Population < initialPopulation);
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

        Assert.Equal(SubsistenceMode.ProtoFarming, society.SubsistenceMode);
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
    public void ViableSociety_CanGrowIntoPolityThresholdOrganically()
    {
        World world = CreateWorld();
        EmergingSociety society = SeedSociety(world);
        society.Population = 126;
        society.SocialComplexity = 0.54;
        society.SurvivalKnowledge = 0.72;
        society.FoodSecurity = 0.66;
        society.StorageSupport = 0.48;
        society.SettlementSupport = 0.76;
        society.LocalCarryingSupport = 0.74;
        society.CulturalKnowledge["a"] = new CulturalDiscovery("a", "Water route", CulturalDiscoveryCategory.Geography, RegionId: 0);
        society.CulturalKnowledge["b"] = new CulturalDiscovery("b", "Safe roots", CulturalDiscoveryCategory.FoodSafety, SpeciesId: 2, RegionId: 0);
        society.CulturalKnowledge["c"] = new CulturalDiscovery("c", "Fertile basin", CulturalDiscoveryCategory.Environment, RegionId: 0);
        world.SocialSettlements.Add(new SocialSettlement(1)
        {
            FounderSocietyId = society.Id,
            FounderLineageId = society.LineageId,
            RegionId = 0,
            FoundingYear = 120,
            Population = 52,
            SettlementViability = 0.84,
            StorageLevel = 0.58
        });
        society.SettlementIds.Add(1);

        SocialEmergenceSystem system = CreateSystem(new WorldGenerationSettings
        {
            PolityFormationMinimumPopulation = 135,
            PolityFormationMinimumKnowledgeCount = 3,
            PolityFormationComplexityThreshold = 0.52
        });

        for (int year = 0; year < 3 && world.Polities.Count == 0; year++)
        {
            system.UpdateYear(world);
            world.Time.Reset(world.Time.Year + 1, 1);
        }

        Assert.Contains(world.Polities, polity => polity.FounderSocietyId == society.Id);
    }

    [Fact]
    public void Fragmentation_PreservesSocietyContinuity()
    {
        World world = CreateWorld();
        EmergingSociety society = SeedSociety(world);
        society.Population = 620;
        society.Cohesion = 0.06;
        society.IdentityStrength = 0.10;
        society.SurvivalKnowledge = 0.24;
        society.StorageSupport = 0.04;
        society.SettlementSupport = 0.08;
        society.LocalCarryingSupport = 0.20;
        society.MigrationPressure = 0.76;
        society.FragmentationPressure = 0.94;
        society.PressureSummary = "frontier strain";
        world.SocialSettlements.Clear();

        SocialEmergenceSystem system = CreateSystem();
        typeof(SocialEmergenceSystem)
            .GetMethod("FragmentSociety", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(system, new object[] { world, society, world.Regions[0] });

        Assert.True(world.Societies.Count >= 2);
        Assert.Contains(world.Societies, candidate => candidate.ParentSocietyId == society.Id);
        Assert.Contains(world.CivilizationalHistory, evt => evt.Type == CivilizationalHistoryEventType.Fragmentation);
    }

    [Fact]
    public void SameLineageCanActivateMultipleRegionalTrajectories()
    {
        World world = CreateWorld();
        RegionSpeciesPopulation frontierPopulation = world.Regions[1].GetOrCreateSpeciesPopulation(1);
        frontierPopulation.PopulationCount = 140;
        frontierPopulation.HabitatSuitability = 0.82;
        frontierPopulation.StressScore = 0.08;

        SocialEmergenceSystem system = CreateSystem(new WorldGenerationSettings
        {
            SentientActivationMinimumPopulation = 40,
            SentientActivationMinimumSupport = 0.35,
            SentientActivationMaximumIndependentGroupsPerLineage = 2,
            SentientTrajectoryMinimumRegionalSeparation = 1
        });

        system.UpdateYear(world);

        Assert.Equal(2, world.SentientGroups.Count(group => !group.IsCollapsed && group.SourceLineageId == 10));
        Assert.Equal(2, world.SentientGroups.Where(group => !group.IsCollapsed && group.SourceLineageId == 10).Select(group => group.CurrentRegionId).Distinct().Count());
    }

    [Fact]
    public void HealthyWorldSupportsMultipleTrajectoriesFromOneLineage()
    {
        World world = CreateWorld();
        RegionSpeciesPopulation frontierPopulation = world.Regions[1].GetOrCreateSpeciesPopulation(1);
        frontierPopulation.PopulationCount = 150;
        frontierPopulation.HabitatSuitability = 0.84;
        frontierPopulation.StressScore = 0.06;

        SocialEmergenceSystem system = CreateSystem(new WorldGenerationSettings
        {
            SentientActivationMinimumPopulation = 40,
            SentientActivationMinimumSupport = 0.35,
            PersistentGroupCohesionThreshold = 0.30,
            SocietyFormationContinuityYears = 2,
            SocietyFormationIdentityThreshold = 0.18,
            SentientActivationMaximumIndependentGroupsPerLineage = 2,
            SentientTrajectoryMinimumRegionalSeparation = 1
        });

        for (int year = 0; year < 4; year++)
        {
            system.UpdateYear(world);
            world.Time.Reset(world.Time.Year + 1, 1);
        }

        int lineageArcCount = world.Societies.Count(society => !society.IsCollapsed && society.LineageId == 10)
            + world.SentientGroups.Count(group => !group.IsCollapsed && group.SourceLineageId == 10);
        Assert.True(lineageArcCount >= 2);
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

    private static SentientPopulationGroup SeedGroup(World world, int regionId, int population)
    {
        SentientPopulationGroup group = new(1)
        {
            SourceLineageId = 10,
            CurrentRegionId = regionId,
            FounderRegionId = regionId,
            ActivationYear = 120,
            PopulationCount = population,
            MobilityMode = MobilityMode.SemiSedentary,
            Cohesion = 0.56,
            SocialComplexity = 0.32,
            SurvivalKnowledge = 0.42,
            SettlementIntent = 0.34,
            Stress = 0.10,
            SedentismPressure = 0.42,
            ContinuityYears = 2,
            IdentityStrength = 0.30,
            MigrationPattern = "anchored circuit",
            FoundingMemorySeed = "basin awakening",
            ThreatMemorySeed = "seasonal predators",
            PressureSummary = "shared survival",
            FoodSecurity = 0.54,
            StorageSupport = 0.26,
            LocalCarryingSupport = 0.70,
            MigrationPressure = 0.10,
            FragmentationPressure = 0.08
        };
        group.SharedKnowledge["water"] = new CulturalDiscovery("water", "Reliable water", CulturalDiscoveryCategory.Geography, RegionId: regionId);
        group.SharedKnowledge["grain"] = new CulturalDiscovery("grain", "Wild grain is edible", CulturalDiscoveryCategory.FoodSafety, SpeciesId: 2, RegionId: regionId);
        group.SharedKnowledge["prey"] = new CulturalDiscovery("prey", "Herd beasts are useful prey", CulturalDiscoveryCategory.SpeciesUse, SpeciesId: 4, RegionId: regionId);
        world.SentientGroups.Add(group);
        return group;
    }

    private static void MakeRegionHarsh(Region region)
    {
        region.Biome = RegionBiome.Drylands;
        region.Fertility = 0.18;
        region.WaterAvailability = 0.14;
        region.PlantBiomass = 120;
        region.AnimalBiomass = 30;
        region.MaxPlantBiomass = 240;
        region.MaxAnimalBiomass = 90;
        region.EcologyProfile = RegionEcologyProfileBuilder.Build(region);
    }
}
