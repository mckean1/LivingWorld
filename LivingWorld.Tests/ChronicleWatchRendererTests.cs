using System;
using System.Collections.Generic;
using LivingWorld.Core;
using LivingWorld.Generation;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Presentation;
using LivingWorld.Societies;
using Xunit;

namespace LivingWorld.Tests;

public sealed class ChronicleWatchRendererTests
{
    [Fact]
    public void BuildStatusLines_ShowsFrozenFocalSelectionState()
    {
        World world = new(new WorldTime(90, 1));
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.FocalSelection;
        world.PrehistoryRuntime.ActivitySummary = "Refreshing the focal list";
        world.PrehistoryRuntime.LastCheckpointOutcome = PrehistoryCheckpointOutcome.EnterFocalSelection("ready", "almost there");
        world.PlayerEntryCandidates.Add(CreateCandidate(101));

        IReadOnlyList<string> lines = ChronicleWatchRenderer.BuildStatusLines(
            world,
            polity: null,
            new WatchUiState(),
            width: 80,
            stageNameFormatter: stage => stage.ToString());

        Assert.Contains(" Chronicle Watch - Focal selection (time paused)", lines);
        Assert.Contains(" Candidate pool: 1 viable start(s) available", lines);
        Assert.Contains(" Handoff: awaiting player selection", lines);
        Assert.Contains(lines, line => line.Contains("Checkpoint: EnterFocalSelection", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildStatusLines_ShowsActivePlayHandoffSummary()
    {
        World world = CreateActivePlayWorld();
        world.BeginActiveSimulation();

        Polity polity = Assert.Single(world.Polities);

        IReadOnlyList<string> lines = ChronicleWatchRenderer.BuildStatusLines(
            world,
            polity,
            new WatchUiState(),
            width: 80,
            stageNameFormatter: stage => stage.ToString());

        Assert.Contains($" Chronicle boundary: {world.LiveChronicleStartYear}", lines);
        Assert.Contains(lines, line => line.Contains("Handoff summary:", StringComparison.Ordinal));
        Assert.Contains(" Control: Society | AnchoredHomeRange", lines);
    }

    [Fact]
    public void BuildStatusLines_GenerationFailureIsHonest()
    {
        World world = new(new WorldTime(70, 4));
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.GenerationFailure;
        world.PrehistoryRuntime.ActivitySummary = "Unable to surface viable starts.";
        world.PrehistoryRuntime.LastCheckpointOutcome = PrehistoryCheckpointOutcome.Failure("world_failed");

        IReadOnlyList<string> lines = ChronicleWatchRenderer.BuildStatusLines(
            world,
            polity: null,
            new WatchUiState(),
            width: 80,
            stageNameFormatter: stage => stage.ToString());

        Assert.Contains(" World Generation Failure", lines);
        Assert.Contains(" Details: world_failed", lines);
        Assert.Contains(lines, line => line.Contains("honest failure", StringComparison.Ordinal));
    }

    private static PlayerEntryCandidateSummary CreateCandidate(int polityId)
        => new(
            polityId,
            $"Polity {polityId}",
            polityId,
            $"Species {polityId}",
            polityId,
            polityId,
            $"Region {polityId}",
            6,
            30,
            2,
            "medium",
            "Mixed",
            "Condition",
            "Settlement",
            "Regional",
            "Lineage",
            "Discovery",
            "Learned",
            "Note",
            "Opportunity",
            0.5,
            StabilityBand.Stable,
            false);

    private static World CreateActivePlayWorld()
    {
        World world = new(new WorldTime(920, 6));
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.ActivePlay;
        world.PrehistoryRuntime.ActivitySummary = "Chronicle running";
        world.PrehistoryRuntime.PhaseLabel = "Active play";

        world.Regions.Add(new Region(0, "Green Basin") { Biome = RegionBiome.RiverValley, Fertility = 0.72, WaterAvailability = 0.68 });
        world.Regions.Add(new Region(1, "Stone Shelf") { Biome = RegionBiome.Highlands, Fertility = 0.61, WaterAvailability = 0.52 });
        world.Regions.Add(new Region(2, "River Road") { Biome = RegionBiome.Plains, Fertility = 0.65, WaterAvailability = 0.58 });
        world.Regions[0].AddConnection(1);
        world.Regions[1].AddConnection(0);
        world.Regions[1].AddConnection(2);
        world.Regions[2].AddConnection(1);

        world.Species.Add(new Species(1, "Humans", 0.8, 0.8) { TrophicRole = TrophicRole.Omnivore });
        world.Species.Add(new Species(2, "Highland Kin", 0.8, 0.8) { TrophicRole = TrophicRole.Omnivore });

        Polity polity = new(23, "Focus Polity", 1, 0, 120, stage: PolityStage.Tribe)
        {
            LineageId = 77,
            YearsSinceFounded = 6,
            SettlementStatus = SettlementStatus.SemiSettled
        };
        polity.EstablishFirstSettlement(0, "Green Hearth");
        polity.AddSettlement(1, "Stone Watch");
        polity.AddDiscovery(new CulturalDiscovery("region:2", "River road", CulturalDiscoveryCategory.Geography, null, 2));
        world.Polities.Add(polity);

        world.PlayerEntryCandidates.Add(new PlayerEntryCandidateSummary(
            23,
            "Focus Polity",
            1,
            "Humans",
            77,
            0,
            "Green Basin",
            6,
            920,
            2,
            "Established",
            "Mixed hunter-forager",
            "Holding",
            "anchored hearth network",
            "river valley with usable local support",
            "deep descendant branch; mixed adaptation",
            "River road",
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
            "Focus Polity is an anchored mixed hunter-forager start on river valley ground, with stable support and deep continuity.",
            ["Reliable current support"],
            ["Thin durability for a normal stop"],
            ["Recent shock may still cascade"],
            new CandidateScoreBreakdown(0.71, 0.72, 0.69, 0.64, 0.41, 0.56, 0.18, 0.61, CandidateScoreTier.Strong, "Runs on continuity depth, with no major drag beyond external entanglement."),
            ["maturity:anchored", "home:river_valley"],
            "dup-key"));

        world.PrehistoryObserver.Upsert(new PeopleMonthlySnapshot(
            23, "Focus Polity", 1, 77, 920, 6, (920 * 12) + 6, 120, 0, 0, [0, 1], 0, 2, 1, 1, 0, 0, 0.74, 0.82, 0.33, 0.04, 2, 48, 40, 44, 0.92, 0.95, 0.00, 0.10, 144, 120, 1, 1, 1, 0.18, 0.12,
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
                    new PeopleSnapshotHeader(23, "Focus Polity", 1, 77, 920, 6),
                    new SnapshotWindowAvailability(12, 24, true, 3, 6, 12, 12),
                    new CurrentPeopleState(120, 0, 2, 0.92, 0.95, 0.18, 0.12, 0.82, 0.33, 0.04, 0.74, true, false, false, false, false, false, false, true, true, 0, 1, 0),
                    new DemographyHistoryRollup(120, 114, 108, 104, 4, 1, 96),
                    new SupportHistoryRollup(0.92, 0.90, 0.88, 0.91, 0, 1, 2, 2, 0, 0, 0),
                    new SpatialHistoryRollup(2, 2.0, 0.82, 0.79, 0.33, 0.28, 0.24, 0.04, 1, 2),
                    new RootednessHistoryRollup(6, 12, 18, 5, 0.74, 12, 0, 0),
                    new SocialContinuityHistoryRollup(24, 24, 0, 0, 0, false),
                    new SettlementHistoryRollup(2, 6, 12, 18, 12, 18, 0, 0, 1, 96),
                    new PoliticalHistoryRollup(PolityStage.Tribe, SettlementStatus.SemiSettled, 9, 4, 12, 12, 5),
                    new ActionableSignalHistoryRollup(1, 2, 0, 1, 3, 6, 0, 0),
                    new HistoryShockMarkers(false, 0, 0, 0, false, 0, 0, 0, false, 0, 0, 0, false, 0, 0, 0, false, 0, 0, 0),
                    new EvaluatorHealthSummary(
                        new DemographicHealthSummary(120, 114, 108, 1, 96, 0),
                        new SupportStabilityHealth(SupportStabilityState.Stable, 0.92, 0.90, 0.88, 0.91, 0, 1, false, 0, 0, false),
                        new MovementCoherenceHealth(MovementCoherenceState.Coherent, 0.82, 0.33, 0.04, 1.08, 0.28, 0.24, 6, 10, 0, 0),
                        new RootednessHealth(RootednessState.Rooted, 12, 5, 0.74, 12, false, 0, 0, false),
                        new ContinuityHealth(ContinuityState.Deep, 24, 24, 0, 0, 0, false)))
            ],
            [
                new RegionEvaluationSnapshot(
                    23, 920, 6,
                    new RegionGlobalEvaluation(0, "Green Basin", RegionBiome.RiverValley.ToString(), 0.72, 0.68, 0.80, 0.55, 0.78, 1, 1, 1, 0.12, 0.08),
                    new PeopleRegionEvaluation(23, PeopleRegionRelationshipType.HomeCore, true, true, false, 12, 0.58, 0.94, 0.72, 0.18, 0.10, 2, 4)),
                new RegionEvaluationSnapshot(
                    23, 920, 6,
                    new RegionGlobalEvaluation(1, "Stone Shelf", RegionBiome.Highlands.ToString(), 0.61, 0.52, 0.70, 0.42, 0.64, 2, 1, 1, 0.16, 0.10),
                    new PeopleRegionEvaluation(23, PeopleRegionRelationshipType.Occupied, false, true, false, 8, 0.32, 0.82, 0.61, 0.34, 0.18, 1, 2)),
                new RegionEvaluationSnapshot(
                    23, 920, 6,
                    new RegionGlobalEvaluation(2, "River Road", RegionBiome.Plains.ToString(), 0.65, 0.58, 0.74, 0.46, 0.68, 1, 0, 0, 0.10, 0.05),
                    new PeopleRegionEvaluation(23, PeopleRegionRelationshipType.SeasonalRoute, false, false, false, 6, 0.10, 0.68, 0.55, 0.44, 0.16, 3, 1))
            ],
            [
                new NeighborContextSnapshot(
                    23,
                    920,
                    6,
                    new NeighborhoodSummary(1, 1, 1, 0, 1, 0),
                    [new NeighborRelationshipSnapshot(23, 31, "Stone Chorus", 2, 88, 1, 1, true, true, false, true, false, false, 1, 2, 0.84, 0.22, ["exchange_context"])],
                    new NeighborAggregateMetrics(140, 0, 1, 1.0, 0.22))
            ],
            "snapshot");

        ActivePlayHandoffPackage handoffPackage = new ActivePlayHandoffBuilder().Build(world, polity.Id);
        world.ActivePlayHandoff.RecordPackage(handoffPackage);
        return world;
    }
}
