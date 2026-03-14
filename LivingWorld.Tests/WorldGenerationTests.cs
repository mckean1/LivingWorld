using LivingWorld.Core;
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
    public void RegionEcologyProfileBuilder_DerivesInspectableValues()
    {
        Region region = new(0, "Green Reach")
        {
            Biome = RegionBiome.RiverValley,
            Fertility = 0.84,
            WaterAvailability = 0.78,
            MaxPlantBiomass = 1180,
            MaxAnimalBiomass = 320
        };

        RegionEcologyProfile profile = RegionEcologyProfileBuilder.Build(region);

        Assert.InRange(profile.BasePrimaryProductivity, 0.65, 1.0);
        Assert.InRange(profile.HabitabilityScore, 0.60, 1.0);
        Assert.InRange(profile.MigrationEase, 0.20, 1.0);
        Assert.InRange(profile.EnvironmentalVolatility, 0.0, 1.0);
    }

    [Fact]
    public void WorldGenerator_BuildsPrimitiveLifeFirstStartupWorld()
    {
        World world = new WorldGenerator(seed: 7).Generate();

        Assert.Equal(WorldStartupStage.FocalSelection, world.StartupStage);
        Assert.Equal(36, world.Regions.Count);
        Assert.True(world.Species.Count >= 7);
        Assert.Equal(world.Species.Count, world.EvolutionaryLineages.Count);
        Assert.True(world.Polities.Count >= 1);
        Assert.True(world.Societies.Count >= 1);
        Assert.True(world.Species.Count(species => species.IsPrimitiveLineage) >= 7);
        Assert.Contains(world.Species, species => species.ParentSpeciesId is not null);
        Assert.True(world.PhaseAReadinessReport.OccupiedRegions > 0);
        Assert.True(world.PhaseBReadinessReport.MatureLineageCount >= 0);
        Assert.True(world.PhaseCReadinessReport.PolityCount >= 1);
        Assert.NotEmpty(world.EvolutionaryHistory);
        Assert.NotEmpty(world.CivilizationalHistory);
        Assert.True(world.Time.Year >= world.StartupAgeConfiguration.MinPrehistoryYears);
        Assert.Equal(1, world.Time.Month);
        Assert.NotEmpty(world.PlayerEntryCandidates);
        Assert.NotNull(world.PrehistoryRuntime.LastCheckpointOutcome);
    }

    [Fact]
    public void SuitabilityBasedPrimitiveSeeding_FavorsPreferredHabitats()
    {
        World world = new WorldGenerator(seed: 11).Generate();
        Species wetProducer = Assert.Single(world.Species, species => species.PrimitiveTemplateId == "mat_reeds");

        Region bestRegion = world.Regions
            .OrderByDescending(region => SpeciesEcology.CalculateBaseHabitatSuitability(wetProducer, region))
            .First();
        int occupiedWetProducerRegions = world.Regions.Count(region => region.GetSpeciesPopulation(wetProducer.Id)?.PopulationCount > 0);

        Assert.Contains(bestRegion.Biome, new[] { RegionBiome.RiverValley, RegionBiome.Wetlands, RegionBiome.Coast });
        Assert.True(bestRegion.GetSpeciesPopulation(wetProducer.Id)?.PopulationCount > 0);
        Assert.InRange(occupiedWetProducerRegions, 8, 30);
    }

    [Fact]
    public void PrimitiveSeeding_ProvidesBroadProducerCoverage_WithoutMakingEveryLineageGlobal()
    {
        World world = new WorldGenerator(seed: 13).Generate();

        int producerCoveredRegions = world.Regions.Count(region => region.SpeciesPopulations.Any(population =>
            population.PopulationCount > 0
            && world.Species.First(species => species.Id == population.SpeciesId).TrophicRole == TrophicRole.Producer));

        Assert.True(producerCoveredRegions >= 30);
        Assert.All(world.Species, species =>
        {
            int occupiedRegions = world.Regions.Count(region => region.GetSpeciesPopulation(species.Id)?.PopulationCount > 0);
            Assert.True(occupiedRegions > 0);
            Assert.True(occupiedRegions < world.Regions.Count);
        });
    }

    [Fact]
    public void PrimitiveSeeding_IsNonUniformAcrossWorld()
    {
        World world = new WorldGenerator(seed: 17).Generate();

        IReadOnlyList<int> occupiedCounts = world.Species
            .Select(species => world.Regions.Count(region => region.GetSpeciesPopulation(species.Id)?.PopulationCount > 0))
            .OrderBy(count => count)
            .ToList();
        IReadOnlyList<int> regionalRichness = world.Regions
            .Select(region => region.SpeciesPopulations.Count(population => population.PopulationCount > 0))
            .OrderBy(count => count)
            .ToList();

        Assert.True(occupiedCounts.Distinct().Count() >= 4);
        Assert.True(regionalRichness.First() < regionalRichness.Last());
        Assert.Contains(world.Regions, region => region.SpeciesPopulations.Count(population => population.PopulationCount > 0) >= 4);
        Assert.Contains(world.Regions, region => region.SpeciesPopulations.Count(population => population.PopulationCount > 0) <= 2);
    }

    [Fact]
    public void EarlyEcologicalLoop_UpdatesPopulationSignals()
    {
        World world = new WorldGenerator(seed: 19).Generate();
        EcosystemSystem ecosystemSystem = new(new EcosystemSettings
        {
            MaxMigrationTargetsPerPopulation = 0
        });

        ecosystemSystem.UpdateSeason(world);
        ecosystemSystem.ResolveSeasonalCleanup(world);

        Assert.Contains(world.Regions.SelectMany(region => region.SpeciesPopulations), population => population.FoodSupport > 0);
        Assert.Contains(world.Regions.SelectMany(region => region.SpeciesPopulations), population => population.ReproductionPressure > 0);
        Assert.Contains(world.Regions.SelectMany(region => region.SpeciesPopulations), population => population.Trend is PopulationTrend.Growing or PopulationTrend.Declining or PopulationTrend.Stable or PopulationTrend.Founder);
    }

    [Fact]
    public void FounderPopulationSpread_CreatesSmallViableRegionalColonies()
    {
        World world = CreateFounderSpreadWorld();
        EcosystemSystem ecosystemSystem = new(new EcosystemSettings
        {
            HerbivoreExpansionCapacityRatioThreshold = 0.32,
            FrontierTargetSuitability = 0.45,
            MinimumSourcePopulationForMigration = 6,
            FounderPopulationMinimum = 2
        });

        ecosystemSystem.InitializeRegionalPopulations(world);

        RegionSpeciesPopulation sourcePopulation = world.Regions[0].GetOrCreateSpeciesPopulation(1);
        RegionSpeciesPopulation targetPopulation = world.Regions[1].GetOrCreateSpeciesPopulation(1);
        sourcePopulation.PopulationCount = 72;
        sourcePopulation.CarryingCapacity = 90;
        sourcePopulation.MigrationPressure = 0.88;
        targetPopulation.PopulationCount = 0;

        ecosystemSystem.UpdateSeason(world);

        Assert.True(targetPopulation.PopulationCount > 0);
        Assert.InRange(targetPopulation.PopulationCount, 2, 12);
        Assert.Equal(PopulationTrend.Founder, targetPopulation.Trend);
    }

    [Fact]
    public void LocalExtinctionBehavior_RemovesFailedPopulation_AndKeepsDebugHistory()
    {
        World world = CreateFounderSpreadWorld();
        EcosystemSystem ecosystemSystem = new();

        ecosystemSystem.InitializeRegionalPopulations(world);

        Region region = world.Regions[0];
        RegionSpeciesPopulation population = region.GetOrCreateSpeciesPopulation(1);
        population.PopulationCount = 0;
        population.HasEverExisted = true;
        population.HabitatSuitability = 0.28;
        population.StressScore = 0.92;
        population.LastPopulationBeforeExtinction = 9;
        population.LastPopulationExitReason = "collapse";

        ecosystemSystem.ResolveSeasonalCleanup(world);

        Assert.Contains(world.LocalPopulationExtinctions, record =>
            record.RegionId == region.Id
            && record.SpeciesId == 1
            && record.Reason == "collapse");
    }

    [Fact]
    public void PhaseAReadinessReport_ExplainsBrokenWorlds()
    {
        World world = new WorldGenerator(seed: 23).Generate();
        foreach (Region region in world.Regions)
        {
            foreach (RegionSpeciesPopulation population in region.SpeciesPopulations)
            {
                if (world.Species.First(species => species.Id == population.SpeciesId).TrophicRole == TrophicRole.Producer)
                {
                    population.PopulationCount = 0;
                }
            }
        }

        PhaseAReadinessReport report = PhaseAReadinessEvaluator.Evaluate(world, new WorldGenerationSettings());

        Assert.False(report.IsReady);
        Assert.Contains(report.FailureReasons, reason => reason.StartsWith("producer_coverage_below_target:", StringComparison.Ordinal));
    }

    [Fact]
    public void GeneratedWorld_CapturesEvolutionaryHistoryAndPhaseBReadiness()
    {
        World world = new WorldGenerator(seed: 29).Generate();

        Assert.NotEmpty(world.EvolutionaryLineages);
        Assert.Contains(world.EvolutionaryHistory, evt => evt.Type is EvolutionaryHistoryEventType.MinorMutation or EvolutionaryHistoryEventType.MajorMutation or EvolutionaryHistoryEventType.Speciation);
        Assert.True(world.PhaseBReadinessReport.MaxAncestryDepth >= 0);
        Assert.True(world.PhaseBReadinessReport.MatureRegionalDivergenceCount >= 0);
    }

    [Fact]
    public void CompactYoungWorldValidation_SurfacesOrganicCandidatesAcrossMultipleRoots()
    {
        World world = new WorldGenerator(seed: 43, CreateCompactValidationSettings()).Generate();

        IReadOnlyList<PlayerEntryCandidateSummary> organicCandidates = world.PlayerEntryCandidates
            .Where(candidate => !candidate.IsFallbackCandidate)
            .ToList();

        Assert.True(world.PhaseBDiagnostics.SentienceCapableRootBranchCount >= 2);
        Assert.True(organicCandidates.Count >= 2);
        Assert.True(organicCandidates.Select(candidate => candidate.HomeRegionId).Distinct().Count() >= 2);
        Assert.True(organicCandidates.Select(candidate => candidate.LineageId).Distinct().Count() >= 2);
    }

    private static World CreateFounderSpreadWorld()
    {
        World world = new(new WorldTime(), WorldSimulationPhase.Bootstrap)
        {
            StartupStage = WorldStartupStage.PrimitiveEcologyFoundation
        };

        Region source = new(0, "Source Basin")
        {
            Biome = RegionBiome.Plains,
            Fertility = 0.72,
            WaterAvailability = 0.62,
            PlantBiomass = 720,
            AnimalBiomass = 180,
            MaxPlantBiomass = 1080,
            MaxAnimalBiomass = 260
        };
        Region target = new(1, "Target Basin")
        {
            Biome = RegionBiome.Plains,
            Fertility = 0.68,
            WaterAvailability = 0.58,
            PlantBiomass = 680,
            AnimalBiomass = 120,
            MaxPlantBiomass = 1020,
            MaxAnimalBiomass = 240
        };

        source.EcologyProfile = RegionEcologyProfileBuilder.Build(source);
        target.EcologyProfile = RegionEcologyProfileBuilder.Build(target);
        source.AddConnection(1);
        target.AddConnection(0);
        world.Regions.Add(source);
        world.Regions.Add(target);

        Species producer = new(0, "Test Mats", 0.02, 0.02)
        {
            IsPrimitiveLineage = true,
            PrimitiveTemplateId = "test_mats",
            EcologyNiche = "producer",
            TrophicRole = TrophicRole.Producer,
            TemperaturePreference = 0.58,
            TemperatureTolerance = 0.24,
            MoisturePreference = 0.56,
            MoistureTolerance = 0.22,
            FertilityPreference = 0.70,
            WaterPreference = 0.62,
            PlantBiomassAffinity = 0.90,
            BaseCarryingCapacityFactor = 1.16,
            BaseReproductionRate = 0.18,
            BaseDeclineRate = 0.02,
            Resilience = 0.52,
            MigrationCapability = 0.12,
            ExpansionPressure = 0.20,
            StartingSpreadWeight = 0.88
        };
        producer.PreferredBiomes.Add(RegionBiome.Plains);
        producer.InitialRangeRegionIds.Add(0);
        producer.InitialRangeRegionIds.Add(1);

        Species grazer = new(1, "Test Grazers", 0.03, 0.04)
        {
            IsPrimitiveLineage = true,
            PrimitiveTemplateId = "test_grazers",
            EcologyNiche = "grazer",
            TrophicRole = TrophicRole.Herbivore,
            TemperaturePreference = 0.58,
            TemperatureTolerance = 0.22,
            MoisturePreference = 0.56,
            MoistureTolerance = 0.20,
            FertilityPreference = 0.66,
            WaterPreference = 0.58,
            PlantBiomassAffinity = 0.72,
            AnimalBiomassAffinity = 0.06,
            BaseCarryingCapacityFactor = 1.00,
            BaseReproductionRate = 0.10,
            BaseDeclineRate = 0.03,
            Resilience = 0.44,
            MigrationCapability = 0.28,
            ExpansionPressure = 0.24,
            StartingSpreadWeight = 0.54
        };
        grazer.PreferredBiomes.Add(RegionBiome.Plains);
        grazer.DietSpeciesIds.Add(producer.Id);
        grazer.InitialRangeRegionIds.Add(0);

        world.Species.Add(producer);
        world.Species.Add(grazer);

        return world;
    }

    private static WorldGenerationSettings CreateCompactValidationSettings()
        => new()
        {
            StartupWorldAgePreset = StartupWorldAgePreset.YoungWorld,
            RegionCount = 16,
            ContinentWidth = 4,
            ContinentHeight = 4,
            PhaseAMaximumBootstrapMonths = 36,
            PhaseBMinimumBootstrapYears = 120,
            PhaseBMaximumBootstrapYears = 320,
            PhaseCMinimumBootstrapYears = 80,
            PhaseCMaximumBootstrapYears = 220,
            ReadinessEvaluationIntervalYears = 10,
            MaxStartupRegenerationAttempts = 2
        };
}
