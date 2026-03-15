using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using LivingWorld.Generation;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;
using Xunit;

namespace LivingWorld.Tests;

public sealed class PrehistoryObserverSnapshotTests
{
    [Fact]
    public void PeopleMonthlySnapshot_UsesCurrentMonthTradeAndMovementFactsOnly()
    {
        World world = CreateWorld(3, (1, 2));
        Polity focal = AddPolity(world, 1, "River Walkers", speciesId: 1, regionId: 1, population: 120, lineageId: 10, createHomeSettlement: false);
        Polity tradePartner = AddPolity(world, 2, "Stone Neighbors", speciesId: 1, regionId: 2, population: 110, lineageId: 11, createHomeSettlement: false);
        focal.PreviousRegionId = 0;
        focal.MovedThisYear = true;
        focal.TradePartnerCountThisYear = 4;

        PrehistoryObserverService service = new();
        service.CaptureCurrentMonth(world);
        PeopleMonthlySnapshot initial = Assert.Single(world.PrehistoryObserver.GetPeopleHistory(focal.Id));

        Assert.False(initial.MovedThisMonth);
        Assert.False(initial.TradeContactThisMonth);
        Assert.Equal(0, initial.TradePartnerCount);

        focal.ResetMonthlyObserverFacts();
        tradePartner.ResetMonthlyObserverFacts();
        focal.MovedThisMonth = true;
        focal.TradePartnersThisMonth.Add(tradePartner.Id);
        tradePartner.TradePartnersThisMonth.Add(focal.Id);
        service.CaptureCurrentMonth(world);

        PeopleMonthlySnapshot updated = Assert.Single(world.PrehistoryObserver.GetPeopleHistory(focal.Id));
        Assert.True(updated.MovedThisMonth);
        Assert.True(updated.TradeContactThisMonth);
        Assert.Equal(1, updated.TradePartnerCount);
    }

    [Fact]
    public void PeopleMonthlySnapshot_DoesNotMarkNomadicGroupsAsStableSettled()
    {
        World world = CreateWorld(2, (0, 1));
        Polity polity = AddPolity(world, 1, "Wind Camps", speciesId: 1, regionId: 0, population: 90, lineageId: 10, createHomeSettlement: false);

        PrehistoryObserverService service = new();
        service.CaptureCurrentMonth(world);
        PeopleMonthlySnapshot snapshot = Assert.Single(world.PrehistoryObserver.GetPeopleHistory(polity.Id));

        Assert.Equal(0, snapshot.SettlementCount);
        Assert.Equal(0, snapshot.StableSettlementCount);
        Assert.Equal(0, snapshot.OldestSettlementAgeMonths);
        Assert.False(snapshot.IsAnchoredThisMonth);
        Assert.False(snapshot.IsStrongAnchoredThisMonth);
    }

    [Fact]
    public void PeopleHistoryWindowSnapshot_RollsWindowsAndReportsPartialCoverageHonestly()
    {
        List<PeopleMonthlySnapshot> history = Enumerable.Range(0, 5)
            .Select(index => CreateSnapshot(
                absoluteMonthIndex: index,
                population: 100 + (index * 10),
                supportAdequacy: 0.80 + (index * 0.05),
                continuousIdentityMonthsObserved: index + 1))
            .ToList();

        PeopleHistoryWindowSnapshot snapshot = new PeopleHistoryWindowSnapshotBuilder().Build(history);

        Assert.Equal(5, snapshot.WindowAvailability.AvailableMonthlySnapshots);
        Assert.Equal(5, snapshot.WindowAvailability.AvailableInLast6Months);
        Assert.Equal(5, snapshot.WindowAvailability.AvailableInLast12Months);
        Assert.Equal(5, snapshot.WindowAvailability.AvailableInLast24Months);
        Assert.Equal(120.0, snapshot.DemographyHistoryRollup.AveragePopulationLast6Months, 3);
        Assert.Equal(120.0, snapshot.DemographyHistoryRollup.AveragePopulationLast12Months, 3);
        Assert.Equal(4, snapshot.DemographyHistoryRollup.GrowthMonthsLast6Months);
    }

