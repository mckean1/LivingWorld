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
        int absoluteMonthIndex = ObserverMath.ToAbsoluteMonthIndex(world.Time.Year, world.Time.Month);
        foreach (Polity polity in world.Polities
                     .Where(candidate => candidate.Population > 0)
                     .OrderBy(candidate => candidate.Id))
        {
            PeopleMonthlySnapshot? previous = world.Prehistory.Observer.GetLatestBeforeMonth(polity.Id, absoluteMonthIndex);
            PeopleMonthlySnapshot snapshot = _monthlyExtractor.Create(world, polity, previous);
            world.Prehistory.Observer.Upsert(snapshot);
        }
    }

    public PrehistoryObserverSnapshot Observe(World world)
    {
        List<PeopleHistoryWindowSnapshot> peopleHistoryWindows = [];
        List<RegionEvaluationSnapshot> regionEvaluations = [];
        List<NeighborContextSnapshot> neighborContexts = [];

        foreach (Polity polity in world.Polities
                     .Where(candidate => candidate.Population > 0)
                     .OrderBy(candidate => candidate.Id))
        {
            IReadOnlyList<PeopleMonthlySnapshot> history = GetObservedHistory(world, polity);
            if (history.Count == 0)
            {
                continue;
            }

            PeopleHistoryWindowSnapshot peopleHistory = _historyBuilder.Build(history);
            peopleHistoryWindows.Add(peopleHistory);
            regionEvaluations.AddRange(_regionBuilder.Build(world, polity, peopleHistory, history));
            neighborContexts.Add(_neighborBuilder.Build(world, polity));
        }

        return new PrehistoryObserverSnapshot(
            world.Time.Year,
            world.Time.Month,
            peopleHistoryWindows,
            regionEvaluations,
            neighborContexts,
            $"Observer snapshot for {peopleHistoryWindows.Count} people at year {world.Time.Year}, month {world.Time.Month}.",
            [$"region_contexts:{regionEvaluations.Count}", $"neighbor_contexts:{neighborContexts.Count}"]);
    }

    private List<PeopleMonthlySnapshot> GetObservedHistory(World world, Polity polity)
    {
        int absoluteMonthIndex = ObserverMath.ToAbsoluteMonthIndex(world.Time.Year, world.Time.Month);
        List<PeopleMonthlySnapshot> history = world.Prehistory.Observer.GetPeopleHistory(polity.Id).ToList();
        if (history.Any(snapshot => snapshot.AbsoluteMonthIndex == absoluteMonthIndex))
        {
            return history;
        }

        PeopleMonthlySnapshot? previous = world.Prehistory.Observer.GetLatestBeforeMonth(polity.Id, absoluteMonthIndex);
        history.Add(_monthlyExtractor.Create(world, polity, previous));
        history.Sort(static (left, right) => left.AbsoluteMonthIndex.CompareTo(right.AbsoluteMonthIndex));
        return history;
    }
}

