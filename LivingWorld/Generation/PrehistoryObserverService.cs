using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Generation;

public sealed class PrehistoryObserverService
{
    private readonly PeopleMonthlySnapshotExtractor _monthlyExtractor = new();
    private readonly PeopleHistoryWindowSnapshotBuilder _historyBuilder = new();
    private readonly RegionEvaluationSnapshotBuilder _regionBuilder = new();
    private readonly NeighborContextSnapshotBuilder _neighborBuilder = new();

    public void CaptureCurrentMonth(World world)
    {
        ObserverWorldContext context = new(world);
        int absoluteMonthIndex = ObserverMath.ToAbsoluteMonthIndex(world.Time.Year, world.Time.Month);
        foreach (Polity polity in world.Polities
                     .Where(candidate => candidate.Population > 0)
                     .OrderBy(candidate => candidate.Id))
        {
            PeopleMonthlySnapshot? previous = world.Prehistory.Observer.GetLatestBeforeMonth(polity.Id, absoluteMonthIndex);
            PeopleMonthlySnapshot snapshot = _monthlyExtractor.Create(context, polity, previous);
            world.Prehistory.Observer.Upsert(snapshot);
        }
    }

    public PrehistoryObserverSnapshot Observe(World world)
    {
        ObserverWorldContext context = new(world);
        List<PeopleHistoryWindowSnapshot> peopleHistoryWindows = [];
        List<RegionEvaluationSnapshot> regionEvaluations = [];
        List<NeighborContextSnapshot> neighborContexts = [];
        Dictionary<int, IReadOnlyList<PeopleMonthlySnapshot>> rawPeopleHistoryById = new();

        foreach (Polity polity in world.Polities
                     .Where(candidate => candidate.Population > 0)
                     .OrderBy(candidate => candidate.Id))
        {
            IReadOnlyList<PeopleMonthlySnapshot> history = GetObservedHistory(context, polity);
            if (history.Count == 0)
            {
                continue;
            }

            rawPeopleHistoryById[polity.Id] = history;
            PeopleHistoryWindowSnapshot peopleHistory = _historyBuilder.Build(history);
            peopleHistoryWindows.Add(peopleHistory);
            regionEvaluations.AddRange(_regionBuilder.Build(context, polity, peopleHistory, history));
            neighborContexts.Add(_neighborBuilder.Build(context, polity));
        }

        return new PrehistoryObserverSnapshot(
            context.World.Time.Year,
            context.World.Time.Month,
            peopleHistoryWindows,
            regionEvaluations,
            neighborContexts,
            $"Observer snapshot for {peopleHistoryWindows.Count} people at year {context.World.Time.Year}, month {context.World.Time.Month}.",
            [$"region_contexts:{regionEvaluations.Count}", $"neighbor_contexts:{neighborContexts.Count}"],
            rawPeopleHistoryById);
    }

    private List<PeopleMonthlySnapshot> GetObservedHistory(ObserverWorldContext context, Polity polity)
    {
        int absoluteMonthIndex = ObserverMath.ToAbsoluteMonthIndex(context.World.Time.Year, context.World.Time.Month);
        List<PeopleMonthlySnapshot> history = context.World.Prehistory.Observer.GetPeopleHistory(polity.Id).ToList();
        if (history.Any(snapshot => snapshot.AbsoluteMonthIndex == absoluteMonthIndex))
        {
            return history;
        }

        PeopleMonthlySnapshot? previous = context.World.Prehistory.Observer.GetLatestBeforeMonth(polity.Id, absoluteMonthIndex);
        history.Add(_monthlyExtractor.Create(context, polity, previous));
        history.Sort(static (left, right) => left.AbsoluteMonthIndex.CompareTo(right.AbsoluteMonthIndex));
        return history;
    }
}

internal sealed class ObserverWorldContext
{
    private readonly Dictionary<int, Region> _regionsById;
    private readonly Dictionary<int, Species> _speciesById;
    private readonly List<Polity> _activePolities;
    private readonly Dictionary<int, int> _activePopulationByRegionId = [];
    private readonly Dictionary<int, int> _settlementCountByRegionId = [];
    private readonly Dictionary<int, HashSet<int>> _occupyingPolityIdsByRegionId = [];
    private readonly Dictionary<(int PolityId, int RegionId), int> _historicalSignificanceCountByPolityRegion = [];
    private readonly Dictionary<int, int> _recentExtinctionCountByRegionId = [];
    private readonly Dictionary<(int LeftRegionId, int RightRegionId), int> _hopDistanceCache = [];

    public ObserverWorldContext(World world)
    {
        World = world;
        _regionsById = world.Regions.ToDictionary(region => region.Id);
        _speciesById = world.Species.ToDictionary(species => species.Id);
        _activePolities = world.Polities
            .Where(polity => polity.Population > 0)
            .OrderBy(polity => polity.Id)
            .ToList();

        foreach (Polity polity in _activePolities)
        {
            foreach (int regionId in GetOccupiedRegionIds(polity))
            {
                if (!_occupyingPolityIdsByRegionId.TryGetValue(regionId, out HashSet<int>? polityIds))
                {
                    polityIds = [];
                    _occupyingPolityIdsByRegionId[regionId] = polityIds;
                }

                polityIds.Add(polity.Id);
            }

            if (_regionsById.ContainsKey(polity.RegionId))
            {
                _activePopulationByRegionId[polity.RegionId] = GetActivePopulationInRegion(polity.RegionId) + polity.Population;
            }

            foreach (Settlement settlement in polity.Settlements)
            {
                if (_regionsById.ContainsKey(settlement.RegionId))
                {
                    _settlementCountByRegionId[settlement.RegionId] = GetSettlementCountInRegion(settlement.RegionId) + 1;
                }
            }
        }

        foreach (CivilizationalHistoryEvent entry in world.CivilizationalHistory)
        {
            if (!entry.PolityId.HasValue || !_regionsById.ContainsKey(entry.RegionId))
            {
                continue;
            }

            (int PolityId, int RegionId) key = (entry.PolityId.Value, entry.RegionId);
            _historicalSignificanceCountByPolityRegion[key] = GetHistoricalSignificanceCount(entry.PolityId.Value, entry.RegionId) + 1;
        }

        foreach (LocalPopulationExtinctionRecord extinction in world.LocalPopulationExtinctions)
        {
            if (ObserverMath.MonthsBetween(extinction.Year, extinction.Month, world.Time.Year, world.Time.Month) >= 12)
            {
                continue;
            }

            _recentExtinctionCountByRegionId[extinction.RegionId] = GetRecentExtinctionCount(extinction.RegionId) + 1;
        }
    }