    [Fact]
    public void PeopleHistoryWindowSnapshot_ExposesCurrentShocksAndAgesShockMarkersThroughWindows()
    {
        List<PeopleMonthlySnapshot> history =
        [
            CreateSnapshot(0, supportCrash: true, displacement: true, settlementLoss: true, collapse: true, identityBreak: true, activeIdentityBreak: true, continuousIdentityMonthsObserved: 0),
            CreateSnapshot(1, continuousIdentityMonthsObserved: 1),
            CreateSnapshot(2, continuousIdentityMonthsObserved: 2),
            CreateSnapshot(3, supportCrash: true, displacement: true, settlementLoss: true, collapse: true, identityBreak: true, activeIdentityBreak: true, continuousIdentityMonthsObserved: 0),
            CreateSnapshot(4, continuousIdentityMonthsObserved: 4),
            CreateSnapshot(5, continuousIdentityMonthsObserved: 5),
            CreateSnapshot(6, supportCrash: false, displacement: false, settlementLoss: false, collapse: false, identityBreak: false, activeIdentityBreak: false, continuousIdentityMonthsObserved: 1)
        ];

        PeopleHistoryWindowSnapshot snapshot = new PeopleHistoryWindowSnapshotBuilder().Build(history);

        Assert.False(snapshot.CurrentPeopleState.HasCurrentSupportCrash);
        Assert.False(snapshot.HistoryShockMarkers.CurrentSupportCrash);
        Assert.Equal(0, snapshot.HistoryShockMarkers.SupportCrashMonthsLast3Months);
        Assert.Equal(1, snapshot.HistoryShockMarkers.SupportCrashMonthsLast6Months);
        Assert.Equal(2, snapshot.HistoryShockMarkers.SupportCrashMonthsLast12Months);
        Assert.Equal(3, snapshot.SocialContinuityHistoryRollup.MonthsSinceIdentityBreak);

        PeopleHistoryWindowSnapshot currentShock = new PeopleHistoryWindowSnapshotBuilder().Build([
            CreateSnapshot(10, supportCrash: false, displacement: false, continuousIdentityMonthsObserved: 4),
            CreateSnapshot(11, supportCrash: true, displacement: true, settlementLoss: true, collapse: true, identityBreak: true, activeIdentityBreak: true, continuousIdentityMonthsObserved: 0)
        ]);

        Assert.True(currentShock.CurrentPeopleState.HasCurrentSupportCrash);
        Assert.True(currentShock.CurrentPeopleState.HasCurrentDisplacement);
        Assert.True(currentShock.HistoryShockMarkers.CurrentSupportCrash);
        Assert.True(currentShock.HistoryShockMarkers.CurrentIdentityBreak);
    }

    [Fact]
    public void EvaluatorHealthSummary_DistinguishesMovementCoherenceStates()
    {
        PeopleHistoryWindowSnapshot coherent = new PeopleHistoryWindowSnapshotBuilder().Build(
            Enumerable.Range(0, 6)
                .Select(index => CreateSnapshot(
                    absoluteMonthIndex: index,
                    connectedFootprintShare: 0.90,
                    routeCoverageShare: 0.85,
                    scatterShare: 0.10,
                    supportAdequacy: 1.05,
                    occupiedRegionIds: [1, 2],
                    currentRegionId: 1,
                    homeClusterRegionId: 1))
                .ToList());

        PeopleHistoryWindowSnapshot scattered = new PeopleHistoryWindowSnapshotBuilder().Build(
            Enumerable.Range(0, 6)
                .Select(index => CreateSnapshot(
                    absoluteMonthIndex: index,
                    connectedFootprintShare: 0.35,
                    routeCoverageShare: 0.25,
                    scatterShare: 0.65,
                    supportAdequacy: 0.75,
                    occupiedRegionIds: [1, 3, 5],
                    currentRegionId: 1,
                    homeClusterRegionId: 1,
                    maxFootprintHopDistance: 4))
                .ToList());

        Assert.Equal(MovementCoherenceState.Coherent, coherent.EvaluatorHealthSummary.MovementCoherence.State);
        Assert.Equal(6, coherent.EvaluatorHealthSummary.MovementCoherence.CoherentMonthsLast6Months);
        Assert.Equal(MovementCoherenceState.Scattered, scattered.EvaluatorHealthSummary.MovementCoherence.State);
        Assert.Equal(6, scattered.EvaluatorHealthSummary.MovementCoherence.ScatteredMonthsLast6Months);
    }

