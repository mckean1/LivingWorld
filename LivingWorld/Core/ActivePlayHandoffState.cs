using System;
using System.Collections.Generic;
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
    IReadOnlyList<string> Discoveries,
    IReadOnlyList<string> LearnedCapabilities,
    IReadOnlyList<int> KnownRegionIds,
    IReadOnlyList<int> KnownSpeciesIds,
    IReadOnlyList<int> KnownPolityIds);

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

public sealed class ActivePlayHandoffState
{
    public ActivePlayHandoffPackage? Package { get; private set; }

    public int? SelectedPolityId => Package?.PlayerOwnership.SelectedPeopleId;
    public int? PlayerEntryWorldYear => Package?.Origin.WorldYear;
    public int? PlayerEntryWorldMonth => Package?.Origin.WorldMonth;
    public int? PlayerEntryPolityAge => Package?.Origin.PolityAge;
    public string? CandidateSummarySnapshot => Package?.Chronicle.SummaryHeadline;
    public DateTime? HandoffTimestampUtc => Package?.HandoffTimestampUtc;
    public bool HasRecordedHandoff => Package is not null;

    public void RecordPackage(ActivePlayHandoffPackage package)
    {
        Package = package;
    }

    public void RecordHandoff(int polityId, int worldYear, int polityAge, string summary)
    {
        RecordPackage(new ActivePlayHandoffPackage(
            new ActivePlayPlayerOwnershipState(
                polityId,
                $"People {polityId}",
                0,
                "Unknown species",
                null,
                null,
                worldYear,
                1,
                true),
            new ActivePlayStartingControlState(
                polityId,
                polityId,
                0,
                "Unknown",
                SupportStabilityState.Stable,
                ContinuityState.Established,
                "unknown",
                new ActiveControlConversionResult(
                    ActiveControlKind.Society,
                    ActiveControlSpatialModel.AnchoredHomeRange,
                    CandidateMaturityBand.Anchored,
                    false,
                    "Legacy handoff summary.",
                    "shared leadership",
                    "local ties",
                    "legacy summary only"),
                Array.Empty<int>(),
                Array.Empty<int>(),
                Array.Empty<ActiveControlRegionRelation>(),
                Array.Empty<ActiveControlSettlementTruth>(),
                Array.Empty<ActiveControlNeighborTruth>()),
            new ActivePlayChronicleHandoffState(
                summary,
                [summary]),
            new ActivePlayKnowledgeVisibilityState(
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<int>(),
                Array.Empty<int>(),
                Array.Empty<int>()),
            new ActivePlayOriginRecord(
                worldYear,
                1,
                polityAge,
                summary,
                summary,
                string.Empty,
                string.Empty,
                string.Empty,
                summary),
            new ActivePlayWarningState(
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()),
            DateTime.UtcNow));
    }

    public void SetSelectedPolity(int? polityId)
    {
        if (!polityId.HasValue)
        {
            Package = null;
            return;
        }

        if (Package is not null && Package.PlayerOwnership.SelectedPeopleId == polityId.Value)
        {
            return;
        }

        RecordHandoff(polityId.Value, 0, 0, $"Selected polity {polityId.Value}");
    }
}