    public World World { get; }

    public IReadOnlyList<Polity> ActivePolities => _activePolities;

    public Region GetRequiredRegion(int regionId)
        => _regionsById.TryGetValue(regionId, out Region? region)
            ? region
            : throw new InvalidOperationException($"Observer snapshot referenced missing region {regionId}.");

    public bool TryGetRegion(int regionId, out Region? region)
        => _regionsById.TryGetValue(regionId, out region);

    public Species GetRequiredSpecies(int speciesId)
        => _speciesById.TryGetValue(speciesId, out Species? species)
            ? species
            : throw new InvalidOperationException($"Observer snapshot referenced missing species {speciesId}.");

    public List<int> GetOccupiedRegionIds(Polity polity)
    {
        List<int> occupiedRegionIds = polity.Settlements
            .Select(settlement => settlement.RegionId)
            .Distinct()
            .OrderBy(regionId => regionId)
            .ToList();
        if (occupiedRegionIds.Count == 0)
        {
            occupiedRegionIds.Add(polity.RegionId);
        }

        return occupiedRegionIds;
    }

    public int GetActivePopulationInRegion(int regionId)
        => _activePopulationByRegionId.TryGetValue(regionId, out int population)
            ? population
            : 0;

    public int GetSettlementCountInRegion(int regionId)
        => _settlementCountByRegionId.TryGetValue(regionId, out int settlementCount)
            ? settlementCount
            : 0;

    public int GetOccupyingPeopleCountInRegion(int regionId)
        => _occupyingPolityIdsByRegionId.TryGetValue(regionId, out HashSet<int>? polityIds)
            ? polityIds.Count
            : 0;

    public int GetOtherOccupyingPeopleCountInRegion(int regionId, int polityId)
    {
        if (!_occupyingPolityIdsByRegionId.TryGetValue(regionId, out HashSet<int>? polityIds))
        {
            return 0;
        }

        return polityIds.Contains(polityId)
            ? Math.Max(0, polityIds.Count - 1)
            : polityIds.Count;
    }

    public int GetHistoricalSignificanceCount(int polityId, int regionId)
        => _historicalSignificanceCountByPolityRegion.TryGetValue((polityId, regionId), out int count)
            ? count
            : 0;

    public int GetRecentExtinctionCount(int regionId)
        => _recentExtinctionCountByRegionId.TryGetValue(regionId, out int count)
            ? count
            : 0;

    public int GetHopDistance(int sourceRegionId, int targetRegionId)
    {
        (int LeftRegionId, int RightRegionId) key = sourceRegionId <= targetRegionId
            ? (sourceRegionId, targetRegionId)
            : (targetRegionId, sourceRegionId);
        if (_hopDistanceCache.TryGetValue(key, out int cachedDistance))
        {
            return cachedDistance;
        }

        int resolvedDistance = ObserverMath.ComputeHopDistance(this, sourceRegionId, targetRegionId);
        _hopDistanceCache[key] = resolvedDistance;
        return resolvedDistance;
    }
}