    [Fact]
    public void EvaluatorHealthSummary_DistinguishesRootednessAndContinuityStates()
    {
        PeopleHistoryWindowSnapshot anchored = new PeopleHistoryWindowSnapshotBuilder().Build(
            Enumerable.Range(0, 12)
                .Select(index => CreateSnapshot(
                    absoluteMonthIndex: index,
                    settlementCount: 2,
                    stableSettlementCount: 2,
                    oldestSettlementAgeMonths: 18,
                    averageSettlementAgeMonths: 12,
                    homeClusterShare: 0.85,
                    connectedFootprintShare: 0.90,
                    routeCoverageShare: 0.85,
                    scatterShare: 0.10,
                    isAnchored: true,
                    isStrongAnchored: true,
                    continuousIdentityMonthsObserved: index + 1,
                    occupiedRegionIds: [1, 2],
                    currentRegionId: 1,
                    homeClusterRegionId: 1))
                .ToList());

        PeopleHistoryWindowSnapshot displaced = new PeopleHistoryWindowSnapshotBuilder().Build(
            Enumerable.Range(0, 6)
                .Select(index => CreateSnapshot(
                    absoluteMonthIndex: index,
                    settlementCount: 1,
                    stableSettlementCount: 0,
                    oldestSettlementAgeMonths: 2,
                    averageSettlementAgeMonths: 2,
                    homeClusterShare: 0.35,
                    connectedFootprintShare: 0.45,
                    routeCoverageShare: 0.30,
                    scatterShare: 0.55,
                    isAnchored: false,
                    isStrongAnchored: false,
                    displacement: index >= 4,
                    continuousIdentityMonthsObserved: 0,
                    occupiedRegionIds: [2, 4],
                    currentRegionId: 4,
                    previousRegionId: 2,
                    homeClusterRegionId: 4))
                .ToList());

        PeopleHistoryWindowSnapshot recovering = new PeopleHistoryWindowSnapshotBuilder().Build([
            CreateSnapshot(0, settlementCount: 1, isAnchored: false, isStrongAnchored: false, displacement: true, oldestSettlementAgeMonths: 3, averageSettlementAgeMonths: 3, homeClusterShare: 0.40, continuousIdentityMonthsObserved: 1),
            CreateSnapshot(1, settlementCount: 1, isAnchored: false, isStrongAnchored: false, displacement: true, oldestSettlementAgeMonths: 4, averageSettlementAgeMonths: 4, homeClusterShare: 0.45, continuousIdentityMonthsObserved: 2),
            CreateSnapshot(2, settlementCount: 1, isAnchored: true, isStrongAnchored: false, displacement: false, oldestSettlementAgeMonths: 8, averageSettlementAgeMonths: 8, homeClusterShare: 0.70, continuousIdentityMonthsObserved: 3),
            CreateSnapshot(3, settlementCount: 1, isAnchored: true, isStrongAnchored: false, displacement: false, oldestSettlementAgeMonths: 9, averageSettlementAgeMonths: 9, homeClusterShare: 0.75, continuousIdentityMonthsObserved: 4)
        ]);

        PeopleHistoryWindowSnapshot continuityNew = new PeopleHistoryWindowSnapshotBuilder().Build(
            Enumerable.Range(0, 4).Select(index => CreateSnapshot(index, continuousIdentityMonthsObserved: index + 1)).ToList());
        PeopleHistoryWindowSnapshot continuityFragile = new PeopleHistoryWindowSnapshotBuilder().Build([
            CreateSnapshot(0, continuousIdentityMonthsObserved: 4),
            CreateSnapshot(1, continuousIdentityMonthsObserved: 5),
            CreateSnapshot(2, continuousIdentityMonthsObserved: 6),
            CreateSnapshot(3, identityBreak: true, activeIdentityBreak: true, continuousIdentityMonthsObserved: 0),
            CreateSnapshot(4, activeIdentityBreak: false, continuousIdentityMonthsObserved: 5),
            CreateSnapshot(5, activeIdentityBreak: false, continuousIdentityMonthsObserved: 6),
            CreateSnapshot(6, activeIdentityBreak: false, continuousIdentityMonthsObserved: 7),
            CreateSnapshot(7, activeIdentityBreak: false, continuousIdentityMonthsObserved: 8)
        ]);
        PeopleHistoryWindowSnapshot continuityDeep = new PeopleHistoryWindowSnapshotBuilder().Build(
            Enumerable.Range(0, 30).Select(index => CreateSnapshot(index, continuousIdentityMonthsObserved: index + 1)).ToList());

        Assert.Equal(RootednessState.Anchored, anchored.EvaluatorHealthSummary.Rootedness.State);
        Assert.Equal(RootednessState.Displaced, displaced.EvaluatorHealthSummary.Rootedness.State);
        Assert.True(recovering.EvaluatorHealthSummary.Rootedness.RecoveringFromRecentDisplacement);
        Assert.Equal(ContinuityState.New, continuityNew.EvaluatorHealthSummary.Continuity.State);
        Assert.Equal(ContinuityState.Fragile, continuityFragile.EvaluatorHealthSummary.Continuity.State);
        Assert.Equal(ContinuityState.Deep, continuityDeep.EvaluatorHealthSummary.Continuity.State);
    }

