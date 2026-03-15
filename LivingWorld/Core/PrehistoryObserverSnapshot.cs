using LivingWorld.Societies;

namespace LivingWorld.Core;

public enum PeopleRegionRelationshipType
{
    HomeCore,
    HomePeriphery,
    Occupied,
    SeasonalRoute,
    AdjacentCandidate,
    KnownNonOccupied,
    FormerHome
}

public enum SupportStabilityState
{
    Collapsed,
    Recovering,
    Volatile,
    Stable
}

public enum MovementCoherenceState
{
    Coherent,
    Mixed,
    Scattered
}

public enum RootednessState
{
    Anchored,
    SoftAnchored,
    Displaced
}

public enum ContinuityState
{
    Continuous,
    Fragile,
    Broken
}

public sealed record PeopleMonthlySnapshot(
    int PeopleId,
    string PeopleName,
    int SpeciesId,
    int LineageId,
    int WorldYear,
    int WorldMonth,
    int AbsoluteMonthIndex,
    int Population,
    int CurrentRegionId,
    int PreviousRegionId,
    IReadOnlyList<int> OccupiedRegionIds,
    int HomeClusterRegionId,
    int SettlementCount,
    int SurplusSettlementCount,
    int StableSettlementCount,
    int DeficitSettlementCount,
    int StarvingSettlementCount,
    double HomeClusterShare,
    double ConnectedFootprintShare,
    double ScatterShare,
    int MaxFootprintHopDistance,
    double FoodStores,
    double FoodRequired,
    double FoodProduced,
    double SupportAdequacy,
    double FoodSatisfaction,
    double FoodShortageShare,
    double FoodSurplusShare,
    int OldestSettlementAgeMonths,
    double AverageSettlementAgeMonths,
    int DiscoveryCount,
    int AdvancementCount,
    int TradePartnerCount,
    double MigrationPressure,
    double FragmentationPressure,
    SettlementStatus SettlementStatus,
    PolityStage Stage,
    bool HasManagedFood,
    bool HasAgriculture,
    bool HasFoodStorage,
    bool HasSeasonalPlanning,
    bool IsAnchoredThisMonth,
    bool IsStrongAnchoredThisMonth,
    bool ExpansionOpportunityThisMonth,
    bool TradeContactThisMonth,
    bool MovedThisMonth,
    bool SupportCrashThisMonth,
    bool DisplacementThisMonth,
    bool SettlementLossThisMonth,
    bool CollapseMarkerThisMonth,
    bool IdentityBreakThisMonth,
    bool ActiveIdentityBreakNow,
    int ContinuousIdentityMonthsObserved,
    int RelevantNeighborCount,
    int AdjacentNeighborCount,
    int ReachableNeighborCount,
    int PressureNeighborCount);

public sealed record PeopleSnapshotHeader(
    int PeopleId,
    string PeopleName,
    int SpeciesId,
    int LineageId,
    int WorldYear,
    int WorldMonth);

public sealed record SnapshotWindowAvailability(
    int AvailableMonthlySnapshots,
    int AvailableMonthsSinceFirstObservation,
    bool HasCurrentMonthObservation,
    int AvailableInLast3Months,
    int AvailableInLast6Months,
    int AvailableInLast12Months,
    int AvailableInLast24Months);

public sealed record CurrentPeopleState(
    int Population,
    int CurrentRegionId,
    int SettlementCount,
    double SupportAdequacy,
    double FoodSatisfaction,
    double MigrationPressure,
    double FragmentationPressure,
    double ConnectedFootprintShare,
    double ScatterShare,
    double HomeClusterShare,
    bool IsAnchored,
    bool IsStrongAnchored,
    bool HasCurrentSupportCrash,
    bool HasCurrentDisplacement,
    bool HasCurrentSettlementLoss,
    bool HasCurrentCollapseMarker,
    bool ActiveIdentityBreakNow,
    bool HasExpansionOpportunity,
    bool HasTradeContact,
    int StarvingSettlementCount,
    int RelevantNeighborCount,
    int PressureNeighborCount);

public sealed record DemographyHistoryRollup(
    int CurrentPopulation,
    double AveragePopulationLast6Months,
    double AveragePopulationLast12Months,
    double AveragePopulationLast24Months,
    int GrowthMonthsLast6Months,
    int DeclineMonthsLast12Months,
    int MinimumPopulationLast12Months);

public sealed record SupportHistoryRollup(
    double CurrentSupportAdequacy,
    double AverageSupportAdequacyLast6Months,
    double AverageSupportAdequacyLast12Months,
    double AverageFoodSatisfactionLast12Months,
    int ShortageMonthsLast6Months,
    int ShortageMonthsLast12Months,
    int ShortageMonthsLast24Months,
    int RecoveryMonthsLast6Months,
    int SupportCrashMonthsLast3Months,
    int SupportCrashMonthsLast6Months,
    int SupportCrashMonthsLast12Months);