internal sealed class PeopleMonthlySnapshotExtractor
{
    public PeopleMonthlySnapshot Create(ObserverWorldContext context, Polity polity, PeopleMonthlySnapshot? previous)
    {
        List<int> occupiedRegionIds = context.GetOccupiedRegionIds(polity);

        int settlementCount = polity.SettlementCount;
        int homeClusterRegionId = occupiedRegionIds[0];
        double homeClusterShare = 1.0;
        int oldestSettlementAgeMonths = 0;
        double averageSettlementAgeMonths = 0.0;
        int surplusSettlements = 0;
        int stableSettlements = 0;
        int deficitSettlements = 0;
        int starvingSettlements = 0;
        double required = polity.FoodNeededThisMonth;
        double produced = polity.FoodGatheredThisMonth + polity.FoodFarmedThisMonth + polity.FoodManagedThisMonth;
        double stored = polity.FoodStores;

        if (settlementCount > 0)
        {
            IGrouping<int, Settlement> dominantCluster = polity.Settlements
                .GroupBy(settlement => settlement.RegionId)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .First();
            homeClusterRegionId = dominantCluster.Key;
            homeClusterShare = dominantCluster.Count() / (double)settlementCount;
            oldestSettlementAgeMonths = polity.Settlements.Max(settlement => settlement.EstablishedMonths);
            averageSettlementAgeMonths = polity.Settlements.Average(settlement => settlement.EstablishedMonths);
            surplusSettlements = polity.Settlements.Count(settlement => settlement.FoodState == FoodState.Surplus);
            stableSettlements = polity.Settlements.Count(settlement => settlement.FoodState == FoodState.Stable);
            deficitSettlements = polity.Settlements.Count(settlement => settlement.FoodState == FoodState.Deficit);
            starvingSettlements = polity.Settlements.Count(settlement => settlement.FoodState == FoodState.Starving);
            required = polity.Settlements.Sum(settlement => settlement.FoodRequired);
            produced = polity.Settlements.Sum(settlement => settlement.FoodProduced + settlement.ManagedAnimalFoodThisMonth + settlement.ManagedCropFoodThisMonth);
            stored = polity.Settlements.Sum(settlement => settlement.FoodStored);
        }

        double supportAdequacy = required <= 0.0
            ? 1.0
            : Math.Clamp((produced + stored) / required, 0.0, 1.5);
        double foodSatisfaction = polity.FoodSatisfactionThisMonth > 0.0
            ? polity.FoodSatisfactionThisMonth
            : Math.Clamp(supportAdequacy, 0.0, 1.0);
        double foodShortageShare = required <= 0.0
            ? 0.0
            : Math.Clamp(polity.FoodShortageThisMonth / required, 0.0, 1.0);
        double foodSurplusShare = required <= 0.0
            ? 0.0
            : Math.Clamp(polity.FoodSurplusThisMonth / required, 0.0, 1.5);
        double connectedFootprintShare = ObserverMath.ComputeLargestConnectedShare(context, occupiedRegionIds);
        double routeCoverageShare = ComputeRouteCoverageShare(context, occupiedRegionIds, homeClusterRegionId);
        double scatterShare = Math.Clamp(1.0 - connectedFootprintShare, 0.0, 1.0);
        int maxFootprintHopDistance = ObserverMath.ComputeMaxPairwiseHopDistance(context, occupiedRegionIds);
        bool movedThisMonth = polity.MovedThisMonth;
        bool supportCrashThisMonth = previous is not null
            && ((previous.SupportAdequacy >= 0.85 && supportAdequacy <= 0.55)
                || (previous.StarvingSettlementCount == 0 && starvingSettlements > 0));
        bool settlementLossThisMonth = previous is not null && previous.SettlementCount > settlementCount;
        bool displacementThisMonth = movedThisMonth
            || (previous is not null
                && previous.HomeClusterRegionId != homeClusterRegionId
                && homeClusterShare < 0.50);
        bool collapseMarkerThisMonth = polity.Population <= 40
            || polity.FragmentationPressure >= 0.85
            || (settlementCount > 0 && starvingSettlements == settlementCount);
        bool identityBreakThisMonth = previous is not null
            && previous.HomeClusterRegionId != homeClusterRegionId
            && (movedThisMonth || settlementLossThisMonth || connectedFootprintShare < 0.50);
        bool activeIdentityBreakNow = identityBreakThisMonth;
        int continuousIdentityMonthsObserved = previous is null
            ? 1
            : identityBreakThisMonth
                ? 0
                : previous.ContinuousIdentityMonthsObserved + Math.Max(1, ObserverMath.ToAbsoluteMonthIndex(context.World.Time.Year, context.World.Time.Month) - previous.AbsoluteMonthIndex);
        bool anchoredThisMonth = settlementCount > 0 && homeClusterShare >= 0.50 && oldestSettlementAgeMonths >= 6 && !movedThisMonth;
        bool strongAnchoredThisMonth = anchoredThisMonth && homeClusterShare >= 0.75 && oldestSettlementAgeMonths >= 12 && starvingSettlements == 0;
        bool expansionOpportunityThisMonth = settlementCount > 0
            && supportAdequacy >= 1.0
            && starvingSettlements == 0
            && polity.MigrationPressure <= 0.35
            && polity.FragmentationPressure <= 0.45;
        bool tradeContactThisMonth = polity.TradePartnersThisMonth.Count > 0;
        List<RelevantNeighborFact> neighbors = ObserverNeighborAnalyzer.GetRelevantNeighbors(context, polity);

        return new PeopleMonthlySnapshot(
            polity.Id,
            polity.Name,
            polity.SpeciesId,
            polity.LineageId,
            context.World.Time.Year,
            context.World.Time.Month,
            ObserverMath.ToAbsoluteMonthIndex(context.World.Time.Year, context.World.Time.Month),
            polity.Population,
            polity.RegionId,
            polity.PreviousRegionId,
            occupiedRegionIds,
            homeClusterRegionId,
            settlementCount,
            surplusSettlements,
            stableSettlements,
            deficitSettlements,
            starvingSettlements,
            homeClusterShare,
            connectedFootprintShare,
            routeCoverageShare,
            scatterShare,
            maxFootprintHopDistance,
            stored,
            required,
            produced,
            supportAdequacy,
            foodSatisfaction,
            foodShortageShare,
            foodSurplusShare,
            oldestSettlementAgeMonths,
            averageSettlementAgeMonths,
            polity.Discoveries.Count,
            polity.Advancements.Count,
            polity.TradePartnersThisMonth.Count,
            polity.MigrationPressure,
            polity.FragmentationPressure,
            polity.SettlementStatus,
            polity.Stage,
            polity.ManagedFoodSupplyEstablished || polity.Settlements.Any(settlement => settlement.ManagedHerds.Count > 0 || settlement.CultivatedCrops.Count > 0),
            polity.HasAdvancement(AdvancementId.Agriculture),
            polity.HasAdvancement(AdvancementId.FoodStorage),
            polity.HasAdvancement(AdvancementId.SeasonalPlanning),
            anchoredThisMonth,
            strongAnchoredThisMonth,
            expansionOpportunityThisMonth,
            tradeContactThisMonth,
            movedThisMonth,
            supportCrashThisMonth,
            displacementThisMonth,
            settlementLossThisMonth,
            collapseMarkerThisMonth,
            identityBreakThisMonth,
            activeIdentityBreakNow,
            continuousIdentityMonthsObserved,
            neighbors.Count,
            neighbors.Count(neighbor => neighbor.SharesBorder),
            neighbors.Count(neighbor => neighbor.IsReachable),
            neighbors.Count(neighbor => neighbor.ExertsPressure));
    }

    private static double ComputeRouteCoverageShare(ObserverWorldContext context, IReadOnlyCollection<int> occupiedRegionIds, int homeClusterRegionId)
    {
        if (occupiedRegionIds.Count <= 1)
        {
            return occupiedRegionIds.Count == 0 ? 0.0 : 1.0;
        }

        int routeCoveredRegions = occupiedRegionIds.Count(regionId => context.GetHopDistance(homeClusterRegionId, regionId) <= 1);
        return Math.Clamp(routeCoveredRegions / (double)occupiedRegionIds.Count, 0.0, 1.0);
    }
}