    [Fact]
    public void Observe_ClassifiesRegionRelationshipsAcrossCurrentOccupiedFormerAndSeasonalContexts()
    {
        PrehistoryObserverService service = new();

        World structuredWorld = CreateWorld(7, (1, 2), (2, 3), (2, 6), (4, 5));
        Polity rooted = AddPolity(structuredWorld, 1, "Rooted People", speciesId: 1, regionId: 1, population: 160, lineageId: 10, createHomeSettlement: true);
        rooted.PreviousRegionId = 4;
        rooted.AddSettlement(2, "Near Camp");
        rooted.AddSettlement(6, "Far Camp");
        ConfigureSettlement(rooted.Settlements[0], foodRequired: 20, foodProduced: 26, foodStored: 12, establishedMonths: 18);
        ConfigureSettlement(rooted.Settlements[1], foodRequired: 18, foodProduced: 20, foodStored: 8, establishedMonths: 12);
        ConfigureSettlement(rooted.Settlements[2], foodRequired: 15, foodProduced: 17, foodStored: 6, establishedMonths: 9);
        ConfigurePolityFood(rooted, required: 53, produced: 63, stored: 26, shortage: 0, surplus: 10, satisfaction: 1.0);

        PrehistoryObserverSnapshot rootedSnapshot = service.Observe(structuredWorld);
        Dictionary<int, PeopleRegionRelationshipType> rootedRelationships = rootedSnapshot.RegionEvaluations
            .Where(snapshot => snapshot.PeopleId == rooted.Id)
            .ToDictionary(snapshot => snapshot.Global.RegionId, snapshot => snapshot.Relative.RelationshipType);

        Assert.Equal(PeopleRegionRelationshipType.HomeCore, rootedRelationships[1]);
        Assert.Equal(PeopleRegionRelationshipType.HomePeriphery, rootedRelationships[2]);
        Assert.Equal(PeopleRegionRelationshipType.Occupied, rootedRelationships[6]);
        Assert.Equal(PeopleRegionRelationshipType.FormerHome, rootedRelationships[4]);
        Assert.Equal(PeopleRegionRelationshipType.AdjacentCandidate, rootedRelationships[3]);
        Assert.Equal(PeopleRegionRelationshipType.KnownNonOccupied, rootedRelationships[5]);

        World nomadicWorld = CreateWorld(4, (1, 2), (2, 3));
        Polity nomads = AddPolity(nomadicWorld, 9, "Dust Nomads", speciesId: 1, regionId: 2, population: 80, lineageId: 20, createHomeSettlement: false);
        nomads.PreviousRegionId = 3;
        ConfigurePolityFood(nomads, required: 20, produced: 18, stored: 4, shortage: 2, surplus: 0, satisfaction: 0.90);

        PrehistoryObserverSnapshot nomadicSnapshot = service.Observe(nomadicWorld);
        Dictionary<int, PeopleRegionRelationshipType> nomadicRelationships = nomadicSnapshot.RegionEvaluations
            .Where(snapshot => snapshot.PeopleId == nomads.Id)
            .ToDictionary(snapshot => snapshot.Global.RegionId, snapshot => snapshot.Relative.RelationshipType);

        Assert.Equal(PeopleRegionRelationshipType.SeasonalRoute, nomadicRelationships[2]);
        Assert.Equal(PeopleRegionRelationshipType.SeasonalRoute, nomadicRelationships[3]);
    }

