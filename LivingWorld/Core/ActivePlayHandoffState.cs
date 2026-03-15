using System;
using System.Collections.Generic;
using LivingWorld.Advancement;
using LivingWorld.Societies;

namespace LivingWorld.Core;

public enum ActiveControlKind
{
    Society,
    Polity
}

public enum ActiveControlSpatialModel
{
    Network,
    AnchoredHomeRange,
    TerritorialCore
}

public enum ActiveControlRegionRelationKind
{
    CoreRegion,
    HomeRange,
    NetworkNode,
    RouteCorridor,
    OpportunityEdge,
    FormerHome
}

public sealed record ActivePlayPlayerOwnershipState(
    int SelectedPeopleId,
    string SelectedPeopleName,
    int SelectedSpeciesId,
    string SelectedSpeciesName,
    int? HomeRegionId,
    string? HomeRegionName,
    int WorldYear,
    int WorldMonth,
    bool StartsPaused);

public sealed record ActiveControlConversionResult(
    ActiveControlKind ControlKind,
    ActiveControlSpatialModel SpatialModel,
    CandidateMaturityBand SourceMaturityBand,
    bool PolityGatePassed,
    string ConversionReason,
    string GovernanceSeed,
    string DiplomaticFrame,
    string AuthorityEvidence);

public sealed record ActiveControlRegionRelation(
    int RegionId,
    string RegionName,
    ActiveControlRegionRelationKind RelationKind,
    bool IsCurrentCenter,
    bool HasSettlement,
    double SupportAdequacy,
    double FrontierInterpretation);

public sealed record ActiveControlSettlementTruth(
    int SettlementId,
    string SettlementName,
    int RegionId,
    string RegionName,
    int EstablishedYears);

public sealed record ActiveControlNeighborTruth(
    int NeighborPeopleId,
    string NeighborName,
    int SpeciesId,
    int CurrentRegionId,
    int HopDistance,
    bool ExertsPressure,
    bool OffersExchangeContext,
    double RelativePressure);

public sealed record ActivePlayStartingControlState(
    int SourcePolityId,
    int LineageId,
    int Population,
    string CurrentCondition,
    SupportStabilityState SupportStability,
    ContinuityState Continuity,
    string StabilityMode,
    ActiveControlConversionResult Conversion,
    IReadOnlyList<int> OccupiedRegionIds,
    IReadOnlyList<int> RouteRegionIds,
    IReadOnlyList<ActiveControlRegionRelation> RegionRelations,
    IReadOnlyList<ActiveControlSettlementTruth> Settlements,
    IReadOnlyList<ActiveControlNeighborTruth> Neighbors);

public sealed record ActivePlayChronicleHandoffState(
    string SummaryHeadline,
    IReadOnlyList<string> SummaryLines);

public sealed record ActivePlayKnowledgeVisibilityState(
    IReadOnlyList<CulturalDiscovery> DiscoveryRecords,
    IReadOnlyList<AdvancementId> LearnedCapabilityIds,
    IReadOnlyList<int> KnownRegionIds,
    IReadOnlyList<int> KnownSpeciesIds,
    IReadOnlyList<int> KnownPolityIds)
{
    public IReadOnlyList<string> Discoveries
        => DiscoveryRecords.Select(discovery => discovery.Summary).ToArray();

    public IReadOnlyList<string> LearnedCapabilities
        => LearnedCapabilityIds.Select(id => AdvancementCatalog.Get(id).Name).ToArray();
}

public sealed record ActivePlayOriginRecord(
    int WorldYear,
    int WorldMonth,
    int PolityAge,
    string QualificationReason,
    string EvidenceSummary,
    string CandidateOriginReason,
    string RecentHistoricalNote,
    string DefiningPressureOrOpportunity,
    string SelectionSummary);

public sealed record ActivePlayWarningState(
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> UnresolvedShocks,
    IReadOnlyList<string> Opportunities);

public sealed record ActivePlayHandoffPackage(
    ActivePlayPlayerOwnershipState PlayerOwnership,
    ActivePlayStartingControlState StartingControl,
    ActivePlayChronicleHandoffState Chronicle,
    ActivePlayKnowledgeVisibilityState Knowledge,
    ActivePlayOriginRecord Origin,
    ActivePlayWarningState Warnings,
    DateTime HandoffTimestampUtc);

public sealed record ActivePlayRuntimeControlState(
    int SelectedPeopleId,
    string SelectedPeopleName,
    int SourcePolityId,
    int LineageId,
    int SelectedSpeciesId,
    string SelectedSpeciesName,
    int? HomeRegionId,
    string? HomeRegionName,
    int? CurrentCenterRegionId,
    ActiveControlKind ControlKind,
    ActiveControlSpatialModel SpatialModel,
    string GovernanceSeed,
    string DiplomaticFrame,
    string AuthorityEvidence);

public sealed class ActivePlayHandoffState
{
    public ActivePlayHandoffPackage? Package { get; private set; }
    public ActivePlayRuntimeControlState? RuntimeControl { get; private set; }

    public int? SelectedPolityId => RuntimeControl?.SourcePolityId;
    public int? PlayerEntryWorldYear => Package?.Origin.WorldYear;
    public int? PlayerEntryWorldMonth => Package?.Origin.WorldMonth;
    public int? PlayerEntryPolityAge => Package?.Origin.PolityAge;
    public string? CandidateSummarySnapshot => Package?.Chronicle.SummaryHeadline;
    public DateTime? HandoffTimestampUtc => Package?.HandoffTimestampUtc;
    public bool HasRecordedHandoff => Package is not null;

    public void RecordPackage(ActivePlayHandoffPackage package)
    {
        Package = package;
        RuntimeControl = CreateRuntimeControl(package);
    }

    public void Clear()
    {
        Package = null;
        RuntimeControl = null;
    }

    private static ActivePlayRuntimeControlState CreateRuntimeControl(ActivePlayHandoffPackage package)
    {
        ActiveControlRegionRelation? currentCenter = package.StartingControl.RegionRelations.FirstOrDefault(relation => relation.IsCurrentCenter);
        return new ActivePlayRuntimeControlState(
            package.PlayerOwnership.SelectedPeopleId,
            package.PlayerOwnership.SelectedPeopleName,
            package.StartingControl.SourcePolityId,
            package.StartingControl.LineageId,
            package.PlayerOwnership.SelectedSpeciesId,
            package.PlayerOwnership.SelectedSpeciesName,
            package.PlayerOwnership.HomeRegionId,
            package.PlayerOwnership.HomeRegionName,
            currentCenter?.RegionId ?? package.PlayerOwnership.HomeRegionId,
            package.StartingControl.Conversion.ControlKind,
            package.StartingControl.Conversion.SpatialModel,
            package.StartingControl.Conversion.GovernanceSeed,
            package.StartingControl.Conversion.DiplomaticFrame,
            package.StartingControl.Conversion.AuthorityEvidence);
    }
}