public sealed class PeopleHistoryWindowSnapshotBuilder
{
    public PeopleHistoryWindowSnapshot Build(IReadOnlyList<PeopleMonthlySnapshot> orderedHistory)
    {
        PeopleMonthlySnapshot current = orderedHistory[^1];
        List<PeopleMonthlySnapshot> last3 = SelectWindow(orderedHistory, current, 3);
        List<PeopleMonthlySnapshot> last6 = SelectWindow(orderedHistory, current, 6);
        List<PeopleMonthlySnapshot> last12 = SelectWindow(orderedHistory, current, 12);
        List<PeopleMonthlySnapshot> last24 = SelectWindow(orderedHistory, current, 24);

        SnapshotWindowAvailability availability = new(
            orderedHistory.Count,
            Math.Max(1, current.AbsoluteMonthIndex - orderedHistory[0].AbsoluteMonthIndex + 1),
            true,
            last3.Count,
            last6.Count,
            last12.Count,
            last24.Count);

        CurrentPeopleState currentPeopleState = new(
            current.Population,
            current.CurrentRegionId,
            current.SettlementCount,
            current.SupportAdequacy,
            current.FoodSatisfaction,
            current.MigrationPressure,
            current.FragmentationPressure,
            current.ConnectedFootprintShare,
            current.RouteCoverageShare,
            current.ScatterShare,
            current.HomeClusterShare,
            current.IsAnchoredThisMonth,
            current.IsStrongAnchoredThisMonth,
            current.SupportCrashThisMonth,
            current.DisplacementThisMonth,
            current.SettlementLossThisMonth,
            current.CollapseMarkerThisMonth,
            current.ActiveIdentityBreakNow,
            current.ExpansionOpportunityThisMonth,
            current.TradeContactThisMonth,
            current.StarvingSettlementCount,
            current.RelevantNeighborCount,
            current.PressureNeighborCount);

        DemographyHistoryRollup demography = new(
            current.Population,
            ObserverMath.Average(last6, snapshot => snapshot.Population),
            ObserverMath.Average(last12, snapshot => snapshot.Population),
            ObserverMath.Average(last24, snapshot => snapshot.Population),
            CountTransitions(last6, static (previous, next) => next.Population > previous.Population),
            CountTransitions(last12, static (previous, next) => next.Population < previous.Population),
            last12.Count == 0 ? current.Population : last12.Min(snapshot => snapshot.Population));

        SupportHistoryRollup support = new(
            current.SupportAdequacy,
            ObserverMath.Average(last6, snapshot => snapshot.SupportAdequacy),
            ObserverMath.Average(last12, snapshot => snapshot.SupportAdequacy),
            ObserverMath.Average(last12, snapshot => snapshot.FoodSatisfaction),
            last6.Count(snapshot => snapshot.FoodShortageShare > 0.05 || snapshot.StarvingSettlementCount > 0),
            last12.Count(snapshot => snapshot.FoodShortageShare > 0.05 || snapshot.StarvingSettlementCount > 0),
            last24.Count(snapshot => snapshot.FoodShortageShare > 0.05 || snapshot.StarvingSettlementCount > 0),
            CountTransitions(last6, static (previous, next) => next.SupportAdequacy > previous.SupportAdequacy && previous.FoodShortageShare > 0.05),
            last3.Count(snapshot => snapshot.SupportCrashThisMonth),
            last6.Count(snapshot => snapshot.SupportCrashThisMonth),
            last12.Count(snapshot => snapshot.SupportCrashThisMonth));

        SpatialHistoryRollup spatial = new(
            current.OccupiedRegionIds.Count,
            ObserverMath.Average(last12, snapshot => snapshot.OccupiedRegionIds.Count),
            current.ConnectedFootprintShare,
            ObserverMath.Average(last12, snapshot => snapshot.ConnectedFootprintShare),
            current.RouteCoverageShare,
            ObserverMath.Average(last6, snapshot => snapshot.RouteCoverageShare),
            ObserverMath.Average(last12, snapshot => snapshot.RouteCoverageShare),
            current.ScatterShare,
            CountTransitions(last12, static (previous, next) => next.CurrentRegionId != previous.CurrentRegionId),
            last24.Count == 0 ? current.MaxFootprintHopDistance : last24.Max(snapshot => snapshot.MaxFootprintHopDistance));

        RootednessHistoryRollup rootedness = new(
            last6.Count(snapshot => snapshot.IsAnchoredThisMonth),
            last12.Count(snapshot => snapshot.IsAnchoredThisMonth),
            last24.Count(snapshot => snapshot.IsAnchoredThisMonth),
            last12.Count(snapshot => snapshot.IsStrongAnchoredThisMonth),
            ObserverMath.Average(last12, snapshot => snapshot.HomeClusterShare),
            last12.Count(snapshot => snapshot.SettlementCount > 0 && snapshot.OldestSettlementAgeMonths >= 6),
            last6.Count(snapshot => snapshot.DisplacementThisMonth),
            last12.Count(snapshot => snapshot.DisplacementThisMonth));

        SocialContinuityHistoryRollup continuity = new(
            current.ContinuousIdentityMonthsObserved,
            MonthsSinceLast(last24, static snapshot => snapshot.IdentityBreakThisMonth, current.AbsoluteMonthIndex),
            last6.Count(snapshot => snapshot.IdentityBreakThisMonth),
            last12.Count(snapshot => snapshot.IdentityBreakThisMonth),
            last24.Count(snapshot => snapshot.IdentityBreakThisMonth),
            current.ActiveIdentityBreakNow);

        SettlementHistoryRollup settlements = new(
            current.SettlementCount,
            last6.Count(snapshot => snapshot.SettlementCount > 0),
            last12.Count(snapshot => snapshot.SettlementCount > 0),
            last24.Count(snapshot => snapshot.SettlementCount > 0),
            last12.Count(snapshot => snapshot.SettlementCount > 0 && snapshot.OldestSettlementAgeMonths >= 6),
            last24.Count(snapshot => snapshot.SettlementCount > 0 && snapshot.OldestSettlementAgeMonths >= 6),
            last6.Count(snapshot => snapshot.SettlementLossThisMonth),
            last12.Count(snapshot => snapshot.SettlementLossThisMonth),
            CountTransitions(last6, static (previous, next) => next.SettlementCount > previous.SettlementCount),
            ObserverMath.Average(last12, snapshot => snapshot.OldestSettlementAgeMonths));

        PoliticalHistoryRollup political = new(
            current.Stage,
            current.SettlementStatus,
            last12.Count(snapshot => snapshot.Stage >= PolityStage.Tribe || snapshot.SettlementCount > 0),
            last12.Count(snapshot => snapshot.HasAgriculture),
            last12.Count(snapshot => snapshot.HasFoodStorage),
            last12.Count(snapshot => snapshot.HasSeasonalPlanning),
            last12.Count(snapshot => snapshot.SettlementCount >= 2));

        ActionableSignalHistoryRollup actionable = new(
            last6.Count(snapshot => snapshot.MigrationPressure >= 0.55 || snapshot.MovedThisMonth),
            last12.Count(snapshot => snapshot.MigrationPressure >= 0.55 || snapshot.MovedThisMonth),
            last6.Count(snapshot => snapshot.FragmentationPressure >= 0.60),
            last12.Count(snapshot => snapshot.FragmentationPressure >= 0.60),
            last6.Count(snapshot => snapshot.ExpansionOpportunityThisMonth),
            last12.Count(snapshot => snapshot.TradeContactThisMonth),
            last6.Sum(snapshot => snapshot.StarvingSettlementCount > 0 ? 1 : 0),
            last12.Sum(snapshot => snapshot.StarvingSettlementCount > 0 ? 1 : 0));

        HistoryShockMarkers shocks = new(
            current.SupportCrashThisMonth,
            last3.Count(snapshot => snapshot.SupportCrashThisMonth),
            last6.Count(snapshot => snapshot.SupportCrashThisMonth),
            last12.Count(snapshot => snapshot.SupportCrashThisMonth),
            current.DisplacementThisMonth,
            last3.Count(snapshot => snapshot.DisplacementThisMonth),
            last6.Count(snapshot => snapshot.DisplacementThisMonth),
            last12.Count(snapshot => snapshot.DisplacementThisMonth),
            current.SettlementLossThisMonth,
            last3.Count(snapshot => snapshot.SettlementLossThisMonth),
            last6.Count(snapshot => snapshot.SettlementLossThisMonth),
            last12.Count(snapshot => snapshot.SettlementLossThisMonth),
            current.CollapseMarkerThisMonth,
            last3.Count(snapshot => snapshot.CollapseMarkerThisMonth),
            last6.Count(snapshot => snapshot.CollapseMarkerThisMonth),
            last12.Count(snapshot => snapshot.CollapseMarkerThisMonth),
            current.IdentityBreakThisMonth,
            last3.Count(snapshot => snapshot.IdentityBreakThisMonth),
            last6.Count(snapshot => snapshot.IdentityBreakThisMonth),
            last12.Count(snapshot => snapshot.IdentityBreakThisMonth));

        EvaluatorHealthSummary health = PrehistoryReadinessEvidenceEvaluator.EvaluateHealth(
            current,
            last3,
            last6,
            last12,
            last24,
            demography,
            support,
            spatial,
            rootedness,
            continuity);

        return new PeopleHistoryWindowSnapshot(
            new PeopleSnapshotHeader(current.PeopleId, current.PeopleName, current.SpeciesId, current.LineageId, current.WorldYear, current.WorldMonth),
            availability,
            currentPeopleState,
            demography,
            support,
            spatial,
            rootedness,
            continuity,
            settlements,
            political,
            actionable,
            shocks,
            health);
    }

