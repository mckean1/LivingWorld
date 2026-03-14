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
    public void PrehistoryRuntimeOrchestrator_DoesNotEnableReadinessBeforeMinimumAge()
    {
        World world = new(new WorldTime(200, 1), WorldSimulationPhase.Bootstrap);
        StartupWorldAgeConfiguration config = StartupWorldAgeConfiguration.ForPreset(StartupWorldAgePreset.StandardWorld);
        PrehistoryRuntimeOrchestrator orchestrator = new();

        orchestrator.Initialize(world, config);
        orchestrator.RefreshAge(world);

        Assert.False(world.PrehistoryRuntime.AreReadinessChecksActive);

        world.Time.Reset(config.MinPrehistoryYears, 1);
        orchestrator.RefreshAge(world);

        Assert.True(world.PrehistoryRuntime.AreReadinessChecksActive);
    }

    [Fact]
    public void WorldGenerator_BuildsPlayerEntryCandidates_AndStopsPrehistory()
    {
        World world = new WorldGenerator(seed: 31).Generate();

        Assert.Equal(WorldStartupStage.FocalSelection, world.StartupStage);
        Assert.Equal(PrehistoryRuntimePhase.FocalSelection, world.PrehistoryRuntime.CurrentPhase);
        Assert.False(world.PrehistoryRuntime.IsPrehistoryAdvancing);
        Assert.NotEmpty(world.PlayerEntryCandidates);
        Assert.InRange(world.PlayerEntryCandidates.Count, 1, 8);
        Assert.NotNull(world.PrehistoryRuntime.LastCheckpointOutcome);
    }

    [Fact]
    public void WorldGenerator_ForceStopsAtMaxAgeWhenReadinessNeverPasses()
    {
        WorldGenerationSettings settings = new();
        World world = CreateWeakFallbackWorld();

        bool accepted = PlayerEntryOutcomeEvaluator.ShouldSurfaceFocalSelection(world, settings, allowEmergencyFallback: false, out List<string> rejectionReasons);

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
    public void EmergencyAdmittedCandidates_AreAlwaysLabeledFallbackDerived()
    {
        World world = CreateEmergencyAdmissionWorld();
        PlayerEntryCandidateGenerator generator = new(new WorldGenerationSettings());

        IReadOnlyList<PlayerEntryCandidateSummary> candidates = generator.Generate(world, allowEmergencyFallback: true, out _);

        PlayerEntryCandidateSummary candidate = Assert.Single(candidates);
        Assert.True(candidate.IsFallbackCandidate);
        Assert.True(candidate.IsEmergencyAdmitted);
        Assert.StartsWith("emergency_admission:", candidate.CandidateOriginReason, StringComparison.Ordinal);
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

        bool accepted = PlayerEntryOutcomeEvaluator.ShouldSurfaceFocalSelection(world, settings, allowEmergencyFallback: false, out List<string> rejectionReasons);

        Assert.False(accepted);
        Assert.NotEmpty(rejectionReasons);
    }

    [Fact]
    public void WorldsWithZeroOrganicPolities_DoNotPassHealthyStart()
    {
        WorldGenerationSettings settings = new();
        World world = CreateWeakFallbackWorld();
        world.PhaseCReadinessReport = new PhaseCReadinessReport(
            false,
            1,
            0,
            1,
            1,
            0,
            1,
            1,
            0,
            1,
            1,
            1,
            0,
            1,
            1,
            0,
            1,
            4.0,
            0.18,
            new[] { "fallback_only_polities", "fallback_only_focal_candidates" });

        bool accepted = PlayerEntryOutcomeEvaluator.ShouldSurfaceFocalSelection(world, settings, allowEmergencyFallback: false, out List<string> rejectionReasons);

        Assert.False(accepted);
        Assert.Contains("no_organic_polities_available", rejectionReasons);
        Assert.Contains("no_organic_focal_candidates_available", rejectionReasons);
    }

    [Fact]
    public void ReadinessGating_StaysAlignedForFallbackOnlyWorlds()
    {
        WorldGenerationSettings settings = new();
        World world = CreateWeakFallbackWorld();
        world.PhaseAReadinessReport = new PhaseAReadinessReport(
            36,
            36,
            1.0,
            34,
            0.94,
            26,
            0.72,
            8,
            0.22,
            20,
            20,
            0,
            true,
            Array.Empty<string>());
        world.PhaseBReadinessReport = new PhaseBReadinessReport(
            true,
            4,
            3,
            1,
            1,
            6,
            2,
            16,
            Array.Empty<string>());
        world.PhaseCReadinessReport = new PhaseCReadinessReport(
            false,
            1,
            0,
            1,
            1,
            0,
            1,
            1,
            0,
            1,
            1,
            1,
            0,
            1,
            1,
            0,
            1,
            6.0,
            0.20,
            new[] { "fallback_only_polities", "fallback_only_focal_candidates" });

        WorldReadinessReport report = WorldReadinessEvaluator.Evaluate(world, settings);
        world.WorldReadinessReport = report;
        bool accepted = PlayerEntryOutcomeEvaluator.ShouldSurfaceFocalSelection(world, settings, allowEmergencyFallback: false, out List<string> rejectionReasons);

        Assert.False(report.IsReady);
        Assert.Contains(report.FailureReasons, reason => reason.StartsWith("organic_polity_count_below_target:", StringComparison.Ordinal));
        Assert.Contains("no_organic_polities_available", rejectionReasons);
        Assert.Contains("fallback_only_candidate_pool", rejectionReasons);
        Assert.False(accepted);
    }

    [Fact]
    public void CandidateGeneration_DistinguishesOrganicAndFallbackStartsCorrectly()
    {
        World world = CreateCandidateWorld();
        Polity fallbackPolity = new(4, "Ash Hills Remnant", 3, 2, 220, lineageId: 30)
        {
            YearsSinceFounded = 9,
            CurrentPressureSummary = "holding together after migration",
            IsFallbackCreated = true
        };
        fallbackPolity.EstablishFirstSettlement(2, "Ash Hills Hearth");
        fallbackPolity.AddDiscovery(new CulturalDiscovery("ash", "The uplands hold good stone", CulturalDiscoveryCategory.Geography, RegionId: 2));
        fallbackPolity.AddDiscovery(new CulturalDiscovery("roots", "Stored bulbs survive the dry season", CulturalDiscoveryCategory.FoodSafety, RegionId: 2));
        world.Polities.Add(fallbackPolity);
        world.FocalCandidateProfiles.Add(new FocalCandidateProfile(4, 30, 3, 9, 1, "solid", 2, "holding together after migration", "ash, roots", "descended from an older split", StabilityBand.Stable, true));

        PlayerEntryCandidateGenerator generator = new(new WorldGenerationSettings());

        IReadOnlyList<PlayerEntryCandidateSummary> candidates = generator.Generate(world, allowEmergencyFallback: true, out _);

        Assert.Contains(candidates, candidate => candidate.PolityId == 1 && !candidate.IsFallbackCandidate);
        Assert.Contains(candidates, candidate => candidate.PolityId == 4 && candidate.IsFallbackCandidate);
    }

    [Fact]
    public void CandidateDiversitySelection_PrefersDistinctHealthyStartOverNearDuplicate()
    {
        World world = CreateDiverseCandidateWorld();
        PlayerEntryCandidateGenerator generator = new(new WorldGenerationSettings());

        IReadOnlyList<PlayerEntryCandidateSummary> candidates = generator.Generate(world, allowEmergencyFallback: false, out _);

        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, candidate => candidate.PolityId == 1);
        Assert.Contains(candidates, candidate => candidate.PolityId == 3);
        Assert.DoesNotContain(candidates, candidate => candidate.PolityId == 2);
    }

    [Fact]
    public void CandidateSummaries_SurfaceSettlementRegionalAndLineageProfiles()
    {
        World world = CreateDiverseCandidateWorld();
        PlayerEntryCandidateGenerator generator = new(new WorldGenerationSettings());

        IReadOnlyList<PlayerEntryCandidateSummary> candidates = generator.Generate(world, allowEmergencyFallback: false, out _);
        PlayerEntryCandidateSummary distinct = Assert.Single(candidates, candidate => candidate.PolityId == 3);

        Assert.Equal("single frontier hearth", distinct.SettlementProfile);
        Assert.Contains("dry frontier", distinct.RegionalProfile, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("branch", distinct.LineageProfile, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CandidateSummaries_UseCurrentPolitySubsistenceInsteadOfFounderOrigin()
    {
        World world = CreateDiverseCandidateWorld();
        PlayerEntryCandidateGenerator generator = new(new WorldGenerationSettings());

        IReadOnlyList<PlayerEntryCandidateSummary> candidates = generator.Generate(world, allowEmergencyFallback: false, out _);
        PlayerEntryCandidateSummary basin = Assert.Single(candidates, candidate => candidate.PolityId == 1);

        Assert.Equal("Foraging-focused", basin.SubsistenceStyle);
        Assert.DoesNotContain("farming", basin.SubsistenceStyle, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CandidateSummaries_CanSurfaceHuntingFocusedCurrentProfile()
    {
        World world = CreateHuntingCandidateWorld();
        PlayerEntryCandidateGenerator generator = new(new WorldGenerationSettings());

        IReadOnlyList<PlayerEntryCandidateSummary> candidates = generator.Generate(world, allowEmergencyFallback: false, out _);
        PlayerEntryCandidateSummary hunter = Assert.Single(candidates);

        Assert.Equal("Hunting-focused", hunter.SubsistenceStyle);
        Assert.Contains("frontier", hunter.SettlementProfile, StringComparison.OrdinalIgnoreCase);
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
    public void HealthySeededWorlds_SurfaceMultipleOrganicChoices()
    {
        foreach (int seed in new[] { 7, 43 })
        {
            World world = new WorldGenerator(seed).Generate();

            Assert.True(world.PlayerEntryCandidates.Count(candidate => !candidate.IsFallbackCandidate) >= 2);
            Assert.DoesNotContain(world.PlayerEntryCandidates, candidate => candidate.IsFallbackCandidate);
        }
    }

    [Fact]
    public void Simulation_BindsChosenPolity_AndSetsChronicleBoundary()
    {
        World world = new WorldGenerator(seed: 43).Generate();
        using Simulation simulation = new(world, new SimulationOptions { OutputMode = OutputMode.Debug, WriteStructuredHistory = false });

        simulation.RunMonths(1);

        Assert.Equal(WorldStartupStage.ActivePlay, world.StartupStage);
        Assert.NotNull(world.SelectedFocalPolityId);
        Assert.NotNull(world.ActivePlayHandoff.PlayerEntryWorldYear);
        Assert.NotNull(world.ActivePlayHandoff.PlayerEntryPolityAge);
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

    private static World CreateEmergencyAdmissionWorld()
    {
        World world = new(new WorldTime(820, 1), WorldSimulationPhase.Bootstrap)
        {
            StartupStage = WorldStartupStage.PlayerEntryEvaluation,
            StartupAgeConfiguration = StartupWorldAgeConfiguration.ForPreset(StartupWorldAgePreset.StandardWorld)
        };

        world.Species.Add(new Species(1, "River Folk", 0.8, 0.7) { SentienceCapability = SentienceCapabilityState.Capable });
        world.Regions.Add(new Region(0, "Green Basin") { Biome = RegionBiome.RiverValley, Fertility = 0.8, WaterAvailability = 0.7 });

        Polity pressured = new(1, "Green Basin Confederacy", 1, 0, 70, lineageId: 10)
        {
            YearsSinceFounded = 2,
            CurrentPressureSummary = "frontier strain"
        };
        pressured.EstablishFirstSettlement(0, "Green Basin Hearth");
        pressured.AddDiscovery(new CulturalDiscovery("water", "Reliable basin water", CulturalDiscoveryCategory.Geography, RegionId: 0));
        pressured.AddDiscovery(new CulturalDiscovery("grain", "Edible marsh grain", CulturalDiscoveryCategory.FoodSafety, RegionId: 0));
        pressured.AddDiscovery(new CulturalDiscovery("elk", "Marsh elk routes are reliable", CulturalDiscoveryCategory.SpeciesUse, RegionId: 0));
        world.Polities.Add(pressured);
        world.FocalCandidateProfiles.Add(new FocalCandidateProfile(1, 10, 1, 2, 1, "growing", 0, "frontier strain", "water, grain", "held the basin together", StabilityBand.Stable, true));

        return world;
    }

    private static World CreateSelectionWorld()
    {
        World world = CreateCandidateWorld();
        world.StartupStage = WorldStartupStage.FocalSelection;
        world.PrehistoryRuntime.LastCheckpointOutcome = PrehistoryCheckpointOutcome.EnterFocalSelection("world_readiness_passed");
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
            "paired hearths",
            "river valley, rich water and fertile ground",
            "deep descendant branch; river valley fit 0.88",
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

    private static World CreateDiverseCandidateWorld()
    {
        World world = new(new WorldTime(860, 1), WorldSimulationPhase.Bootstrap)
        {
            StartupStage = WorldStartupStage.PlayerEntryEvaluation,
            StartupAgeConfiguration = new StartupWorldAgeConfiguration(
                StartupWorldAgePreset.StandardWorld,
                MinPrehistoryYears: 700,
                TargetPrehistoryYears: 1000,
                MaxPrehistoryYears: 1400,
                ReadinessStrictness: 1.0,
                CandidateCountTarget: 2)
        };

        world.Species.Add(new Species(1, "River Folk", 0.8, 0.7)
        {
            SentienceCapability = SentienceCapabilityState.Capable,
            EcologyNiche = "river gatherers"
        });
        world.Species.Add(new Species(2, "Steppe Folk", 0.78, 0.62)
        {
            SentienceCapability = SentienceCapabilityState.Capable,
            EcologyNiche = "dryland hunters"
        });
        world.EvolutionaryLineages.Add(new EvolutionaryLineage(10, 1, "river gatherers", TrophicRole.Omnivore)
        {
            ParentLineageId = 8,
            AncestryDepth = 2,
            HabitatAdaptationSummary = "river valley fit 0.88",
            AdaptationPressureSummary = "food instability"
        });
        world.EvolutionaryLineages.Add(new EvolutionaryLineage(20, 2, "dryland hunters", TrophicRole.Omnivore)
        {
            ParentLineageId = 18,
            AncestryDepth = 1,
            HabitatAdaptationSummary = "drylands fit 0.79",
            AdaptationPressureSummary = "climate mismatch"
        });

        world.Regions.Add(new Region(0, "Green Basin") { Biome = RegionBiome.RiverValley, Fertility = 0.82, WaterAvailability = 0.76 });
        world.Regions.Add(new Region(1, "Silver Ford") { Biome = RegionBiome.RiverValley, Fertility = 0.78, WaterAvailability = 0.72 });
        world.Regions.Add(new Region(2, "Stone Steppe") { Biome = RegionBiome.Drylands, Fertility = 0.34, WaterAvailability = 0.22 });

        world.Societies.Add(new EmergingSociety(1)
        {
            LineageId = 10,
            SpeciesId = 1,
            SubsistenceMode = SubsistenceMode.ProtoFarming
        });
        world.Societies.Add(new EmergingSociety(2)
        {
            LineageId = 10,
            SpeciesId = 1,
            SubsistenceMode = SubsistenceMode.ProtoFarming
        });
        world.Societies.Add(new EmergingSociety(3)
        {
            LineageId = 20,
            SpeciesId = 2,
            SubsistenceMode = SubsistenceMode.HuntingFocused
        });

        Polity basin = new(1, "Green Basin Confederacy", 1, 0, 260, lineageId: 10)
        {
            FounderSocietyId = 1,
            YearsSinceFounded = 14,
            CurrentPressureSummary = "anchoring on rich ground"
        };
        basin.EstablishFirstSettlement(0, "Green Basin Hearth");
        basin.AddSettlement(0, "Green Basin Ford");
        basin.AddDiscovery(new CulturalDiscovery("water", "Reliable basin water", CulturalDiscoveryCategory.Geography, RegionId: 0));
        basin.AddDiscovery(new CulturalDiscovery("grain", "Edible marsh grain", CulturalDiscoveryCategory.FoodSafety, RegionId: 0));
        basin.AddDiscovery(new CulturalDiscovery("elk", "Marsh elk routes are reliable", CulturalDiscoveryCategory.SpeciesUse, RegionId: 0));
        world.Polities.Add(basin);

        Polity ford = new(2, "Silver Ford Confederacy", 1, 1, 240, lineageId: 10)
        {
            FounderSocietyId = 2,
            YearsSinceFounded = 13,
            CurrentPressureSummary = "anchoring on rich ground"
        };
        ford.EstablishFirstSettlement(1, "Silver Ford Hearth");
        ford.AddSettlement(1, "Silver Ford Ford");
        ford.AddDiscovery(new CulturalDiscovery("water2", "Reliable ford water", CulturalDiscoveryCategory.Geography, RegionId: 1));
        ford.AddDiscovery(new CulturalDiscovery("grain2", "Floodplain grain stores well", CulturalDiscoveryCategory.FoodSafety, RegionId: 1));
        ford.AddDiscovery(new CulturalDiscovery("reed2", "River reeds make good baskets", CulturalDiscoveryCategory.SpeciesUse, RegionId: 1));
        world.Polities.Add(ford);

        Polity steppe = new(3, "Stone Steppe Riders", 2, 2, 188, lineageId: 20)
        {
            FounderSocietyId = 3,
            YearsSinceFounded = 9,
            CurrentPressureSummary = "frontier strain"
        };
        steppe.EstablishFirstSettlement(2, "Stone Steppe Hearth");
        steppe.AddDiscovery(new CulturalDiscovery("salt", "Salt pans survive the dry months", CulturalDiscoveryCategory.Geography, RegionId: 2));
        steppe.AddDiscovery(new CulturalDiscovery("herds", "Steppe herds move through the dry season", CulturalDiscoveryCategory.SpeciesUse, RegionId: 2));
        steppe.AddDiscovery(new CulturalDiscovery("wind", "Wind shelters matter in the steppe", CulturalDiscoveryCategory.Environment, RegionId: 2));
        world.Polities.Add(steppe);

        world.FocalCandidateProfiles.Add(new FocalCandidateProfile(1, 10, 1, 14, 2, "large", 0, "anchoring on rich ground", "water, grain", "founded a second hearth", StabilityBand.Strong, true));
        world.FocalCandidateProfiles.Add(new FocalCandidateProfile(2, 10, 1, 13, 2, "solid", 1, "anchoring on rich ground", "water, grain", "held the ford together", StabilityBand.Strong, true));
        world.FocalCandidateProfiles.Add(new FocalCandidateProfile(3, 20, 2, 9, 1, "solid", 2, "frontier strain", "salt, herds", "shifted into new ground", StabilityBand.Stable, true));

        return world;
    }

    private static World CreateWeakFallbackWorld()
    {
        World world = CreateSelectionWorld();
        world.PrehistoryRuntime.LastCheckpointOutcome = PrehistoryCheckpointOutcome.ForceEnterFocalSelection("max_prehistory_age_reached");
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
            "single hearth",
            "river valley, rich water and fertile ground",
            "younger descendant branch; mixed adaptation",
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

    private static World CreateHuntingCandidateWorld()
    {
        World world = new(new WorldTime(860, 1), WorldSimulationPhase.Bootstrap)
        {
            StartupStage = WorldStartupStage.PlayerEntryEvaluation,
            StartupAgeConfiguration = StartupWorldAgeConfiguration.ForPreset(StartupWorldAgePreset.StandardWorld)
        };

        Species hunters = new(8, "Ash Riders", 0.74, 0.60)
        {
            SentienceCapability = SentienceCapabilityState.Capable,
            EcologyNiche = "dryland hunters",
            PlantBiomassAffinity = 0.22,
            AnimalBiomassAffinity = 0.88
        };
        world.Species.Add(hunters);
        world.EvolutionaryLineages.Add(new EvolutionaryLineage(80, 8, "dryland hunters", TrophicRole.Omnivore)
        {
            ParentLineageId = 70,
            AncestryDepth = 2,
            HabitatAdaptationSummary = "drylands fit 0.84",
            AdaptationPressureSummary = "predation pressure"
        });

        world.Regions.Add(new Region(0, "Ash Steppe")
        {
            Biome = RegionBiome.Drylands,
            Fertility = 0.28,
            WaterAvailability = 0.20
        });

        world.Societies.Add(new EmergingSociety(8)
        {
            LineageId = 80,
            SpeciesId = 8,
            SubsistenceMode = SubsistenceMode.ProtoFarming
        });

        Polity polity = new(8, "Ash Steppe Riders", 8, 0, 250, lineageId: 80)
        {
            FounderSocietyId = 8,
            YearsSinceFounded = 14,
            MigrationPressure = 0.24,
            FragmentationPressure = 0.10,
            CurrentPressureSummary = "frontier strain"
        };
        polity.EstablishFirstSettlement(0, "Ash Steppe Hearth");
        polity.SuccessfulHuntsBySpecies[44] = 6;
        polity.KnownDangerousPreySpeciesIds.Add(44);
        polity.AddDiscovery(new CulturalDiscovery("salt", "Dry wells and salt pans mark the route", CulturalDiscoveryCategory.Geography, RegionId: 0));
        polity.AddDiscovery(new CulturalDiscovery("herds", "The herd-beasts cross the ash flats at dusk", CulturalDiscoveryCategory.SpeciesUse, RegionId: 0, SpeciesId: 44));
        polity.AddDiscovery(new CulturalDiscovery("bone", "Bone points hold better against the steppe herds", CulturalDiscoveryCategory.Resource, RegionId: 0));
        world.Polities.Add(polity);
        world.FocalCandidateProfiles.Add(new FocalCandidateProfile(8, 80, 8, 14, 1, "solid", 0, "frontier strain", "salt, herds", "followed the herd trail", StabilityBand.Strong, true));

        return world;
    }
}
