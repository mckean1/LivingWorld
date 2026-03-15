using LivingWorld.Core;
using LivingWorld.Generation;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Presentation;
using LivingWorld.Societies;
using Xunit;

namespace LivingWorld.Tests;

public sealed class SimulationHandoffTests
{
    [Fact]
    public void FocalSelectionHandoff_DoesNotAdvanceMonthAndStartsPaused()
    {
        World world = CreateFocalSelectionWorld();
        int yearAtSelection = world.Time.Year;
        int monthAtSelection = world.Time.Month;

        using Simulation simulation = new(world, new SimulationOptions
        {
            OutputMode = OutputMode.Debug,
            WriteStructuredHistory = false
        });
        int eventCountAtSelection = world.Events.Count;

        simulation.RunMonths(1);

        Assert.Equal(PrehistoryRuntimePhase.ActivePlay, world.PrehistoryRuntime.CurrentPhase);
        Assert.Equal(yearAtSelection, world.Time.Year);
        Assert.Equal(monthAtSelection, world.Time.Month);
        Assert.True(simulation.IsWatchPaused);
        Assert.NotNull(world.ActivePlayHandoff.Package);
        Assert.True(world.ActivePlayHandoff.Package!.PlayerOwnership.StartsPaused);
        Assert.Equal(eventCountAtSelection, world.Events.Count);
        Assert.Equal(world.SelectedFocalPolityId, world.ActivePlayHandoff.Package.PlayerOwnership.SelectedPeopleId);
        Assert.NotNull(world.ActiveControl);
        Assert.True(world.IsActiveControlBackingPolity(world.ActivePlayHandoff.Package.StartingControl.SourcePolityId));
        Assert.Equal(world.ActivePlayHandoff.Package.StartingControl.SourcePolityId, world.ActiveControl!.SourcePolityId);
        Assert.Equal(world.ActivePlayHandoff.Package.PlayerOwnership.HomeRegionId, world.ActiveControl.HomeRegionId);
    }

