using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Generation;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;
using Xunit;

namespace LivingWorld.Tests;

public sealed class ActivePlayHandoffBuilderTests
{
    [Fact]
    public void Build_PreservesDiscoveriesAndLearnedSeparately()
    {
        World world = CreateWorld(CandidateMaturityBand.Anchored, polityGatePasses: false);
        Polity polity = world.Polities[0];
        polity.AddDiscovery(new CulturalDiscovery("region:1", "River crossing", CulturalDiscoveryCategory.Geography, null, 1));
        polity.LearnAdvancement(AdvancementId.SeasonalPlanning);
        polity.LearnAdvancement(AdvancementId.FoodStorage);

        ActivePlayHandoffPackage handoff = new ActivePlayHandoffBuilder().Build(world, polity.Id);

        Assert.Contains("River crossing", handoff.Knowledge.Discoveries);
        Assert.DoesNotContain("Seasonal Planning", handoff.Knowledge.Discoveries);
        Assert.Contains("Seasonal Planning", handoff.Knowledge.LearnedCapabilities);
        Assert.Contains("Storage", handoff.Knowledge.LearnedCapabilities);
    }

    [Theory]
    [InlineData(CandidateMaturityBand.Mobile)]
    [InlineData(CandidateMaturityBand.Anchored)]
    public void Build_MobileAndAnchoredStartsRemainSociety(CandidateMaturityBand maturityBand)
    {
        World world = CreateWorld(maturityBand, polityGatePasses: true);

        ActivePlayHandoffPackage handoff = new ActivePlayHandoffBuilder().Build(world, world.Polities[0].Id);

        Assert.Equal(ActiveControlKind.Society, handoff.StartingControl.Conversion.ControlKind);
    }

    [Fact]
    public void Build_SettlingStartOnlyBecomesPolityWhenFullGatePasses()
    {
        World blockedWorld = CreateWorld(CandidateMaturityBand.Settling, polityGatePasses: false);
        World passingWorld = CreateWorld(CandidateMaturityBand.Settling, polityGatePasses: true);
        ActivePlayHandoffBuilder builder = new();

        ActivePlayHandoffPackage blocked = builder.Build(blockedWorld, blockedWorld.Polities[0].Id);
        ActivePlayHandoffPackage passed = builder.Build(passingWorld, passingWorld.Polities[0].Id);

        Assert.Equal(ActiveControlKind.Society, blocked.StartingControl.Conversion.ControlKind);
        Assert.Equal(ActiveControlKind.Polity, passed.StartingControl.Conversion.ControlKind);
    }

    [Fact]
    public void Build_EmergentPolityFallsBackToSocietyWhenAuthorityIsThin()
    {
        World world = CreateWorld(CandidateMaturityBand.EmergentPolity, polityGatePasses: false);

        ActivePlayHandoffPackage handoff = new ActivePlayHandoffBuilder().Build(world, world.Polities[0].Id);

        Assert.Equal(ActiveControlKind.Society, handoff.StartingControl.Conversion.ControlKind);
        Assert.False(handoff.StartingControl.Conversion.PolityGatePassed);
    }

    [Fact]
    public void Build_PreservesRoutesSettlementsAndRegionRelations()
    {
        World world = CreateWorld(CandidateMaturityBand.Anchored, polityGatePasses: false);

        ActivePlayHandoffPackage handoff = new ActivePlayHandoffBuilder().Build(world, world.Polities[0].Id);

        Assert.Equal([0, 1], handoff.StartingControl.OccupiedRegionIds);
        Assert.Equal([2], handoff.StartingControl.RouteRegionIds);
        Assert.Contains(handoff.StartingControl.Settlements, settlement => settlement.RegionId == 0);
        Assert.Contains(handoff.StartingControl.RegionRelations, relation => relation.RegionId == 2 && relation.RelationKind == ActiveControlRegionRelationKind.RouteCorridor);
        Assert.Contains(handoff.StartingControl.RegionRelations, relation => relation.RegionId == 3 && relation.RelationKind == ActiveControlRegionRelationKind.OpportunityEdge);
    }