    [Fact]
    public void Observe_FiltersNeighborNoiseAndRequiresPairSpecificExchangeEvidence()
    {
        World world = CreateWorld(7, (1, 2), (2, 3), (3, 4), (4, 5), (1, 6));
        Polity focal = AddPolity(world, 1, "Focal", speciesId: 1, regionId: 1, population: 100, lineageId: 10, createHomeSettlement: true);
        ConfigureSettlement(focal.Settlements[0], foodRequired: 20, foodProduced: 12, foodStored: 1, establishedMonths: 12);
        ConfigurePolityFood(focal, required: 20, produced: 12, stored: 1, shortage: 7, surplus: 0, satisfaction: 0.60);

        Polity opportunity = AddPolity(world, 2, "Granary", speciesId: 1, regionId: 2, population: 120, lineageId: 11, createHomeSettlement: true);
        ConfigureSettlement(opportunity.Settlements[0], foodRequired: 20, foodProduced: 32, foodStored: 10, establishedMonths: 14);
        ConfigurePolityFood(opportunity, required: 20, produced: 32, stored: 10, shortage: 0, surplus: 22, satisfaction: 1.0);

        Polity monthlyTrade = AddPolity(world, 3, "Bridge", speciesId: 1, regionId: 3, population: 95, lineageId: 12, createHomeSettlement: true);
        ConfigureSettlement(monthlyTrade.Settlements[0], foodRequired: 18, foodProduced: 18, foodStored: 4, establishedMonths: 14);
        ConfigurePolityFood(monthlyTrade, required: 18, produced: 18, stored: 4, shortage: 0, surplus: 4, satisfaction: 1.0);
        focal.TradePartnersThisMonth.Add(monthlyTrade.Id);
        monthlyTrade.TradePartnersThisMonth.Add(focal.Id);

        Polity yearlyTradeNoise = AddPolity(world, 4, "Old Ledger", speciesId: 1, regionId: 4, population: 90, lineageId: 13, createHomeSettlement: true);
        ConfigureSettlement(yearlyTradeNoise.Settlements[0], foodRequired: 18, foodProduced: 18, foodStored: 3, establishedMonths: 10);
        ConfigurePolityFood(yearlyTradeNoise, required: 18, produced: 18, stored: 3, shortage: 0, surplus: 3, satisfaction: 1.0);
        focal.TradePartnerCountThisYear = 6;
        yearlyTradeNoise.TradePartnerCountThisYear = 4;

        Polity distantLineageNoise = AddPolity(world, 5, "Far Kin", speciesId: 1, regionId: 5, population: 88, lineageId: focal.LineageId, createHomeSettlement: true);
        ConfigureSettlement(distantLineageNoise.Settlements[0], foodRequired: 18, foodProduced: 18, foodStored: 3, establishedMonths: 10);
        ConfigurePolityFood(distantLineageNoise, required: 18, produced: 18, stored: 3, shortage: 0, surplus: 3, satisfaction: 1.0);

        Polity adjacentNoOpportunity = AddPolity(world, 6, "Quiet Border", speciesId: 1, regionId: 6, population: 85, lineageId: 14, createHomeSettlement: true);
        ConfigureSettlement(adjacentNoOpportunity.Settlements[0], foodRequired: 16, foodProduced: 16, foodStored: 0, establishedMonths: 10);
        ConfigurePolityFood(adjacentNoOpportunity, required: 16, produced: 16, stored: 0, shortage: 0, surplus: 0, satisfaction: 1.0);

        NeighborContextSnapshot neighbors = Assert.Single(new PrehistoryObserverService().Observe(world).NeighborContexts, snapshot => snapshot.PeopleId == focal.Id);
        Dictionary<int, NeighborRelationshipSnapshot> relationships = neighbors.NeighborRelationships.ToDictionary(relationship => relationship.NeighborPeopleId);

        Assert.True(relationships[2].OffersExchangeContext);
        Assert.Contains("exchange_opportunity", relationships[2].RelevanceReasons);
        Assert.True(relationships[3].OffersExchangeContext);
        Assert.Contains("monthly_trade_contact", relationships[3].RelevanceReasons);
        Assert.False(relationships[6].OffersExchangeContext);
        Assert.DoesNotContain(4, relationships.Keys);
        Assert.DoesNotContain(5, relationships.Keys);
    }