public sealed record SpatialHistoryRollup(
    int CurrentOccupiedRegionCount,
    double AverageOccupiedRegionCountLast12Months,
    double CurrentConnectedFootprintShare,
    double AverageConnectedFootprintShareLast12Months,
    double CurrentScatterShare,
    int RegionChangeCountLast12Months,
    int MaxFootprintHopDistanceObserved);

public sealed record RootednessHistoryRollup(
    int AnchoredMonthsLast6Months,
    int AnchoredMonthsLast12Months,
    int AnchoredMonthsLast24Months,
    int StrongAnchoredMonthsLast12Months,
    double AverageHomeClusterShareLast12Months,
    int StableSettlementMonthsLast12Months,
    int DisplacementMonthsLast6Months,
    int DisplacementMonthsLast12Months);

public sealed record SocialContinuityHistoryRollup(
    int ObservedContinuousIdentityMonths,
    int MonthsSinceIdentityBreak,
    int IdentityBreakCountLast6Months,
    int IdentityBreakCountLast12Months,
    int IdentityBreakCountLast24Months,
    bool ActiveIdentityBreakNow);

public sealed record SettlementHistoryRollup(
    int CurrentSettlementCount,
    int SettlementPresentMonthsLast6Months,
    int SettlementPresentMonthsLast12Months,
    int SettlementPresentMonthsLast24Months,
    int StableSettlementMonthsLast12Months,
    int StableSettlementMonthsLast24Months,
    int SettlementLossCountLast6Months,
    int SettlementLossCountLast12Months,
    int NewSettlementMonthsLast6Months,
    double AverageOldestSettlementAgeLast12Months);

public sealed record PoliticalHistoryRollup(
    PolityStage CurrentStage,
    SettlementStatus CurrentSettlementStatus,
    int OrganizedMonthsLast12Months,
    int AgricultureMonthsLast12Months,
    int FoodStorageMonthsLast12Months,
    int PlanningMonthsLast12Months,
    int MultiSettlementMonthsLast12Months);

public sealed record ActionableSignalHistoryRollup(
    int MigrationPressureMonthsLast6Months,
    int MigrationPressureMonthsLast12Months,
    int FragmentationPressureMonthsLast6Months,
    int FragmentationPressureMonthsLast12Months,
    int ExpansionOpportunityMonthsLast6Months,
    int TradeContactMonthsLast12Months,
    int StarvingMonthsLast6Months,
    int StarvingMonthsLast12Months);

public sealed record HistoryShockMarkers(
    bool CurrentSupportCrash,
    int SupportCrashMonthsLast3Months,
    int SupportCrashMonthsLast6Months,
    int SupportCrashMonthsLast12Months,
    bool CurrentDisplacement,
    int DisplacementMonthsLast3Months,
    int DisplacementMonthsLast6Months,
    int DisplacementMonthsLast12Months,
    bool CurrentSettlementLoss,
    int SettlementLossMonthsLast3Months,
    int SettlementLossMonthsLast6Months,
    int SettlementLossMonthsLast12Months,
    bool CurrentCollapseMarker,
    int CollapseMonthsLast3Months,
    int CollapseMonthsLast6Months,
    int CollapseMonthsLast12Months,
    bool CurrentIdentityBreak,
    int IdentityBreakMonthsLast3Months,
    int IdentityBreakMonthsLast6Months,
    int IdentityBreakMonthsLast12Months);

public sealed record DemographicHealthSummary(
    int CurrentPopulation,
    double AveragePopulationLast12Months,
    int DeclineMonthsLast12Months,
    int StarvingMonthsLast12Months);

public sealed record SupportStabilityHealth(
    SupportStabilityState State,
    double CurrentSupportAdequacy,
    double AverageSupportAdequacyLast6Months,
    double AverageSupportAdequacyLast12Months,
    int ShortageMonthsLast12Months,
    bool CurrentSupportCrash,
    bool RecoveringNow);

public sealed record MovementCoherenceHealth(
    MovementCoherenceState State,
    double CurrentConnectedFootprintShare,
    double CurrentScatterShare,
    double CurrentFootprintSupportRatio,
    int CoherentMonthsLast6Months,
    int ScatteredMonthsLast6Months,
    int ScatteredMonthsLast12Months);

public sealed record RootednessHealth(
    RootednessState State,
    int AnchoredMonthsLast12Months,
    int StrongAnchoredMonthsLast12Months,
    double AverageHomeClusterShareLast12Months,
    int StableSettlementMonthsLast12Months,
    bool CurrentDisplacement);