    private static List<PeopleMonthlySnapshot> SelectWindow(IReadOnlyList<PeopleMonthlySnapshot> orderedHistory, PeopleMonthlySnapshot current, int months)
        => orderedHistory
            .Where(snapshot => current.AbsoluteMonthIndex - snapshot.AbsoluteMonthIndex < months)
            .ToList();

    private static int CountTransitions(
        IReadOnlyList<PeopleMonthlySnapshot> history,
        Func<PeopleMonthlySnapshot, PeopleMonthlySnapshot, bool> predicate)
    {
        if (history.Count < 2)
        {
            return 0;
        }

        int count = 0;
        for (int index = 1; index < history.Count; index++)
        {
            if (predicate(history[index - 1], history[index]))
            {
                count++;
            }
        }

        return count;
    }

    private static int MonthsSinceLast(
        IReadOnlyList<PeopleMonthlySnapshot> history,
        Func<PeopleMonthlySnapshot, bool> predicate,
        int currentAbsoluteMonthIndex)
    {
        PeopleMonthlySnapshot? last = history.LastOrDefault(predicate);
        return last is null
            ? history.Count == 0 ? 0 : currentAbsoluteMonthIndex - history[0].AbsoluteMonthIndex + 1
            : currentAbsoluteMonthIndex - last.AbsoluteMonthIndex;
    }
}

internal sealed class RegionEvaluationSnapshotBuilder
{
    public IReadOnlyList<RegionEvaluationSnapshot> Build(
        ObserverWorldContext context,
        Polity polity,
        PeopleHistoryWindowSnapshot peopleHistory,
        IReadOnlyList<PeopleMonthlySnapshot> rawHistory)
    {
        Species species = context.GetRequiredSpecies(polity.SpeciesId);
        HashSet<int> relevantRegionIds = [peopleHistory.CurrentPeopleState.CurrentRegionId];
        foreach (int regionId in rawHistory.SelectMany(snapshot => snapshot.OccupiedRegionIds))
        {
            relevantRegionIds.Add(regionId);
        }

        relevantRegionIds.Add(polity.PreviousRegionId);
        foreach (int regionId in relevantRegionIds.ToList())
        {
            if (!context.TryGetRegion(regionId, out Region? region) || region is null)
            {
                continue;
            }

            foreach (int connectedRegionId in region.ConnectedRegionIds)
            {
                relevantRegionIds.Add(connectedRegionId);
            }
        }

        return relevantRegionIds
            .Where(regionId => context.TryGetRegion(regionId, out _))
            .OrderBy(regionId => regionId)
            .Select(regionId => BuildSnapshot(context, polity, species, peopleHistory, rawHistory, regionId))
            .ToList();
    }