    [Fact]
    public void ObserverSnapshot_ArtifactsRemainDescriptiveWithoutEvaluatorConclusions()
    {
        PrehistoryObserverSnapshot snapshot = new PrehistoryObserverService().Observe(CreateObservedWorldForDescriptionChecks());
        string[] bannedTerms = ["viable", "score", "best", "recommend", "qualification", "risk_tier"];

        foreach (string banned in bannedTerms)
        {
            Assert.DoesNotContain(banned, snapshot.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(snapshot.Notes, text => text.Contains(banned, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(snapshot.NeighborContexts.SelectMany(context => context.NeighborRelationships).SelectMany(relationship => relationship.RelevanceReasons), reason => reason.Contains(banned, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void ObserverSnapshot_IsDeterministicForSameSeedAndState()
    {
        PrehistoryObserverService service = new();
        World firstWorld = new WorldGenerator(seed: 43).Generate();
        World secondWorld = new WorldGenerator(seed: 43).Generate();

        PrehistoryObserverSnapshot first = service.Observe(firstWorld);
        PrehistoryObserverSnapshot second = service.Observe(secondWorld);

        Assert.Equal(first.WorldYear, second.WorldYear);
        Assert.Equal(first.WorldMonth, second.WorldMonth);
        Assert.Equal(first.Summary, second.Summary);
        Assert.Equal(first.Notes, second.Notes);
        Assert.Equal(first.PeopleHistoryWindows, second.PeopleHistoryWindows);
        Assert.Equal(first.RegionEvaluations, second.RegionEvaluations);
        Assert.Equal(first.NeighborContexts.Select(DescribeNeighborContext), second.NeighborContexts.Select(DescribeNeighborContext));
    }

    private static World CreateObservedWorldForDescriptionChecks()
    {
        World world = CreateWorld(4, (1, 2), (2, 3));
        Polity focal = AddPolity(world, 1, "River Hearth", speciesId: 1, regionId: 1, population: 140, lineageId: 10, createHomeSettlement: true);
        Polity neighbor = AddPolity(world, 2, "Stone Plain", speciesId: 1, regionId: 2, population: 110, lineageId: 11, createHomeSettlement: true);
        ConfigureSettlement(focal.Settlements[0], foodRequired: 20, foodProduced: 28, foodStored: 8, establishedMonths: 18);
        ConfigureSettlement(neighbor.Settlements[0], foodRequired: 20, foodProduced: 14, foodStored: 2, establishedMonths: 10);
        ConfigurePolityFood(focal, required: 20, produced: 28, stored: 8, shortage: 0, surplus: 16, satisfaction: 1.0);
        ConfigurePolityFood(neighbor, required: 20, produced: 14, stored: 2, shortage: 4, surplus: 0, satisfaction: 0.80);
        return world;
    }

    private static World CreateWorld(int regionCount, params (int Left, int Right)[] connections)
    {
        World world = new(new WorldTime(12, 6));
        for (int regionId = 0; regionId < regionCount; regionId++)
        {
            world.Regions.Add(new Region(regionId, $"Region {regionId}")
            {
                Fertility = 0.75,
                WaterAvailability = 0.70,
                PlantBiomass = 80,
                AnimalBiomass = 60,
                MaxPlantBiomass = 100,
                MaxAnimalBiomass = 100
            });
        }

        foreach ((int left, int right) in connections)
        {
            world.Regions[left].AddConnection(right);
            world.Regions[right].AddConnection(left);
        }

        world.Species.Add(new Species(1, "Humans", 0.8, 0.7) { IsSapient = true });
        return world;
    }

    private static Polity AddPolity(World world, int id, string name, int speciesId, int regionId, int population, int lineageId, bool createHomeSettlement)
    {
        Polity polity = new(id, name, speciesId, regionId, population, lineageId: lineageId)
        {
            PreviousRegionId = regionId,
            SettlementStatus = createHomeSettlement ? SettlementStatus.SemiSettled : SettlementStatus.Nomadic
        };
        if (createHomeSettlement)
        {
            polity.EstablishFirstSettlement(regionId, $"{name} Hearth");
        }
        else
        {
            polity.ClearSettlementState();
        }

        world.Polities.Add(polity);
        foreach (Region region in world.Regions)
        {
            region.GetOrCreateSpeciesPopulation(speciesId).PopulationCount = 300;
        }

        return polity;
    }

    private static void ConfigurePolityFood(Polity polity, double required, double produced, double stored, double shortage, double surplus, double satisfaction)
    {
        polity.FoodNeededThisMonth = required;
        polity.FoodGatheredThisMonth = produced;
        polity.FoodFarmedThisMonth = 0;
        polity.FoodManagedThisMonth = 0;
        polity.FoodStores = stored;
        polity.FoodShortageThisMonth = shortage;
        polity.FoodSurplusThisMonth = surplus;
        polity.FoodSatisfactionThisMonth = satisfaction;
    }

    private static void ConfigureSettlement(Settlement settlement, double foodRequired, double foodProduced, double foodStored, int establishedMonths)
    {
        settlement.FoodRequired = foodRequired;
        settlement.FoodProduced = foodProduced;
        settlement.FoodStored = foodStored;
        settlement.EstablishedMonths = establishedMonths;
        settlement.CalculateFoodState();
    }

    private static PeopleMonthlySnapshot CreateSnapshot(
        int absoluteMonthIndex,
        int population = 100,
        int currentRegionId = 1,
        int previousRegionId = 1,
        int settlementCount = 1,
        int surplusSettlementCount = 0,
        int stableSettlementCount = 1,
        int deficitSettlementCount = 0,
        int starvingSettlementCount = 0,
        double homeClusterShare = 1.0,
        double connectedFootprintShare = 1.0,
        double routeCoverageShare = 1.0,
        double scatterShare = 0.0,
        int maxFootprintHopDistance = 0,
        double foodStores = 10.0,
        double foodRequired = 8.0,
        double foodProduced = 9.0,
        double supportAdequacy = 1.0,
        double foodSatisfaction = 1.0,
        double foodShortageShare = 0.0,
        double foodSurplusShare = 0.1,
        int oldestSettlementAgeMonths = 12,
        double averageSettlementAgeMonths = 12.0,
        int discoveryCount = 2,
        int advancementCount = 1,
        int tradePartnerCount = 0,
        double migrationPressure = 0.2,
        double fragmentationPressure = 0.2,
        SettlementStatus settlementStatus = SettlementStatus.SemiSettled,
        PolityStage stage = PolityStage.Band,
        bool hasManagedFood = false,
        bool hasAgriculture = false,
        bool hasFoodStorage = false,
        bool hasSeasonalPlanning = false,
        bool isAnchored = true,
        bool isStrongAnchored = false,
        bool expansionOpportunity = false,
        bool tradeContact = false,
        bool moved = false,
        bool supportCrash = false,
        bool displacement = false,
        bool settlementLoss = false,
        bool collapse = false,
        bool identityBreak = false,
        bool activeIdentityBreak = false,
        int continuousIdentityMonthsObserved = 12,
        int relevantNeighborCount = 0,
        int adjacentNeighborCount = 0,
        int reachableNeighborCount = 0,
        int pressureNeighborCount = 0,
        IReadOnlyList<int>? occupiedRegionIds = null,
        int homeClusterRegionId = 1)
    {
        int worldYear = absoluteMonthIndex / 12;
        int worldMonth = (absoluteMonthIndex % 12) + 1;
        IReadOnlyList<int> occupied = occupiedRegionIds ?? [currentRegionId];
        return new PeopleMonthlySnapshot(
            1,
            "People",
            1,
            1,
            worldYear,
            worldMonth,
            absoluteMonthIndex,
            population,
            currentRegionId,
            previousRegionId,
            occupied,
            homeClusterRegionId,
            settlementCount,
            surplusSettlementCount,
            stableSettlementCount,
            deficitSettlementCount,
            starvingSettlementCount,
            homeClusterShare,
            connectedFootprintShare,
            routeCoverageShare,
            scatterShare,
            maxFootprintHopDistance,
            foodStores,
            foodRequired,
            foodProduced,
            supportAdequacy,
            foodSatisfaction,
            foodShortageShare,
            foodSurplusShare,
            oldestSettlementAgeMonths,
            averageSettlementAgeMonths,
            discoveryCount,
            advancementCount,
            tradePartnerCount,
            migrationPressure,
            fragmentationPressure,
            settlementStatus,
            stage,
            hasManagedFood,
            hasAgriculture,
            hasFoodStorage,
            hasSeasonalPlanning,
            isAnchored,
            isStrongAnchored,
            expansionOpportunity,
            tradeContact,
            moved,
            supportCrash,
            displacement,
            settlementLoss,
            collapse,
            identityBreak,
            activeIdentityBreak,
            continuousIdentityMonthsObserved,
            relevantNeighborCount,
            adjacentNeighborCount,
            reachableNeighborCount,
            pressureNeighborCount);
    }

    private static string DescribeNeighborContext(NeighborContextSnapshot snapshot)
        => string.Join(
            "|",
            snapshot.PeopleId,
            snapshot.WorldYear,
            snapshot.WorldMonth,
            snapshot.NeighborhoodSummary.RelevantNeighborCount,
            snapshot.NeighborhoodSummary.ExchangeContextNeighborCount,
            snapshot.NeighborAggregateMetrics.TotalNeighborPopulation,
            string.Join(
                ";",
                snapshot.NeighborRelationships.Select(relationship => string.Join(
                    ",",
                    relationship.NeighborPeopleId,
                    relationship.HopDistance,
                    relationship.SharesBorder,
                    relationship.IsReachable,
                    relationship.ExertsPressure,
                    relationship.OffersExchangeContext,
                    relationship.HasFormerSharedSpace,
                    relationship.SharesLineage,
                    relationship.ContactCount,
                    relationship.RelativePressure.ToString("F4"),
                    string.Join("/", relationship.RelevanceReasons)))));
}
