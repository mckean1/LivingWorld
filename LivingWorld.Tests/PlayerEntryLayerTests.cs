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
        WorldGenerationSettings settings = new();
        World world = CreateWeakFallbackWorld();

        bool accepted = PlayerEntryOutcomeEvaluator.ShouldSurfaceFocalSelection(world, settings, out List<string> rejectionReasons);

        Assert.False(accepted);
        Assert.Contains("max_age_stop_only_produced_fallback_pool", rejectionReasons);
        Assert.Contains("single_fallback_candidate_rejected", rejectionReasons);
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
    public void StrictWeakWorldHandling_RejectsFallbackOnlyWorlds()
    {
        WorldGenerationSettings settings = new()
        {
            MinimumBiologicalReadinessFloor = 0.99,
            MinimumHealthyCandidateCount = 3,
            MinimumViablePlayerEntryCandidates = 3,
            MaximumEmergencyFallbackCandidatesToSurface = 0,
            MaxStartupRegenerationAttempts = 1
        };
        World world = CreateWeakFallbackWorld();

        bool accepted = PlayerEntryOutcomeEvaluator.ShouldSurfaceFocalSelection(world, settings, out List<string> rejectionReasons);

        Assert.False(accepted);
        Assert.NotEmpty(rejectionReasons);
    }

    [Fact]
    public void FocalSelection_DefaultPresentation_HidesInternalFallbackWording()
    {
        World world = CreateSelectionWorld();
        WatchUiState uiState = new();
        uiState.SetActiveMainView(WatchViewType.FocalSelection);

        IReadOnlyList<string> lines = WatchScreenBuilder.BuildBodyLines(world, new ChronicleFocus(), uiState);
        string joined = string.Join('\n', lines);

        Assert.DoesNotContain("fallback", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bootstrap", joined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Start Summary:", joined);
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

    [Fact]
    public void ChronicleRenderer_SanitizesDetachedSummaryFragments()
    {
        World world = CreateSelectionWorld();
        ChronicleWatchRenderer renderer = new(
            new SimulationOptions { OutputMode = OutputMode.Watch, WriteStructuredHistory = false },
            new ChronicleColorWriter(),
            new ChronicleEventFormatter());
        ChronicleFocus focus = new();
        focus.SetFocus(1, 10);
        WatchUiState uiState = new();
        uiState.SetActiveMainView(WatchViewType.Chronicle);

        renderer.Record(world, focus, uiState, new WorldEvent
        {
            Year = 812,
            Month = 1,
            SimulationPhase = WorldSimulationPhase.Active,
            Narrative = "A settlement was founded",
            Type = WorldEventType.SettlementFounded,
            Severity = WorldEventSeverity.Major,
            PolityId = 1
        });

        List<string> entries = (List<string>)typeof(ChronicleWatchRenderer)
            .GetField("_chronicleEntries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(renderer)!;
        entries.Add("learned Seasonal Planning, Storage");

        renderer.Render(world, focus, uiState);

        Assert.All(renderer.SnapshotChronicleEntries(), entry => Assert.StartsWith("Year ", entry, StringComparison.Ordinal));
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

    private static World CreateSelectionWorld()
    {
        World world = CreateCandidateWorld();
        world.StartupStage = WorldStartupStage.FocalSelection;
        world.PrehistoryStopReason = PrehistoryStopReason.ReadinessSatisfied;
        world.PlayerEntryCandidates.Clear();
        world.PlayerEntryCandidates.Add(new PlayerEntryCandidateSummary(
            1,
            "Green Basin Confederacy",
            1,
            "River Folk",
            10,
            0,
            "Green Basin",
            12,
            812,
            2,
            "large",
            "Proto-farming",
            "Stable",
            "Reliable basin water, Edible marsh grain",
            "Fire, Seasonal Planning",
            "recently founded a settlement",
            "holding fertile ground",
            0.92,
            StabilityBand.Stable,
            false));
        world.WorldReadinessReport = new WorldReadinessReport(
            true,
            812,
            0.86,
            0.74,
            0.72,
            0.84,
            0.78,
            1,
            Array.Empty<string>(),
            new Dictionary<string, bool>
            {
                ["biology"] = true,
                ["social"] = true,
                ["civilization"] = true,
                ["candidates"] = true,
                ["stability"] = true
            });
        world.Time.Reset(812, 1);
        return world;
    }

    private static World CreateWeakFallbackWorld()
    {
        World world = CreateSelectionWorld();
        world.PrehistoryStopReason = PrehistoryStopReason.MaxAgeReached;
        world.PlayerEntryCandidates.Clear();
        world.PlayerEntryCandidates.Add(new PlayerEntryCandidateSummary(
            1,
            "Green Basin Confederacy",
            1,
            "River Folk",
            10,
            0,
            "Green Basin",
            4,
            950,
            1,
            "small",
            "Mixed subsistence",
            "Vulnerable",
            "Shared survival lore",
            "None",
            "holds an older local history",
            "holding together through recent strain",
            0.46,
            StabilityBand.Fragile,
            true));
        world.WorldReadinessReport = world.WorldReadinessReport with
        {
            IsReady = false,
            WorldAgeYears = 950,
            BiologicalScore = 0.34,
            CandidateScore = 0.28,
            ViableCandidateCount = 1,
            FailureReasons = new[] { "biology_floor_below_minimum", "candidates_not_ready" }
        };
        return world;
    }
}
