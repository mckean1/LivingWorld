using LivingWorld.Core;
using LivingWorld.Generation;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Presentation;
using LivingWorld.Societies;
using Xunit;

namespace LivingWorld.Tests;

public sealed class PlayerEntryLayerTests
{
    [Fact]
    public void StartupWorldAgeConfiguration_UsesPresetSpecificBounds()
    {
        StartupWorldAgeConfiguration young = StartupWorldAgeConfiguration.ForPreset(StartupWorldAgePreset.YoungWorld);
        StartupWorldAgeConfiguration standard = StartupWorldAgeConfiguration.ForPreset(StartupWorldAgePreset.StandardWorld);
        StartupWorldAgeConfiguration ancient = StartupWorldAgeConfiguration.ForPreset(StartupWorldAgePreset.AncientWorld);

        Assert.True(young.MinPrehistoryYears < standard.MinPrehistoryYears);
        Assert.True(standard.TargetPrehistoryYears < ancient.TargetPrehistoryYears);
        Assert.True(young.MaxPrehistoryYears < ancient.MaxPrehistoryYears);
    }

    [Fact]
    public void PrehistoryRuntimeController_DoesNotEnableReadinessBeforeMinimumAge()
    {
        World world = new(new WorldTime(200, 1), WorldSimulationPhase.Bootstrap);
        StartupWorldAgeConfiguration config = StartupWorldAgeConfiguration.ForPreset(StartupWorldAgePreset.StandardWorld);
        PrehistoryRuntimeController controller = new();

        controller.Initialize(world, config);
        controller.RefreshAge(world);

        Assert.False(world.PrehistoryRuntime.AreReadinessChecksActive);

        world.Time.Reset(config.MinPrehistoryYears, 1);
        controller.RefreshAge(world);

        Assert.True(world.PrehistoryRuntime.AreReadinessChecksActive);
    }

    [Fact]
    public void WorldGenerator_BuildsPlayerEntryCandidates_AndStopsPrehistory()
    {
        World world = new WorldGenerator(seed: 31).Generate();

        Assert.Equal(WorldStartupStage.FocalSelection, world.StartupStage);
        Assert.Equal(PrehistoryRuntimeState.FocalSelection, world.PrehistoryRuntime.CurrentState);
        Assert.False(world.PrehistoryRuntime.IsPrehistoryAdvancing);
        Assert.NotEmpty(world.PlayerEntryCandidates);
        Assert.InRange(world.PlayerEntryCandidates.Count, 1, 8);
        Assert.NotNull(world.PrehistoryStopReason);
    }

    [Fact]
    public void WorldGenerator_ForceStopsAtMaxAgeWhenReadinessNeverPasses()
    {
        WorldGenerationSettings settings = new()
        {
            StartupWorldAgePreset = StartupWorldAgePreset.YoungWorld,
            MinimumViablePlayerEntryCandidates = 99
        };

        World world = new WorldGenerator(seed: 41, settings).Generate();

        Assert.True(world.Time.Year >= world.StartupAgeConfiguration.MaxPrehistoryYears);
        Assert.True(
            world.PrehistoryStopReason is PrehistoryStopReason.MaxAgeReached or PrehistoryStopReason.ForcedFallback,
            $"Unexpected stop reason: {world.PrehistoryStopReason}");
    }

    [Fact]
    public void CandidateGeneration_ExcludesInvalidPolities()
    {
        World world = CreateCandidateWorld();
        PlayerEntryCandidateGenerator generator = new(new WorldGenerationSettings());

        IReadOnlyList<PlayerEntryCandidateSummary> candidates = generator.Generate(world, allowEmergencyFallback: false, out Dictionary<int, string> rejectionReasons);

        Assert.Single(candidates);
        Assert.Equal(1, candidates[0].PolityId);
        Assert.Contains(2, rejectionReasons.Keys);
        Assert.Contains(3, rejectionReasons.Keys);
    }