public sealed record ContinuityHealth(
    ContinuityState State,
    int ObservedContinuousIdentityMonths,
    int MonthsSinceIdentityBreak,
    int IdentityBreakCountLast12Months,
    bool ActiveIdentityBreakNow);

public sealed record EvaluatorHealthSummary(
    DemographicHealthSummary Demography,
    SupportStabilityHealth Support,
    MovementCoherenceHealth MovementCoherence,
    RootednessHealth Rootedness,
    ContinuityHealth Continuity);

public sealed record PeopleHistoryWindowSnapshot(
    PeopleSnapshotHeader Header,
    SnapshotWindowAvailability WindowAvailability,
    CurrentPeopleState CurrentPeopleState,
    DemographyHistoryRollup DemographyHistoryRollup,
    SupportHistoryRollup SupportHistoryRollup,
    SpatialHistoryRollup SpatialHistoryRollup,
    RootednessHistoryRollup RootednessHistoryRollup,
    SocialContinuityHistoryRollup SocialContinuityHistoryRollup,
    SettlementHistoryRollup SettlementHistoryRollup,
    PoliticalHistoryRollup PoliticalHistoryRollup,
    ActionableSignalHistoryRollup ActionableSignalHistoryRollup,
    HistoryShockMarkers HistoryShockMarkers,
    EvaluatorHealthSummary EvaluatorHealthSummary);

public sealed record RegionGlobalEvaluation(
    int RegionId,
    string RegionName,
    string Biome,
    double Fertility,
    double WaterAvailability,
    double PlantBiomassRatio,
    double AnimalBiomassRatio,
    double SpeciesSupportRatio,
    int ConnectedRegionCount,
    int SettlementCount,
    int OccupyingPeopleCount,
    double CompetitionPressure,
    double RecentInstability);

public sealed record PeopleRegionEvaluation(
    int PeopleId,
    PeopleRegionRelationshipType RelationshipType,
    bool IsCurrentCenterRegion,
    bool IsOccupied,
    bool IsFormerHomeRegion,
    int PresenceMonthsObserved,
    double SupportContributionShare,
    double SupportAdequacy,
    double SubsistenceCompatibility,
    double FrontierInterpretation,
    double RelativeCompetitionPressure,
    int ContactCount,
    int HistoricalSignificanceCount);

public sealed record RegionEvaluationSnapshot(
    int PeopleId,
    int WorldYear,
    int WorldMonth,
    RegionGlobalEvaluation Global,
    PeopleRegionEvaluation Relative);

public sealed record NeighborhoodSummary(
    int RelevantNeighborCount,
    int AdjacentNeighborCount,
    int ReachableNeighborCount,
    int PressureNeighborCount,
    int ExchangeContextNeighborCount,
    int SharedSpaceNeighborCount);

public sealed record NeighborRelationshipSnapshot(
    int PeopleId,
    int NeighborPeopleId,
    string NeighborName,
    int SpeciesId,
    int LineageId,
    int CurrentRegionId,
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

public sealed record NeighborAggregateMetrics(
    int TotalNeighborPopulation,
    int StrongerNeighborCount,
    int SettlementFrontierAdjacencyCount,
    double AverageHopDistance,
    double AverageRelativePressure);

public sealed record NeighborContextSnapshot(
    int PeopleId,
    int WorldYear,
    int WorldMonth,
    NeighborhoodSummary NeighborhoodSummary,
    IReadOnlyList<NeighborRelationshipSnapshot> NeighborRelationships,
    NeighborAggregateMetrics NeighborAggregateMetrics);

public sealed class PrehistoryObserverSnapshot
{
    public PrehistoryObserverSnapshot(
        int worldYear,
        int worldMonth,
        IReadOnlyList<PeopleHistoryWindowSnapshot> peopleHistoryWindows,
        IReadOnlyList<RegionEvaluationSnapshot> regionEvaluations,
        IReadOnlyList<NeighborContextSnapshot> neighborContexts,
        string summary,
        IReadOnlyList<string>? notes = null)
    {
        WorldYear = worldYear;
        WorldMonth = worldMonth;
        PeopleHistoryWindows = peopleHistoryWindows;
        RegionEvaluations = regionEvaluations;
        NeighborContexts = neighborContexts;
        Summary = summary;
        Notes = notes ?? Array.Empty<string>();
    }

    public int WorldYear { get; }
    public int WorldMonth { get; }
    public IReadOnlyList<PeopleHistoryWindowSnapshot> PeopleHistoryWindows { get; }
    public IReadOnlyList<RegionEvaluationSnapshot> RegionEvaluations { get; }
    public IReadOnlyList<NeighborContextSnapshot> NeighborContexts { get; }
    public string Summary { get; }
    public IReadOnlyList<string> Notes { get; }
}