    private static RegionEvaluationSnapshot BuildSnapshot(
        ObserverWorldContext context,
        Polity polity,
        Species species,
        PeopleHistoryWindowSnapshot peopleHistory,
        IReadOnlyList<PeopleMonthlySnapshot> rawHistory,
        int regionId)
    {
        Region region = context.GetRequiredRegion(regionId);
        List<int> currentOccupiedRegions = context.GetOccupiedRegionIds(polity);

        RegionSpeciesPopulation? speciesPopulation = region.GetSpeciesPopulation(species.Id);
        double speciesSupportRatio = speciesPopulation is null || polity.Population <= 0
            ? 0.0
            : Math.Clamp(speciesPopulation.PopulationCount / Math.Max(1.0, polity.Population), 0.0, 1.5);
        double plantBiomassRatio = region.MaxPlantBiomass <= 0.0
            ? 0.0
            : Math.Clamp(region.PlantBiomass / region.MaxPlantBiomass, 0.0, 1.0);
        double animalBiomassRatio = region.MaxAnimalBiomass <= 0.0
            ? 0.0
            : Math.Clamp(region.AnimalBiomass / region.MaxAnimalBiomass, 0.0, 1.0);
        double competitionPressure = region.CarryingCapacity <= 0.0
            ? 0.0
            : Math.Clamp(context.GetActivePopulationInRegion(region.Id) / region.CarryingCapacity, 0.0, 2.0);
        int recentExtinctionCount = context.GetRecentExtinctionCount(region.Id);
        double recentInstability = Math.Clamp(
            (recentExtinctionCount * 0.20)
            + Math.Max(0.0, 0.35 - plantBiomassRatio)
            + Math.Max(0.0, 0.25 - animalBiomassRatio),
            0.0,
            1.0);

        RegionGlobalEvaluation global = new(
            region.Id,
            region.Name,
            region.Biome.ToString(),
            region.Fertility,
            region.WaterAvailability,
            plantBiomassRatio,
            animalBiomassRatio,
            speciesSupportRatio,
            region.ConnectedRegionIds.Count,
            context.GetSettlementCountInRegion(region.Id),
            context.GetOccupyingPeopleCountInRegion(region.Id),
            competitionPressure,
            recentInstability);

        bool isOccupied = currentOccupiedRegions.Contains(region.Id);
        bool isFormerHome = region.Id == polity.PreviousRegionId
            || rawHistory.Any(snapshot => snapshot.HomeClusterRegionId == region.Id && !snapshot.OccupiedRegionIds.Contains(region.Id));
        PeopleRegionRelationshipType relationshipType = ResolveRelationshipType(context, polity, peopleHistory, currentOccupiedRegions, region.Id, isOccupied, isFormerHome);
        int presenceMonthsObserved = rawHistory.Count(snapshot => snapshot.OccupiedRegionIds.Contains(region.Id) || snapshot.CurrentRegionId == region.Id);
        double totalSupport = currentOccupiedRegions
            .Select(occupiedRegionId => context.GetRequiredRegion(occupiedRegionId).GetSpeciesPopulation(species.Id)?.PopulationCount ?? 0)
            .Sum();
        double supportContributionShare = totalSupport <= 0.0
            ? 0.0
            : (speciesPopulation?.PopulationCount ?? 0) / totalSupport;
        double supportAdequacy = Math.Clamp(
            speciesSupportRatio
            + (region.Fertility * 0.20)
            + (region.WaterAvailability * 0.15)
            + (plantBiomassRatio * 0.15)
            + (animalBiomassRatio * 0.10),
            0.0,
            1.5);
        double subsistenceCompatibility = ResolveSubsistenceCompatibility(polity, species, region, plantBiomassRatio, animalBiomassRatio);
        double frontierInterpretation = currentOccupiedRegions.Any(occupiedRegionId => context.GetHopDistance(occupiedRegionId, region.Id) == 1)
            ? 1.0
            : 0.0;
        int contactCount = context.GetOtherOccupyingPeopleCountInRegion(region.Id, polity.Id);
        int historicalSignificanceCount = context.GetHistoricalSignificanceCount(polity.Id, region.Id);

        PeopleRegionEvaluation relative = new(
            polity.Id,
            relationshipType,
            polity.RegionId == region.Id,
            isOccupied,
            isFormerHome,
            presenceMonthsObserved,
            supportContributionShare,
            supportAdequacy,
            subsistenceCompatibility,
            frontierInterpretation,
            Math.Clamp(competitionPressure - peopleHistory.CurrentPeopleState.SupportAdequacy, 0.0, 2.0),
            contactCount,
            historicalSignificanceCount);

        return new RegionEvaluationSnapshot(polity.Id, context.World.Time.Year, context.World.Time.Month, global, relative);
    }

    private static PeopleRegionRelationshipType ResolveRelationshipType(
        ObserverWorldContext context,
        Polity polity,
        PeopleHistoryWindowSnapshot peopleHistory,
        IReadOnlyCollection<int> currentOccupiedRegions,
        int regionId,
        bool isOccupied,
        bool isFormerHome)
    {
        if (polity.SettlementCount == 0 && (regionId == polity.RegionId || regionId == polity.PreviousRegionId))
        {
            return PeopleRegionRelationshipType.SeasonalRoute;
        }

        if (isOccupied && regionId == peopleHistory.CurrentPeopleState.CurrentRegionId)
        {
            return PeopleRegionRelationshipType.HomeCore;
        }

        if (isFormerHome && !isOccupied)
        {
            return PeopleRegionRelationshipType.FormerHome;
        }

        if (isOccupied)
        {
            bool adjacentToCore = context.GetHopDistance(peopleHistory.CurrentPeopleState.CurrentRegionId, regionId) <= 1;
            return adjacentToCore
                ? PeopleRegionRelationshipType.HomePeriphery
                : PeopleRegionRelationshipType.Occupied;
        }

        bool adjacentCandidate = currentOccupiedRegions.Any(occupiedRegionId => context.GetHopDistance(occupiedRegionId, regionId) == 1);
        return adjacentCandidate
            ? PeopleRegionRelationshipType.AdjacentCandidate
            : PeopleRegionRelationshipType.KnownNonOccupied;
    }