    [Fact]
    public void Simulation_BindsChosenPolity_AndSetsChronicleBoundary()
    {
        World world = new WorldGenerator(seed: 43).Generate();
        using Simulation simulation = new(world, new SimulationOptions { OutputMode = OutputMode.Debug, WriteStructuredHistory = false });

        simulation.RunMonths(1);

        Assert.Equal(WorldStartupStage.ActivePlay, world.StartupStage);
        Assert.NotNull(world.SelectedFocalPolityId);
        Assert.NotNull(world.PlayerEntryWorldYear);
        Assert.NotNull(world.LiveChronicleStartYear);
        Assert.True(world.IsEventVisibleInLiveChronicle(new WorldEvent
        {
            Year = world.LiveChronicleStartYear!.Value,
            Month = world.LiveChronicleStartMonth!.Value,
            SimulationPhase = WorldSimulationPhase.Active,
            Narrative = "Post-selection event"
        }));
        Assert.False(world.IsEventVisibleInLiveChronicle(new WorldEvent
        {
            Year = world.LiveChronicleStartYear.Value - 1,
            Month = 12,
            SimulationPhase = WorldSimulationPhase.Bootstrap,
            Narrative = "Prehistory event"
        }));
    }

    private static World CreateCandidateWorld()
    {
        World world = new(new WorldTime(820, 1), WorldSimulationPhase.Bootstrap)
        {
            StartupStage = WorldStartupStage.PlayerEntryEvaluation,
            StartupAgeConfiguration = StartupWorldAgeConfiguration.ForPreset(StartupWorldAgePreset.StandardWorld)
        };

        world.Species.Add(new Species(1, "River Folk", 0.8, 0.7) { SentienceCapability = SentienceCapabilityState.Capable });
        world.Species.Add(new Species(2, "Stone Folk", 0.7, 0.6) { SentienceCapability = SentienceCapabilityState.Capable });
        world.Species.Add(new Species(3, "Ash Folk", 0.7, 0.6) { SentienceCapability = SentienceCapabilityState.Capable });
        world.Regions.Add(new Region(0, "Green Basin") { Biome = RegionBiome.RiverValley, Fertility = 0.8, WaterAvailability = 0.7 });
        world.Regions.Add(new Region(1, "Red Steppe") { Biome = RegionBiome.Plains, Fertility = 0.5, WaterAvailability = 0.4 });
        world.Regions.Add(new Region(2, "Ash Hills") { Biome = RegionBiome.Highlands, Fertility = 0.4, WaterAvailability = 0.3 });

        Polity strong = new(1, "Green Basin Confederacy", 1, 0, 260, lineageId: 10)
        {
            YearsSinceFounded = 12,
            CurrentPressureSummary = "anchoring on rich ground"
        };
        strong.EstablishFirstSettlement(0, "Green Basin Hearth");
        strong.AddSettlement(0, "Green Basin Ford");
        strong.AddDiscovery(new CulturalDiscovery("water", "Reliable basin water", CulturalDiscoveryCategory.Geography, RegionId: 0));
        strong.AddDiscovery(new CulturalDiscovery("grain", "Edible marsh grain", CulturalDiscoveryCategory.FoodSafety, RegionId: 0));
        strong.AddDiscovery(new CulturalDiscovery("elk", "Marsh elk routes are reliable", CulturalDiscoveryCategory.SpeciesUse, RegionId: 0));
        world.Polities.Add(strong);

        Polity weakPopulation = new(2, "Red Steppe Band", 2, 1, 40, lineageId: 20)
        {
            YearsSinceFounded = 4,
            CurrentPressureSummary = "migration pressure"
        };
        weakPopulation.EstablishFirstSettlement(1, "Red Steppe Camp");
        world.Polities.Add(weakPopulation);

        Polity noSettlements = new(3, "Ash Hills Wanderers", 3, 2, 140, lineageId: 30)
        {
            YearsSinceFounded = 5,
            CurrentPressureSummary = "frontier strain"
        };
        world.Polities.Add(noSettlements);

        world.FocalCandidateProfiles.Add(new FocalCandidateProfile(1, 10, 1, 12, 2, "large", 0, "anchoring on rich ground", "water, grain", "founded a second hearth", StabilityBand.Strong, true));
        world.FocalCandidateProfiles.Add(new FocalCandidateProfile(2, 20, 2, 4, 1, "small", 1, "migration pressure", "early knowledge", "recovered after migration", StabilityBand.Strained, false));
        world.FocalCandidateProfiles.Add(new FocalCandidateProfile(3, 30, 3, 5, 0, "growing", 2, "frontier strain", "early knowledge", "pressure on the hills", StabilityBand.Stable, false));

        return world;
    }
}