    [Fact]
    public void Build_CreatesCompactInheritedSummary()
    {
        World world = CreateWorld(CandidateMaturityBand.Anchored, polityGatePasses: false);

        ActivePlayHandoffPackage handoff = new ActivePlayHandoffBuilder().Build(world, world.Polities[0].Id);

        Assert.True(handoff.Chronicle.SummaryLines.Count <= 4);
        Assert.Contains("begins as a society", handoff.Chronicle.SummaryHeadline, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("polity_founded", string.Join(" ", handoff.Chronicle.SummaryLines), StringComparison.OrdinalIgnoreCase);
    }

    private static World CreateWorld(CandidateMaturityBand maturityBand, bool polityGatePasses)
    {
        World world = new(new WorldTime(920, 6));
        world.Regions.Add(CreateRegion(0, "Green Basin"));
        world.Regions.Add(CreateRegion(1, "Stone Shelf"));
        world.Regions.Add(CreateRegion(2, "River Road"));
        world.Regions.Add(CreateRegion(3, "Frontier Verge"));
        world.Regions[0].AddConnection(1);
        world.Regions[1].AddConnection(0);
        world.Regions[1].AddConnection(2);
        world.Regions[2].AddConnection(1);
        world.Regions[2].AddConnection(3);
        world.Regions[3].AddConnection(2);

        world.Species.Add(new Species(1, "Humans", 0.8, 0.8) { TrophicRole = TrophicRole.Omnivore });
        world.Species.Add(new Species(2, "Highland Kin", 0.8, 0.8) { TrophicRole = TrophicRole.Omnivore });

        Polity polity = new(10, "River Hearth", 1, 0, 180, stage: polityGatePasses ? PolityStage.Tribe : PolityStage.Band)
        {
            LineageId = 77,
            YearsSinceFounded = 18,
            SettlementStatus = polityGatePasses ? SettlementStatus.Settled : SettlementStatus.SemiSettled
        };
        polity.EstablishFirstSettlement(0, "Green Hearth");
        polity.AddSettlement(1, "Stone Watch");
        polity.AddDiscovery(new CulturalDiscovery("species:2", "Highland kin sightings", CulturalDiscoveryCategory.SpeciesUse, 2, null));
        world.Polities.Add(polity);

        world.PlayerEntryCandidates.Add(CreateCandidateSummary(maturityBand));
        world.PrehistoryObserver.Upsert(CreateMonthlySnapshot(polity.Id));
        world.PrehistoryEvaluation.LatestObserverSnapshot = CreateObserverSnapshot(polity.Id, polityGatePasses);
        return world;
    }

    private static PlayerEntryCandidateSummary CreateCandidateSummary(CandidateMaturityBand maturityBand)
        => new(
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
            "Highland kin sightings",
            "None",
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
                ["thin_local_support"],
                string.Empty,
                "Meets the hard truth floor and normal-entry durability gates."),
            maturityBand,
            "rooted coherence",
            "Anchored mixed hunter-forager in river valley",
            "Solid anchored start with clear internal shape.",
            "River Hearth is an anchored mixed hunter-forager start on river valley ground, with stable support and deep continuity.",
            ["Reliable current support"],
            ["Thin durability for a normal stop"],
            ["Recent shock may still cascade"],
            new CandidateScoreBreakdown(0.71, 0.72, 0.69, 0.64, 0.41, 0.56, 0.18, 0.61, CandidateScoreTier.Strong, "Runs on continuity depth, with no major drag beyond external entanglement."),
            ["maturity:anchored", "home:river_valley"],
            "dup-key");

    private static PeopleMonthlySnapshot CreateMonthlySnapshot(int polityId)
        => new(
            polityId,
            "River Hearth",
            1,
            77,
            920,
            6,
            (920 * 12) + 6,
            180,
            0,
            0,
            [0, 1],
            0,
            2,
            1,
            1,
            0,
            0,
            0.74,
            0.82,
            0.33,
            0.04,
            2,
            48,
            40,
            44,
            0.92,
            0.95,
            0.00,
            0.10,
            144,
            120,
            1,
            0,
            1,
            0.18,
            0.12,
            SettlementStatus.SemiSettled,
            PolityStage.Tribe,
            HasManagedFood: false,
            HasAgriculture: false,
            HasFoodStorage: true,
            HasSeasonalPlanning: true,
            IsAnchoredThisMonth: true,
            IsStrongAnchoredThisMonth: false,
            ExpansionOpportunityThisMonth: true,
            TradeContactThisMonth: true,
            MovedThisMonth: false,
            SupportCrashThisMonth: false,
            DisplacementThisMonth: false,
            SettlementLossThisMonth: false,
            CollapseMarkerThisMonth: false,
            IdentityBreakThisMonth: false,
            ActiveIdentityBreakNow: false,
            ContinuousIdentityMonthsObserved: 24,
            RelevantNeighborCount: 2,
            AdjacentNeighborCount: 1,
            ReachableNeighborCount: 1,
            PressureNeighborCount: 0);