    private static double ResolveSubsistenceCompatibility(Polity polity, Species species, Region region, double plantBiomassRatio, double animalBiomassRatio)
    {
        return PolityProfileResolver.ResolveSubsistenceMode(polity, species) switch
        {
            SubsistenceMode.FarmingEmergent or SubsistenceMode.ProtoFarming => Math.Clamp((region.Fertility * 0.45) + (region.WaterAvailability * 0.30) + (plantBiomassRatio * 0.10), 0.0, 1.0),
            SubsistenceMode.HuntingFocused => Math.Clamp((animalBiomassRatio * 0.40) + (region.EffectiveEcologyProfile.MigrationEase * 0.20) + (region.WaterAvailability * 0.10), 0.0, 1.0),
            SubsistenceMode.ForagingFocused => Math.Clamp((plantBiomassRatio * 0.30) + (region.Fertility * 0.20) + (region.WaterAvailability * 0.15), 0.0, 1.0),
            _ => Math.Clamp((plantBiomassRatio * 0.20) + (animalBiomassRatio * 0.20) + (region.Fertility * 0.18) + (region.WaterAvailability * 0.12), 0.0, 1.0)
        };
    }
}

internal sealed class NeighborContextSnapshotBuilder
{
    public NeighborContextSnapshot Build(ObserverWorldContext context, Polity polity)
    {
        List<RelevantNeighborFact> relevantNeighbors = ObserverNeighborAnalyzer.GetRelevantNeighbors(context, polity);
        List<NeighborRelationshipSnapshot> relationships = relevantNeighbors
            .OrderBy(neighbor => neighbor.HopDistance)
            .ThenByDescending(neighbor => neighbor.ExertsPressure)
            .ThenBy(neighbor => neighbor.Neighbor.Id)
            .Select(neighbor => new NeighborRelationshipSnapshot(
                polity.Id,
                neighbor.Neighbor.Id,
                neighbor.Neighbor.Name,
                neighbor.Neighbor.SpeciesId,
                neighbor.Neighbor.LineageId,
                neighbor.Neighbor.RegionId,
                neighbor.HopDistance,
                neighbor.SharesBorder,
                neighbor.IsReachable,
                neighbor.ExertsPressure,
                neighbor.OffersExchangeContext,
                neighbor.HasFormerSharedSpace,
                neighbor.SharesLineage,
                neighbor.SettlementFrontierAdjacencyCount,
                neighbor.ContactCount,
                neighbor.PopulationRatio,
                neighbor.RelativePressure,
                neighbor.RelevanceReasons))
            .ToList();
        NeighborhoodSummary summary = new(
            relationships.Count,
            relationships.Count(relationship => relationship.SharesBorder),
            relationships.Count(relationship => relationship.IsReachable),
            relationships.Count(relationship => relationship.ExertsPressure),
            relationships.Count(relationship => relationship.OffersExchangeContext),
            relationships.Count(relationship => relationship.HasFormerSharedSpace));
        NeighborAggregateMetrics aggregates = new(
            relevantNeighbors.Sum(neighbor => neighbor.Neighbor.Population),
            relevantNeighbors.Count(neighbor => neighbor.Neighbor.Population > polity.Population),
            relevantNeighbors.Sum(neighbor => neighbor.SettlementFrontierAdjacencyCount),
            relationships.Count == 0 ? 0.0 : relationships.Average(relationship => relationship.HopDistance),
            relationships.Count == 0 ? 0.0 : relationships.Average(relationship => relationship.RelativePressure));

        return new NeighborContextSnapshot(polity.Id, context.World.Time.Year, context.World.Time.Month, summary, relationships, aggregates);
    }
}

internal static class ObserverNeighborAnalyzer
{
    public static List<RelevantNeighborFact> GetRelevantNeighbors(ObserverWorldContext context, Polity polity)
    {
        List<int> occupiedRegionIds = context.GetOccupiedRegionIds(polity);

        List<RelevantNeighborFact> relevant = [];
        foreach (Polity neighbor in context.ActivePolities.Where(candidate => candidate.Id != polity.Id))
        {
            List<int> neighborRegionIds = context.GetOccupiedRegionIds(neighbor);

            int hopDistance = occupiedRegionIds
                .SelectMany(sourceRegionId => neighborRegionIds.Select(targetRegionId => context.GetHopDistance(sourceRegionId, targetRegionId)))
                .DefaultIfEmpty(int.MaxValue)
                .Min();
            int frontierAdjacencyCount = occupiedRegionIds.Sum(sourceRegionId => neighborRegionIds.Count(targetRegionId => sourceRegionId == targetRegionId || context.GetHopDistance(sourceRegionId, targetRegionId) == 1));
            bool sharesBorder = frontierAdjacencyCount > 0;
            bool reachable = hopDistance <= 3;
            bool hasFormerSharedSpace = polity.PreviousRegionId == neighbor.RegionId
                || neighbor.PreviousRegionId == polity.RegionId
                || occupiedRegionIds.Contains(neighbor.PreviousRegionId)
                || neighborRegionIds.Contains(polity.PreviousRegionId);
            bool sharesLineage = polity.LineageId == neighbor.LineageId;
            bool monthlyTradeContact = polity.TradePartnersThisMonth.Contains(neighbor.Id)
                || neighbor.TradePartnersThisMonth.Contains(polity.Id);
            bool exchangeOpportunity = HasConcreteExchangeOpportunity(polity, neighbor, hopDistance, sharesBorder, hasFormerSharedSpace);
            bool offersExchangeContext = monthlyTradeContact || exchangeOpportunity;
            double populationRatio = neighbor.Population / Math.Max(1.0, polity.Population);
            bool exertsPressure = reachable
                && (sharesBorder || hasFormerSharedSpace)
                && (populationRatio >= 0.80 || neighbor.SettlementCount > polity.SettlementCount || neighbor.FragmentationPressure < polity.FragmentationPressure);

            List<string> reasons = [];
            if (sharesBorder)
            {
                reasons.Add("adjacent_frontier");
            }

            if (reachable)
            {
                reasons.Add("reachable");
            }

            if (hasFormerSharedSpace)
            {
                reasons.Add("former_shared_space");
            }

            if (sharesLineage)
            {
                reasons.Add("shared_lineage");
            }

            if (monthlyTradeContact)
            {
                reasons.Add("monthly_trade_contact");
            }

            if (exchangeOpportunity)
            {
                reasons.Add("exchange_opportunity");
            }

            if (exertsPressure)
            {
                reasons.Add("pressure");
            }

            bool isRelevant = sharesBorder
                || hasFormerSharedSpace
                || exertsPressure
                || offersExchangeContext
                || (sharesLineage && reachable && hopDistance <= 2);
            if (!isRelevant)
            {
                continue;
            }

            double relativePressure = Math.Clamp(
                (populationRatio * 0.55)
                + (sharesBorder ? 0.25 : 0.0)
                + (hasFormerSharedSpace ? 0.10 : 0.0)
                + (neighbor.FragmentationPressure < polity.FragmentationPressure ? 0.10 : 0.0),
                0.0,
                2.0);
            int contactCount = frontierAdjacencyCount
                + (hasFormerSharedSpace ? 1 : 0)
                + (monthlyTradeContact ? 1 : 0);
            relevant.Add(new RelevantNeighborFact(
                neighbor,
                hopDistance == int.MaxValue ? 99 : hopDistance,
                sharesBorder,
                reachable,
                exertsPressure,
                offersExchangeContext,
                hasFormerSharedSpace,
                sharesLineage,
                frontierAdjacencyCount,
                contactCount,
                populationRatio,
                relativePressure,
                reasons));
        }

        return relevant;
    }