public sealed class PeopleMonthlySnapshotExtractor
{
    public PeopleMonthlySnapshot Create(World world, Polity polity, PeopleMonthlySnapshot? previous)
    {
        Species species = world.Species.First(candidate => candidate.Id == polity.SpeciesId);
        List<int> occupiedRegionIds = polity.Settlements
            .Select(settlement => settlement.RegionId)
            .Distinct()
            .OrderBy(regionId => regionId)
            .ToList();
        if (occupiedRegionIds.Count == 0)
        {
            occupiedRegionIds.Add(polity.RegionId);
        }

        int settlementCount = polity.SettlementCount;
        int homeClusterRegionId = occupiedRegionIds[0];
        double homeClusterShare = 1.0;
        int oldestSettlementAgeMonths = 0;
        double averageSettlementAgeMonths = 0.0;
        int surplusSettlements = 0;
        int stableSettlements = settlementCount == 0 ? 1 : 0;
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
        double connectedFootprintShare = ObserverMath.ComputeLargestConnectedShare(world, occupiedRegionIds);
        double scatterShare = Math.Clamp(1.0 - connectedFootprintShare, 0.0, 1.0);
        int maxFootprintHopDistance = ObserverMath.ComputeMaxPairwiseHopDistance(world, occupiedRegionIds);
        bool movedThisMonth = polity.MovedThisYear && polity.PreviousRegionId != polity.RegionId;
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
                : previous.ContinuousIdentityMonthsObserved + Math.Max(1, ObserverMath.ToAbsoluteMonthIndex(world.Time.Year, world.Time.Month) - previous.AbsoluteMonthIndex);
        bool anchoredThisMonth = settlementCount > 0 && homeClusterShare >= 0.50 && oldestSettlementAgeMonths >= 6 && !movedThisMonth;
        bool strongAnchoredThisMonth = anchoredThisMonth && homeClusterShare >= 0.75 && oldestSettlementAgeMonths >= 12 && starvingSettlements == 0;
        bool expansionOpportunityThisMonth = settlementCount > 0
            && supportAdequacy >= 1.0
            && starvingSettlements == 0
            && polity.MigrationPressure <= 0.35
            && polity.FragmentationPressure <= 0.45;
        bool tradeContactThisMonth = polity.TradePartnerCountThisYear > 0;
        List<RelevantNeighborFact> neighbors = ObserverNeighborAnalyzer.GetRelevantNeighbors(world, polity);

        return new PeopleMonthlySnapshot(
            polity.Id,
            polity.Name,
            polity.SpeciesId,
            polity.LineageId,
            world.Time.Year,
            world.Time.Month,
            ObserverMath.ToAbsoluteMonthIndex(world.Time.Year, world.Time.Month),
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
            polity.TradePartnerCountThisYear,
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

        EvaluatorHealthSummary health = BuildHealth(current, last6, last12, demography, support, rootedness, continuity);

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

    private static EvaluatorHealthSummary BuildHealth(
        PeopleMonthlySnapshot current,
        IReadOnlyList<PeopleMonthlySnapshot> last6,
        IReadOnlyList<PeopleMonthlySnapshot> last12,
        DemographyHistoryRollup demography,
        SupportHistoryRollup support,
        RootednessHistoryRollup rootedness,
        SocialContinuityHistoryRollup continuity)
    {
        bool recoveringNow = current.SupportAdequacy >= 0.80
            && last6.Any(snapshot => snapshot.SupportCrashThisMonth || snapshot.FoodShortageShare > 0.10);
        SupportStabilityState supportState = current.SupportAdequacy <= 0.45 || current.StarvingSettlementCount > 0
            ? SupportStabilityState.Collapsed
            : recoveringNow
                ? SupportStabilityState.Recovering
                : support.ShortageMonthsLast12Months >= Math.Max(3, last12.Count / 3)
                    ? SupportStabilityState.Volatile
                    : SupportStabilityState.Stable;
        double footprintSupportRatio = current.OccupiedRegionIds.Count == 0
            ? current.SupportAdequacy
            : current.SupportAdequacy / Math.Max(1, current.OccupiedRegionIds.Count);
        int coherentMonthsLast6 = last6.Count(snapshot => snapshot.ConnectedFootprintShare >= 0.75 && snapshot.ScatterShare <= 0.25);
        int scatteredMonthsLast6 = last6.Count(snapshot => snapshot.ScatterShare >= 0.40);
        int scatteredMonthsLast12 = last12.Count(snapshot => snapshot.ScatterShare >= 0.40);
        MovementCoherenceState movementState = current.ScatterShare >= 0.40 || current.ConnectedFootprintShare <= 0.50
            ? MovementCoherenceState.Scattered
            : coherentMonthsLast6 >= Math.Max(1, last6.Count / 2)
                ? MovementCoherenceState.Coherent
                : MovementCoherenceState.Mixed;
        RootednessState rootednessState = current.DisplacementThisMonth
            ? RootednessState.Displaced
            : current.IsStrongAnchoredThisMonth || rootedness.StrongAnchoredMonthsLast12Months >= Math.Max(2, last12.Count / 2)
                ? RootednessState.Anchored
                : RootednessState.SoftAnchored;
        ContinuityState continuityState = current.ActiveIdentityBreakNow
            ? ContinuityState.Broken
            : continuity.IdentityBreakCountLast12Months > 0 || continuity.ObservedContinuousIdentityMonths < 12
                ? ContinuityState.Fragile
                : ContinuityState.Continuous;

        return new EvaluatorHealthSummary(
            new DemographicHealthSummary(
                demography.CurrentPopulation,
                demography.AveragePopulationLast12Months,
                demography.DeclineMonthsLast12Months,
                last12.Count(snapshot => snapshot.StarvingSettlementCount > 0)),
            new SupportStabilityHealth(
                supportState,
                current.SupportAdequacy,
                support.AverageSupportAdequacyLast6Months,
                support.AverageSupportAdequacyLast12Months,
                support.ShortageMonthsLast12Months,
                current.SupportCrashThisMonth,
                recoveringNow),
            new MovementCoherenceHealth(
                movementState,
                current.ConnectedFootprintShare,
                current.ScatterShare,
                footprintSupportRatio,
                coherentMonthsLast6,
                scatteredMonthsLast6,
                scatteredMonthsLast12),
            new RootednessHealth(
                rootednessState,
                rootedness.AnchoredMonthsLast12Months,
                rootedness.StrongAnchoredMonthsLast12Months,
                rootedness.AverageHomeClusterShareLast12Months,
                rootedness.StableSettlementMonthsLast12Months,
                current.DisplacementThisMonth),
            new ContinuityHealth(
                continuityState,
                continuity.ObservedContinuousIdentityMonths,
                continuity.MonthsSinceIdentityBreak,
                continuity.IdentityBreakCountLast12Months,
                continuity.ActiveIdentityBreakNow));
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

public sealed class RegionEvaluationSnapshotBuilder
{
    public IReadOnlyList<RegionEvaluationSnapshot> Build(
        World world,
        Polity polity,
        PeopleHistoryWindowSnapshot peopleHistory,
        IReadOnlyList<PeopleMonthlySnapshot> rawHistory)
    {
        Species species = world.Species.First(candidate => candidate.Id == polity.SpeciesId);
        HashSet<int> relevantRegionIds = [peopleHistory.CurrentPeopleState.CurrentRegionId];
        foreach (int regionId in rawHistory.SelectMany(snapshot => snapshot.OccupiedRegionIds))
        {
            relevantRegionIds.Add(regionId);
        }

        relevantRegionIds.Add(polity.PreviousRegionId);
        foreach (int regionId in relevantRegionIds.ToList())
        {
            Region region = world.Regions.First(candidate => candidate.Id == regionId);
            foreach (int connectedRegionId in region.ConnectedRegionIds)
            {
                relevantRegionIds.Add(connectedRegionId);
            }
        }

        return relevantRegionIds
            .Where(regionId => world.Regions.Any(region => region.Id == regionId))
            .OrderBy(regionId => regionId)
            .Select(regionId => BuildSnapshot(world, polity, species, peopleHistory, rawHistory, regionId))
            .ToList();
    }

    private static RegionEvaluationSnapshot BuildSnapshot(
        World world,
        Polity polity,
        Species species,
        PeopleHistoryWindowSnapshot peopleHistory,
        IReadOnlyList<PeopleMonthlySnapshot> rawHistory,
        int regionId)
    {
        Region region = world.Regions.First(candidate => candidate.Id == regionId);
        List<int> currentOccupiedRegions = polity.Settlements.Select(settlement => settlement.RegionId).Distinct().ToList();
        if (currentOccupiedRegions.Count == 0)
        {
            currentOccupiedRegions.Add(polity.RegionId);
        }

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
            : Math.Clamp(world.Polities.Where(candidate => candidate.Population > 0 && candidate.RegionId == region.Id).Sum(candidate => candidate.Population) / region.CarryingCapacity, 0.0, 2.0);
        int recentExtinctionCount = world.LocalPopulationExtinctions.Count(record => record.RegionId == region.Id && ObserverMath.MonthsBetween(record.Year, record.Month, world.Time.Year, world.Time.Month) < 12);
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
            world.Polities.Sum(candidate => candidate.Settlements.Count(settlement => settlement.RegionId == region.Id)),
            world.Polities.Count(candidate => candidate.Population > 0 && (candidate.RegionId == region.Id || candidate.Settlements.Any(settlement => settlement.RegionId == region.Id))),
            competitionPressure,
            recentInstability);

        bool isOccupied = currentOccupiedRegions.Contains(region.Id);
        bool isFormerHome = region.Id == polity.PreviousRegionId
            || rawHistory.Any(snapshot => snapshot.HomeClusterRegionId == region.Id && !snapshot.OccupiedRegionIds.Contains(region.Id));
        PeopleRegionRelationshipType relationshipType = ResolveRelationshipType(world, polity, peopleHistory, currentOccupiedRegions, region.Id, isOccupied, isFormerHome);
        int presenceMonthsObserved = rawHistory.Count(snapshot => snapshot.OccupiedRegionIds.Contains(region.Id) || snapshot.CurrentRegionId == region.Id);
        double totalSupport = currentOccupiedRegions
            .Select(occupiedRegionId => world.Regions.First(candidate => candidate.Id == occupiedRegionId).GetSpeciesPopulation(species.Id)?.PopulationCount ?? 0)
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
        double frontierInterpretation = currentOccupiedRegions.Any(occupiedRegionId => ObserverMath.ComputeHopDistance(world, occupiedRegionId, region.Id) == 1)
            ? 1.0
            : 0.0;
        int contactCount = world.Polities.Count(candidate => candidate.Population > 0 && candidate.Id != polity.Id && (candidate.RegionId == region.Id || candidate.Settlements.Any(settlement => settlement.RegionId == region.Id)));
        int historicalSignificanceCount = world.CivilizationalHistory.Count(entry => entry.PolityId == polity.Id && entry.RegionId == region.Id);

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

        return new RegionEvaluationSnapshot(polity.Id, world.Time.Year, world.Time.Month, global, relative);
    }

    private static PeopleRegionRelationshipType ResolveRelationshipType(
        World world,
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
            bool adjacentToCore = ObserverMath.ComputeHopDistance(world, peopleHistory.CurrentPeopleState.CurrentRegionId, regionId) <= 1;
            return adjacentToCore
                ? PeopleRegionRelationshipType.HomePeriphery
                : PeopleRegionRelationshipType.Occupied;
        }

        bool adjacentCandidate = currentOccupiedRegions.Any(occupiedRegionId => ObserverMath.ComputeHopDistance(world, occupiedRegionId, regionId) == 1);
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

public sealed class NeighborContextSnapshotBuilder
{
    public NeighborContextSnapshot Build(World world, Polity polity)
    {
        List<RelevantNeighborFact> relevantNeighbors = ObserverNeighborAnalyzer.GetRelevantNeighbors(world, polity);
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

        return new NeighborContextSnapshot(polity.Id, world.Time.Year, world.Time.Month, summary, relationships, aggregates);
    }
}

internal static class ObserverNeighborAnalyzer
{
    public static List<RelevantNeighborFact> GetRelevantNeighbors(World world, Polity polity)
    {
        List<int> occupiedRegionIds = polity.Settlements.Select(settlement => settlement.RegionId).Distinct().ToList();
        if (occupiedRegionIds.Count == 0)
        {
            occupiedRegionIds.Add(polity.RegionId);
        }

        List<RelevantNeighborFact> relevant = [];
        foreach (Polity neighbor in world.Polities
                     .Where(candidate => candidate.Population > 0 && candidate.Id != polity.Id)
                     .OrderBy(candidate => candidate.Id))
        {
            List<int> neighborRegionIds = neighbor.Settlements.Select(settlement => settlement.RegionId).Distinct().ToList();
            if (neighborRegionIds.Count == 0)
            {
                neighborRegionIds.Add(neighbor.RegionId);
            }

            int hopDistance = occupiedRegionIds
                .SelectMany(sourceRegionId => neighborRegionIds.Select(targetRegionId => ObserverMath.ComputeHopDistance(world, sourceRegionId, targetRegionId)))
                .DefaultIfEmpty(int.MaxValue)
                .Min();
            int frontierAdjacencyCount = occupiedRegionIds.Sum(sourceRegionId => neighborRegionIds.Count(targetRegionId => sourceRegionId == targetRegionId || ObserverMath.ComputeHopDistance(world, sourceRegionId, targetRegionId) == 1));
            bool sharesBorder = frontierAdjacencyCount > 0;
            bool reachable = hopDistance <= 3;
            bool hasFormerSharedSpace = polity.PreviousRegionId == neighbor.RegionId
                || neighbor.PreviousRegionId == polity.RegionId
                || occupiedRegionIds.Contains(neighbor.PreviousRegionId)
                || neighborRegionIds.Contains(polity.PreviousRegionId);
            bool sharesLineage = polity.LineageId == neighbor.LineageId;
            bool offersExchangeContext = reachable && (sharesLineage || polity.TradePartnerCountThisYear > 0 || neighbor.TradePartnerCountThisYear > 0);
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

            if (offersExchangeContext)
            {
                reasons.Add("exchange_context");
            }

            if (exertsPressure)
            {
                reasons.Add("pressure");
            }

            if (reasons.Count == 0)
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
                + (sharesLineage ? 1 : 0)
                + (offersExchangeContext ? 1 : 0);
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

    public static double ComputeLargestConnectedShare(World world, IReadOnlyCollection<int> regionIds)
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
                Region region = world.Regions.First(candidate => candidate.Id == current);
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

    public static int ComputeMaxPairwiseHopDistance(World world, IReadOnlyList<int> regionIds)
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
                int hopDistance = ComputeHopDistance(world, regionIds[left], regionIds[right]);
                if (hopDistance != int.MaxValue)
                {
                    max = Math.Max(max, hopDistance);
                }
            }
        }

        return max;
    }

    public static int ComputeHopDistance(World world, int sourceRegionId, int targetRegionId)
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
            Region region = world.Regions.First(candidate => candidate.Id == regionId);
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