    private static PrehistoryObserverSnapshot CreateObserverSnapshot(int polityId, bool polityGatePasses)
    {
        PeopleHistoryWindowSnapshot history = new(
            new PeopleSnapshotHeader(polityId, "River Hearth", 1, 77, 920, 6),
            new SnapshotWindowAvailability(12, 24, true, 3, 6, 12, 12),
            new CurrentPeopleState(180, 0, 2, 0.92, 0.95, 0.18, 0.12, 0.82, 0.33, 0.04, 0.74, true, false, false, false, false, false, false, true, true, 0, 2, 0),
            new DemographyHistoryRollup(180, 166, 154, 148, 4, 1, 132),
            new SupportHistoryRollup(0.92, 0.90, 0.88, 0.91, 0, 1, 2, 2, 0, 0, 0),
            new SpatialHistoryRollup(2, 2.0, 0.82, 0.79, 0.33, 0.28, 0.24, 0.04, 1, 2),
            new RootednessHistoryRollup(6, 12, 18, polityGatePasses ? 9 : 3, 0.74, 12, 0, 0),
            new SocialContinuityHistoryRollup(24, 24, 0, 0, 0, false),
            new SettlementHistoryRollup(2, 6, 12, 18, 12, 18, 0, 0, 1, 132),
            new PoliticalHistoryRollup(
                polityGatePasses ? PolityStage.Tribe : PolityStage.Band,
                polityGatePasses ? SettlementStatus.Settled : SettlementStatus.SemiSettled,
                polityGatePasses ? 10 : 4,
                polityGatePasses ? 6 : 1,
                12,
                12,
                polityGatePasses ? 6 : 2),
            new ActionableSignalHistoryRollup(1, 2, 0, 1, 3, 6, 0, 0),
            new HistoryShockMarkers(false, 0, 0, 0, false, 0, 0, 0, false, 0, 0, 0, false, 0, 0, 0, false, 0, 0, 0),
            new EvaluatorHealthSummary(
                new DemographicHealthSummary(180, 166, 154, 1, 132, 0),
                new SupportStabilityHealth(SupportStabilityState.Stable, 0.92, 0.90, 0.88, 0.91, 0, 1, false, 0, 0, false),
                new MovementCoherenceHealth(MovementCoherenceState.Coherent, 0.82, 0.33, 0.04, 1.08, 0.28, 0.24, 6, 10, 0, 0),
                new RootednessHealth(polityGatePasses ? RootednessState.DeeplyRooted : RootednessState.Rooted, 12, polityGatePasses ? 9 : 3, 0.74, 12, false, 0, 0, false),
                new ContinuityHealth(ContinuityState.Deep, 24, 24, 0, 0, 0, false)));

        RegionEvaluationSnapshot[] regionEvaluations =
        [
            CreateRegionEvaluation(polityId, 0, "Green Basin", PeopleRegionRelationshipType.HomeCore, true, true, false, 12, 0.58, 0.94, 0.72, 0.18, 0.10, 2, 4),
            CreateRegionEvaluation(polityId, 1, "Stone Shelf", PeopleRegionRelationshipType.Occupied, false, true, false, 8, 0.32, 0.82, 0.61, 0.34, 0.18, 1, 2),
            CreateRegionEvaluation(polityId, 2, "River Road", PeopleRegionRelationshipType.SeasonalRoute, false, false, false, 6, 0.10, 0.68, 0.55, 0.44, 0.16, 3, 1),
            CreateRegionEvaluation(polityId, 3, "Frontier Verge", PeopleRegionRelationshipType.AdjacentCandidate, false, false, false, 0, 0.00, 0.63, 0.58, 0.62, 0.22, 0, 0)
        ];

        NeighborContextSnapshot neighbors = new(
            polityId,
            920,
            6,
            new NeighborhoodSummary(1, 1, 1, 0, 1, 0),
            [
                new NeighborRelationshipSnapshot(polityId, 22, "Stone Chorus", 2, 88, 1, 1, true, true, false, true, false, false, 1, 2, 0.84, 0.22, ["exchange_context"])
            ],
            new NeighborAggregateMetrics(140, 0, 1, 1.0, 0.22));

        return new PrehistoryObserverSnapshot(
            920,
            6,
            [history],
            regionEvaluations,
            [neighbors],
            "snapshot");
    }

    private static RegionEvaluationSnapshot CreateRegionEvaluation(
        int polityId,
        int regionId,
        string regionName,
        PeopleRegionRelationshipType relationshipType,
        bool isCurrentCenter,
        bool isOccupied,
        bool isFormerHome,
        int presenceMonthsObserved,
        double supportContributionShare,
        double supportAdequacy,
        double subsistenceCompatibility,
        double frontierInterpretation,
        double relativeCompetitionPressure,
        int contactCount,
        int historicalSignificanceCount)
        => new(
            polityId,
            920,
            6,
            new RegionGlobalEvaluation(regionId, regionName, RegionBiome.Plains.ToString(), 0.72, 0.68, 0.80, 0.55, 0.78, 2, 1, 1, 0.12, 0.08),
            new PeopleRegionEvaluation(
                polityId,
                relationshipType,
                isCurrentCenter,
                isOccupied,
                isFormerHome,
                presenceMonthsObserved,
                supportContributionShare,
                supportAdequacy,
                subsistenceCompatibility,
                frontierInterpretation,
                relativeCompetitionPressure,
                contactCount,
                historicalSignificanceCount));

    private static Region CreateRegion(int id, string name)
        => new(id, name)
        {
            Biome = RegionBiome.Plains,
            Fertility = 0.72,
            WaterAvailability = 0.68,
            PlantBiomass = 90,
            MaxPlantBiomass = 100,
            AnimalBiomass = 42,
            MaxAnimalBiomass = 50
        };
}