    private static bool HasConcreteExchangeOpportunity(Polity polity, Polity neighbor, int hopDistance, bool sharesBorder, bool hasFormerSharedSpace)
    {
        if (hopDistance > 2 || (!sharesBorder && !hasFormerSharedSpace && hopDistance > 1))
        {
            return false;
        }

        return HasTradeableSurplus(polity) && HasExchangeNeed(neighbor)
            || HasTradeableSurplus(neighbor) && HasExchangeNeed(polity);
    }

    private static bool HasTradeableSurplus(Polity polity)
        => polity.FoodSurplusThisMonth > Math.Max(1.0, polity.FoodNeededThisMonth * 0.05)
            || polity.Settlements.Any(settlement => settlement.FoodState == FoodState.Surplus);

    private static bool HasExchangeNeed(Polity polity)
        => polity.FoodShortageThisMonth > Math.Max(1.0, polity.FoodNeededThisMonth * 0.05)
            || polity.FoodSatisfactionThisMonth < 0.95
            || polity.Settlements.Any(settlement => settlement.FoodState is FoodState.Deficit or FoodState.Starving);
}

internal sealed record RelevantNeighborFact(
    Polity Neighbor,
    int HopDistance,
    bool SharesBorder,
    bool IsReachable,
    bool ExertsPressure,
    bool OffersExchangeContext,
    bool HasFormerSharedSpace,
    bool SharesLineage,
    int SettlementFrontierAdjacencyCount,
    int ContactCount,
    double PopulationRatio,
    double RelativePressure,
    IReadOnlyList<string> RelevanceReasons);

internal static class ObserverMath
{
    public static int ToAbsoluteMonthIndex(int year, int month)
        => checked((year * 12) + Math.Clamp(month - 1, 0, 11));

    public static int MonthsBetween(int fromYear, int fromMonth, int toYear, int toMonth)
        => Math.Max(0, ToAbsoluteMonthIndex(toYear, toMonth) - ToAbsoluteMonthIndex(fromYear, fromMonth));

    public static double Average(IReadOnlyList<PeopleMonthlySnapshot> history, Func<PeopleMonthlySnapshot, double> selector)
        => history.Count == 0 ? 0.0 : history.Average(selector);

    public static double Average(IReadOnlyList<PeopleMonthlySnapshot> history, Func<PeopleMonthlySnapshot, int> selector)
        => history.Count == 0 ? 0.0 : history.Average(snapshot => selector(snapshot));

    public static double ComputeLargestConnectedShare(ObserverWorldContext context, IReadOnlyCollection<int> regionIds)
    {
        if (regionIds.Count <= 1)
        {
            return regionIds.Count == 0 ? 0.0 : 1.0;
        }

        HashSet<int> remaining = [.. regionIds];
        int largestComponent = 0;
        while (remaining.Count > 0)
        {
            int seed = remaining.First();
            Queue<int> frontier = new();
            frontier.Enqueue(seed);
            remaining.Remove(seed);
            int size = 0;

            while (frontier.Count > 0)
            {
                int current = frontier.Dequeue();
                size++;
                Region region = context.GetRequiredRegion(current);
                foreach (int connectedRegionId in region.ConnectedRegionIds)
                {
                    if (remaining.Remove(connectedRegionId))
                    {
                        frontier.Enqueue(connectedRegionId);
                    }
                }
            }

            largestComponent = Math.Max(largestComponent, size);
        }

        return Math.Clamp(largestComponent / (double)regionIds.Count, 0.0, 1.0);
    }

    public static int ComputeMaxPairwiseHopDistance(ObserverWorldContext context, IReadOnlyList<int> regionIds)
    {
        if (regionIds.Count <= 1)
        {
            return 0;
        }

        int max = 0;
        for (int left = 0; left < regionIds.Count; left++)
        {
            for (int right = left + 1; right < regionIds.Count; right++)
            {
                int hopDistance = context.GetHopDistance(regionIds[left], regionIds[right]);
                if (hopDistance != int.MaxValue)
                {
                    max = Math.Max(max, hopDistance);
                }
            }
        }

        return max;
    }

    public static int ComputeHopDistance(ObserverWorldContext context, int sourceRegionId, int targetRegionId)
    {
        if (sourceRegionId == targetRegionId)
        {
            return 0;
        }

        Queue<(int regionId, int depth)> queue = new();
        HashSet<int> visited = [sourceRegionId];
        queue.Enqueue((sourceRegionId, 0));

        while (queue.Count > 0)
        {
            (int regionId, int depth) = queue.Dequeue();
            Region region = context.GetRequiredRegion(regionId);
            foreach (int neighborId in region.ConnectedRegionIds)
            {
                if (!visited.Add(neighborId))
                {
                    continue;
                }

                int nextDepth = depth + 1;
                if (neighborId == targetRegionId)
                {
                    return nextDepth;
                }

                queue.Enqueue((neighborId, nextDepth));
            }
        }

        return int.MaxValue;
    }
}