    private static World CreateFocalSelectionWorld()
    {
        World world = new(new WorldTime(920, 6))
        {
            StartupStage = WorldStartupStage.FocalSelection
        };
        world.StartupAgeConfiguration = StartupWorldAgeConfiguration.ForPreset(StartupWorldAgePreset.StandardWorld);
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.FocalSelection;
        world.PrehistoryRuntime.DetailView = PrehistoryRuntimeDetailView.FocalSelection;
        world.PrehistoryRuntime.PhaseLabel = "Focal selection";
        world.PrehistoryRuntime.ActivitySummary = "Reviewing truthful candidate starts.";
        world.PrehistoryRuntime.IsPrehistoryAdvancing = false;
        world.PrehistoryRuntime.LastCheckpointOutcome = PrehistoryCheckpointOutcome.EnterFocalSelection("ready");
        world.WorldReadinessReport = new WorldReadinessReport(
            new WorldAgeGateReport(920, 700, 900, 1200, PrehistoryAgeGateStatus.TargetAgeReached),
            PrehistoryCheckpointOutcomeKind.EnterFocalSelection,
            WorldReadinessReport.Empty.CategoryResults,
            new CandidatePoolReadinessSummary(1, 1, 1, 1, 0, 1, 1, 1, 1, false, "candidate summary"),
            Array.Empty<string>(),
            Array.Empty<string>(),
            false,
            false,
            new WorldReadinessSummaryData("headline", "candidate headline", "world condition", 1, 0, 0));

        Region greenBasin = new(0, "Green Basin") { Biome = RegionBiome.RiverValley, Fertility = 0.72, WaterAvailability = 0.68 };
        Region stoneShelf = new(1, "Stone Shelf") { Biome = RegionBiome.Highlands, Fertility = 0.61, WaterAvailability = 0.52 };
        Region riverRoad = new(2, "River Road") { Biome = RegionBiome.Plains, Fertility = 0.65, WaterAvailability = 0.58 };
        greenBasin.AddConnection(1);
        stoneShelf.AddConnection(0);
        stoneShelf.AddConnection(2);
        riverRoad.AddConnection(1);
        world.Regions.Add(greenBasin);
        world.Regions.Add(stoneShelf);
        world.Regions.Add(riverRoad);

        world.Species.Add(new Species(1, "Humans", 0.8, 0.8) { TrophicRole = TrophicRole.Omnivore });
        Polity polity = new(10, "River Hearth", 1, 0, 180, stage: PolityStage.Tribe)
        {
            LineageId = 77,
            YearsSinceFounded = 18,
            SettlementStatus = SettlementStatus.SemiSettled
        };
        polity.EstablishFirstSettlement(0, "Green Hearth");
        polity.AddSettlement(1, "Stone Watch");
        world.Polities.Add(polity);

        world.PlayerEntryCandidates.Add(new PlayerEntryCandidateSummary(
            10,
            "River Hearth",
            1,
            "Humans",
            77,
            0,
            "Green Basin",
            18,
            920,
            2,
            "Established",
            "Mixed hunter-forager",
            "Holding",
            "anchored hearth network",
            "river valley with usable local support",
            "deep descendant branch; mixed adaptation",
            "River crossing",
            "Seasonal Planning",
            "recently consolidated into a polity",
            "exchange edges are already visible",
            0.61,
            StabilityBand.Stable,
            false,
            false,
            string.Empty,
            new CandidateViabilityResult(
                true,
                true,
                [new CandidateViabilityGate("support", true, "Current support must pass", "Support is stable.")],
                Array.Empty<string>(),
                Array.Empty<string>(),
                string.Empty,
                "Meets the hard truth floor and normal-entry durability gates."),
            CandidateMaturityBand.Anchored,
            "rooted coherence",
            "Anchored mixed hunter-forager in river valley",
            "Solid anchored start with clear internal shape.",
            "River Hearth is an anchored mixed hunter-forager start on river valley ground, with stable support and deep continuity.",
            ["Reliable current support"],
            ["Thin durability for a normal stop"],
            ["Recent shock may still cascade"],
            new CandidateScoreBreakdown(0.71, 0.72, 0.69, 0.64, 0.41, 0.56, 0.18, 0.61, CandidateScoreTier.Strong, "Runs on continuity depth, with no major drag beyond external entanglement."),
            ["maturity:anchored", "home:river_valley"],
            "dup-key"));

        world.PrehistoryObserver.Upsert(new PeopleMonthlySnapshot(
            10, "River Hearth", 1, 77, 920, 6, (920 * 12) + 6, 180, 0, 0, [0, 1], 0, 2, 1, 1, 0, 0, 0.74, 0.82, 0.33, 0.04, 2, 48, 40, 44, 0.92, 0.95, 0.00, 0.10, 144, 120, 1, 1, 1, 0.18, 0.12,
            SettlementStatus.SemiSettled, PolityStage.Tribe,
            HasManagedFood: false, HasAgriculture: false, HasFoodStorage: true, HasSeasonalPlanning: true,
            IsAnchoredThisMonth: true, IsStrongAnchoredThisMonth: false, ExpansionOpportunityThisMonth: true, TradeContactThisMonth: true,
            MovedThisMonth: false, SupportCrashThisMonth: false, DisplacementThisMonth: false, SettlementLossThisMonth: false,
            CollapseMarkerThisMonth: false, IdentityBreakThisMonth: false, ActiveIdentityBreakNow: false,
            ContinuousIdentityMonthsObserved: 24, RelevantNeighborCount: 1, AdjacentNeighborCount: 1, ReachableNeighborCount: 1, PressureNeighborCount: 0));
        world.PrehistoryEvaluation.LatestObserverSnapshot = new PrehistoryObserverSnapshot(
            920,
            6,
            [
                new PeopleHistoryWindowSnapshot(
                    new PeopleSnapshotHeader(10, "River Hearth", 1, 77, 920, 6),
                    new SnapshotWindowAvailability(12, 24, true, 3, 6, 12, 12),
                    new CurrentPeopleState(180, 0, 2, 0.92, 0.95, 0.18, 0.12, 0.82, 0.33, 0.04, 0.74, true, false, false, false, false, false, false, true, true, 0, 1, 0),
                    new DemographyHistoryRollup(180, 166, 154, 148, 4, 1, 132),
                    new SupportHistoryRollup(0.92, 0.90, 0.88, 0.91, 0, 1, 2, 2, 0, 0, 0),
                    new SpatialHistoryRollup(2, 2.0, 0.82, 0.79, 0.33, 0.28, 0.24, 0.04, 1, 2),
                    new RootednessHistoryRollup(6, 12, 18, 5, 0.74, 12, 0, 0),
                    new SocialContinuityHistoryRollup(24, 24, 0, 0, 0, false),
                    new SettlementHistoryRollup(2, 6, 12, 18, 12, 18, 0, 0, 1, 132),
                    new PoliticalHistoryRollup(PolityStage.Tribe, SettlementStatus.SemiSettled, 9, 4, 12, 12, 5),
                    new ActionableSignalHistoryRollup(1, 2, 0, 1, 3, 6, 0, 0),
                    new HistoryShockMarkers(false, 0, 0, 0, false, 0, 0, 0, false, 0, 0, 0, false, 0, 0, 0, false, 0, 0, 0),
                    new EvaluatorHealthSummary(
                        new DemographicHealthSummary(180, 166, 154, 1, 132, 0),
                        new SupportStabilityHealth(SupportStabilityState.Stable, 0.92, 0.90, 0.88, 0.91, 0, 1, false, 0, 0, false),
                        new MovementCoherenceHealth(MovementCoherenceState.Coherent, 0.82, 0.33, 0.04, 1.08, 0.28, 0.24, 6, 10, 0, 0),
                        new RootednessHealth(RootednessState.Rooted, 12, 5, 0.74, 12, false, 0, 0, false),
                        new ContinuityHealth(ContinuityState.Deep, 24, 24, 0, 0, 0, false)))
            ],
            [
                new RegionEvaluationSnapshot(
                    10, 920, 6,
                    new RegionGlobalEvaluation(0, "Green Basin", RegionBiome.RiverValley.ToString(), 0.72, 0.68, 0.80, 0.55, 0.78, 1, 1, 1, 0.12, 0.08),
                    new PeopleRegionEvaluation(10, PeopleRegionRelationshipType.HomeCore, true, true, false, 12, 0.58, 0.94, 0.72, 0.18, 0.10, 2, 4)),
                new RegionEvaluationSnapshot(
                    10, 920, 6,
                    new RegionGlobalEvaluation(1, "Stone Shelf", RegionBiome.Highlands.ToString(), 0.61, 0.52, 0.70, 0.42, 0.64, 2, 1, 1, 0.16, 0.10),
                    new PeopleRegionEvaluation(10, PeopleRegionRelationshipType.Occupied, false, true, false, 8, 0.32, 0.82, 0.61, 0.34, 0.18, 1, 2)),
                new RegionEvaluationSnapshot(
                    10, 920, 6,
                    new RegionGlobalEvaluation(2, "River Road", RegionBiome.Plains.ToString(), 0.65, 0.58, 0.74, 0.46, 0.68, 1, 0, 0, 0.10, 0.05),
                    new PeopleRegionEvaluation(10, PeopleRegionRelationshipType.SeasonalRoute, false, false, false, 6, 0.10, 0.68, 0.55, 0.44, 0.16, 3, 1))
            ],
            [
                new NeighborContextSnapshot(
                    10,
                    920,
                    6,
                    new NeighborhoodSummary(1, 1, 1, 0, 1, 0),
                    [new NeighborRelationshipSnapshot(10, 22, "Stone Chorus", 1, 88, 1, 1, true, true, false, true, false, false, 1, 2, 0.84, 0.22, ["exchange_context"])],
                    new NeighborAggregateMetrics(140, 0, 1, 1.0, 0.22))
            ],
            "snapshot");

        return world;
    }
}
