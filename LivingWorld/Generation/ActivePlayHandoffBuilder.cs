using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Generation;

public sealed class ActivePlayHandoffBuilder
{
    public ActivePlayHandoffPackage Build(World world, int polityId)
    {
        Polity polity = world.Polities.First(candidate => candidate.Id == polityId);
        PlayerEntryCandidateSummary candidate = world.PlayerEntryCandidates.First(summary => summary.PolityId == polityId);
        PeopleHistoryWindowSnapshot? history = world.LatestObserverSnapshot?.PeopleHistoryWindows.FirstOrDefault(snapshot => snapshot.Header.PeopleId == polityId);
        NeighborContextSnapshot? neighbors = world.LatestObserverSnapshot?.NeighborContexts.FirstOrDefault(snapshot => snapshot.PeopleId == polityId);
        IReadOnlyList<RegionEvaluationSnapshot> regionEvaluations = world.LatestObserverSnapshot?.RegionEvaluations
            .Where(snapshot => snapshot.PeopleId == polityId)
            .OrderBy(snapshot => snapshot.Global.RegionId)
            .ToArray()
            ?? Array.Empty<RegionEvaluationSnapshot>();
        PeopleMonthlySnapshot? monthlySnapshot = world.PrehistoryObserver.GetPeopleHistory(polityId).LastOrDefault();
        ActiveControlConversionResult conversion = ResolveConversion(polity, candidate, history);

        ActivePlayPlayerOwnershipState playerOwnership = new(
            polity.Id,
            polity.Name,
            polity.SpeciesId,
            candidate.SpeciesName,
            candidate.HomeRegionId,
            candidate.HomeRegionName,
            world.Time.Year,
            world.Time.Month,
            StartsPaused: true);

        ActivePlayStartingControlState startingControl = new(
            polity.Id,
            polity.LineageId,
            polity.Population,
            candidate.CurrentCondition,
            history?.EvaluatorHealthSummary.Support.State ?? SupportStabilityState.Stable,
            history?.EvaluatorHealthSummary.Continuity.State ?? ContinuityState.Established,
            candidate.StabilityMode,
            conversion,
            monthlySnapshot?.OccupiedRegionIds.Distinct().OrderBy(id => id).ToArray() ?? ResolveOccupiedRegions(polity),
            ResolveRouteRegionIds(monthlySnapshot, regionEvaluations),
            BuildRegionRelations(world, polity, regionEvaluations, monthlySnapshot, conversion.SpatialModel),
            BuildSettlementTruth(world, polity),
            BuildNeighborTruth(neighbors));

        ActivePlayChronicleHandoffState chronicle = BuildChronicleHandoff(candidate, conversion);
        ActivePlayKnowledgeVisibilityState knowledgeState = BuildKnowledgeVisibilityState(
            polity,
            regionEvaluations,
            neighbors,
            monthlySnapshot);

        ActivePlayOriginRecord origin = new(
            world.Time.Year,
            world.Time.Month,
            polity.YearsSinceFounded,
            candidate.QualificationReason,
            candidate.EvidenceSentence,
            candidate.CandidateOriginReason,
            candidate.RecentHistoricalNote,
            candidate.DefiningPressureOrOpportunity,
            $"{candidate.PolityName} | {candidate.SpeciesName} | {candidate.HomeRegionName} | {candidate.CurrentCondition}");

        ActivePlayWarningState warningState = new(
            candidate.SafeWarnings.ToArray(),
            candidate.SafeRisks.ToArray(),
            BuildShockSummaries(history),
            BuildOpportunitySummaries(history, neighbors, candidate));

        return new ActivePlayHandoffPackage(
            playerOwnership,
            startingControl,
            chronicle,
            knowledgeState,
            origin,
            warningState,
            DateTime.UtcNow);
    }

    private static ActiveControlConversionResult ResolveConversion(
        Polity polity,
        PlayerEntryCandidateSummary candidate,
        PeopleHistoryWindowSnapshot? history)
    {
        bool polityGatePassed = PassesPolityGate(polity, history);
        ActiveControlKind controlKind = candidate.MaturityBand switch
        {
            CandidateMaturityBand.Mobile => ActiveControlKind.Society,
            CandidateMaturityBand.Anchored => ActiveControlKind.Society,
            CandidateMaturityBand.Settling => polityGatePassed ? ActiveControlKind.Polity : ActiveControlKind.Society,
            CandidateMaturityBand.EmergentPolity => polityGatePassed ? ActiveControlKind.Polity : ActiveControlKind.Society,
            _ => ActiveControlKind.Society
        };
        ActiveControlSpatialModel spatialModel = candidate.MaturityBand switch
        {
            CandidateMaturityBand.Mobile => ActiveControlSpatialModel.Network,
            CandidateMaturityBand.Anchored => ActiveControlSpatialModel.AnchoredHomeRange,
            CandidateMaturityBand.Settling => polityGatePassed ? ActiveControlSpatialModel.TerritorialCore : ActiveControlSpatialModel.AnchoredHomeRange,
            CandidateMaturityBand.EmergentPolity => polityGatePassed ? ActiveControlSpatialModel.TerritorialCore : ActiveControlSpatialModel.AnchoredHomeRange,
            _ => ActiveControlSpatialModel.AnchoredHomeRange
        };

        string governanceSeed = polityGatePassed
            ? "structured local authority"
            : candidate.MaturityBand == CandidateMaturityBand.Mobile
                ? "mobile kin leadership"
                : "anchored council leadership";
        string diplomaticFrame = neighborsAreActive(history)
            ? "neighbor-aware contact frame"
            : "local-facing contact frame";
        string authorityEvidence = polityGatePassed
            ? "organized multi-settlement continuity is already present"
            : "authority remains descriptive rather than institutional";
        string conversionReason = controlKind == ActiveControlKind.Polity
            ? "Polity-grade organization is already supported by settlement depth, organized months, and durable continuity."
            : "The start stays a Society because polity-grade authority is not yet thick enough to justify a stronger wrapper.";

        return new ActiveControlConversionResult(
            controlKind,
            spatialModel,
            candidate.MaturityBand,
            polityGatePassed,
            conversionReason,
            governanceSeed,
            diplomaticFrame,
            authorityEvidence);

        static bool neighborsAreActive(PeopleHistoryWindowSnapshot? snapshot)
            => snapshot?.CurrentPeopleState.RelevantNeighborCount > 0;
    }

    private static bool PassesPolityGate(Polity polity, PeopleHistoryWindowSnapshot? history)
    {
        if (history is null)
        {
            return false;
        }

        PoliticalHistoryRollup political = history.PoliticalHistoryRollup;
        CurrentPeopleState current = history.CurrentPeopleState;
        EvaluatorHealthSummary health = history.EvaluatorHealthSummary;

        return polity.Stage >= PolityStage.Tribe
            && current.SettlementCount >= 2
            && political.OrganizedMonthsLast12Months >= 8
            && political.MultiSettlementMonthsLast12Months >= 4
            && (political.AgricultureMonthsLast12Months >= 4 || political.CurrentSettlementStatus == SettlementStatus.Settled)
            && health.Continuity.State is ContinuityState.Established or ContinuityState.Deep
            && health.Support.State is SupportStabilityState.Stable or SupportStabilityState.Recovering
            && !history.HistoryShockMarkers.CurrentIdentityBreak;
    }

    private static IReadOnlyList<int> ResolveOccupiedRegions(Polity polity)
        => polity.Settlements.Count == 0
            ? [polity.RegionId]
            : polity.Settlements.Select(settlement => settlement.RegionId).Append(polity.RegionId).Distinct().OrderBy(id => id).ToArray();

    private static IReadOnlyList<int> ResolveRouteRegionIds(
        PeopleMonthlySnapshot? monthlySnapshot,
        IReadOnlyList<RegionEvaluationSnapshot> regionEvaluations)
    {
        if (monthlySnapshot is not null && monthlySnapshot.RouteCoverageShare <= 0)
        {
            return Array.Empty<int>();
        }

        return regionEvaluations
            .Where(snapshot => snapshot.Relative.RelationshipType == PeopleRegionRelationshipType.SeasonalRoute)
            .Select(snapshot => snapshot.Global.RegionId)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
    }

    private static IReadOnlyList<ActiveControlRegionRelation> BuildRegionRelations(
        World world,
        Polity polity,
        IReadOnlyList<RegionEvaluationSnapshot> regionEvaluations,
        PeopleMonthlySnapshot? monthlySnapshot,
        ActiveControlSpatialModel spatialModel)
    {
        HashSet<int> occupiedRegionIds = monthlySnapshot?.OccupiedRegionIds.ToHashSet() ?? polity.Settlements.Select(settlement => settlement.RegionId).Append(polity.RegionId).ToHashSet();
        HashSet<int> settlementRegionIds = polity.Settlements.Select(settlement => settlement.RegionId).ToHashSet();
        List<ActiveControlRegionRelation> relations = [];

        foreach (RegionEvaluationSnapshot evaluation in regionEvaluations)
        {
            ActiveControlRegionRelationKind relationKind = evaluation.Relative.RelationshipType switch
            {
                PeopleRegionRelationshipType.HomeCore => spatialModel == ActiveControlSpatialModel.TerritorialCore
                    ? ActiveControlRegionRelationKind.CoreRegion
                    : ActiveControlRegionRelationKind.HomeRange,
                PeopleRegionRelationshipType.HomePeriphery => spatialModel == ActiveControlSpatialModel.Network
                    ? ActiveControlRegionRelationKind.NetworkNode
                    : ActiveControlRegionRelationKind.HomeRange,
                PeopleRegionRelationshipType.Occupied => spatialModel == ActiveControlSpatialModel.Network
                    ? ActiveControlRegionRelationKind.NetworkNode
                    : ActiveControlRegionRelationKind.HomeRange,
                PeopleRegionRelationshipType.SeasonalRoute => ActiveControlRegionRelationKind.RouteCorridor,
                PeopleRegionRelationshipType.AdjacentCandidate => ActiveControlRegionRelationKind.OpportunityEdge,
                PeopleRegionRelationshipType.FormerHome => ActiveControlRegionRelationKind.FormerHome,
                _ => ActiveControlRegionRelationKind.HomeRange
            };

            if (!occupiedRegionIds.Contains(evaluation.Global.RegionId)
                && relationKind is not ActiveControlRegionRelationKind.RouteCorridor
                && relationKind is not ActiveControlRegionRelationKind.OpportunityEdge
                && relationKind is not ActiveControlRegionRelationKind.FormerHome)
            {
                continue;
            }

            relations.Add(new ActiveControlRegionRelation(
                evaluation.Global.RegionId,
                evaluation.Global.RegionName,
                relationKind,
                evaluation.Relative.IsCurrentCenterRegion,
                settlementRegionIds.Contains(evaluation.Global.RegionId),
                evaluation.Relative.SupportAdequacy,
                evaluation.Relative.FrontierInterpretation));
        }

        if (relations.Count == 0)
        {
            Region homeRegion = world.Regions.First(region => region.Id == polity.RegionId);
            relations.Add(new ActiveControlRegionRelation(
                homeRegion.Id,
                homeRegion.Name,
                spatialModel == ActiveControlSpatialModel.TerritorialCore
                    ? ActiveControlRegionRelationKind.CoreRegion
                    : ActiveControlRegionRelationKind.HomeRange,
                IsCurrentCenter: true,
                HasSettlement: settlementRegionIds.Contains(homeRegion.Id),
                SupportAdequacy: 1.0,
                FrontierInterpretation: 0.0));
        }

        return relations
            .OrderByDescending(relation => relation.IsCurrentCenter)
            .ThenBy(relation => relation.RegionName, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ActiveControlSettlementTruth> BuildSettlementTruth(World world, Polity polity)
        => polity.Settlements
            .OrderBy(settlement => settlement.Name, StringComparer.Ordinal)
            .Select(settlement => new ActiveControlSettlementTruth(
                settlement.Id,
                settlement.Name,
                settlement.RegionId,
                world.Regions.First(region => region.Id == settlement.RegionId).Name,
                settlement.YearsEstablished))
            .ToArray();

    private static IReadOnlyList<ActiveControlNeighborTruth> BuildNeighborTruth(NeighborContextSnapshot? neighbors)
    {
        if (neighbors is null)
        {
            return Array.Empty<ActiveControlNeighborTruth>();
        }

        return neighbors.NeighborRelationships
            .OrderByDescending(neighbor => neighbor.ExertsPressure)
            .ThenBy(neighbor => neighbor.HopDistance)
            .ThenBy(neighbor => neighbor.NeighborName, StringComparer.Ordinal)
            .Select(neighbor => new ActiveControlNeighborTruth(
                neighbor.NeighborPeopleId,
                neighbor.NeighborName,
                neighbor.SpeciesId,
                neighbor.CurrentRegionId,
                neighbor.HopDistance,
                neighbor.ExertsPressure,
                neighbor.OffersExchangeContext,
                neighbor.RelativePressure))
            .ToArray();
    }

    private static ActivePlayChronicleHandoffState BuildChronicleHandoff(
        PlayerEntryCandidateSummary candidate,
        ActiveControlConversionResult conversion)
    {
        string controlLabel = conversion.ControlKind == ActiveControlKind.Polity ? "polity" : "society";
        string spatialLabel = conversion.SpatialModel switch
        {
            ActiveControlSpatialModel.Network => "network",
            ActiveControlSpatialModel.AnchoredHomeRange => "anchored home range",
            _ => "territorial core"
        };
        string headline = $"{candidate.PolityName} begins as a {controlLabel} with a {spatialLabel} start in {candidate.HomeRegionName}.";
        string pressureLine = string.IsNullOrWhiteSpace(candidate.DefiningPressureOrOpportunity)
            ? "Inherited pressure and opportunity remain unsettled."
            : $"Pressure and opportunity: {candidate.DefiningPressureOrOpportunity}.";

        return new ActivePlayChronicleHandoffState(
            headline,
            [
                headline,
                $"Why this start qualified: {candidate.QualificationReason}",
                $"Inherited context: {candidate.RecentHistoricalNote}",
                pressureLine
            ]);
    }

    private static ActivePlayKnowledgeVisibilityState BuildKnowledgeVisibilityState(
        Polity polity,
        IReadOnlyList<RegionEvaluationSnapshot> regionEvaluations,
        NeighborContextSnapshot? neighbors,
        PeopleMonthlySnapshot? monthlySnapshot)
    {
        HashSet<int> knownRegionIds = regionEvaluations
            .Select(snapshot => snapshot.Global.RegionId)
            .ToHashSet();
        if (knownRegionIds.Count == 0)
        {
            foreach (int regionId in monthlySnapshot?.OccupiedRegionIds ?? ResolveOccupiedRegions(polity))
            {
                knownRegionIds.Add(regionId);
            }
        }

        knownRegionIds.Add(polity.RegionId);
        foreach (int settlementRegionId in polity.Settlements.Select(settlement => settlement.RegionId))
        {
            knownRegionIds.Add(settlementRegionId);
        }

        foreach (int regionId in polity.Discoveries.Where(discovery => discovery.RegionId.HasValue).Select(discovery => discovery.RegionId!.Value))
        {
            knownRegionIds.Add(regionId);
        }

        HashSet<int> knownSpeciesIds = polity.Discoveries
            .Where(discovery => discovery.SpeciesId.HasValue)
            .Select(discovery => discovery.SpeciesId!.Value)
            .ToHashSet();
        knownSpeciesIds.Add(polity.SpeciesId);
        if (neighbors is not null)
        {
            foreach (NeighborRelationshipSnapshot relationship in neighbors.NeighborRelationships)
            {
                knownSpeciesIds.Add(relationship.SpeciesId);
            }
        }

        HashSet<int> knownPolityIds = [polity.Id];
        if (neighbors is not null)
        {
            foreach (NeighborRelationshipSnapshot relationship in neighbors.NeighborRelationships)
            {
                knownPolityIds.Add(relationship.NeighborPeopleId);
            }
        }

        return new ActivePlayKnowledgeVisibilityState(
            polity.Discoveries
                .OrderBy(discovery => discovery.Category)
                .ThenBy(discovery => discovery.Summary, StringComparer.Ordinal)
                .Select(discovery => discovery.Summary)
                .ToArray(),
            polity.Advancements
                .OrderBy(advancement => advancement)
                .Select(advancement => AdvancementCatalog.Get(advancement).Name)
                .ToArray(),
            knownRegionIds.OrderBy(id => id).ToArray(),
            knownSpeciesIds.OrderBy(id => id).ToArray(),
            knownPolityIds.OrderBy(id => id).ToArray());
    }

    private static IReadOnlyList<string> BuildShockSummaries(PeopleHistoryWindowSnapshot? history)
    {
        if (history is null)
        {
            return Array.Empty<string>();
        }

        List<string> shocks = [];
        HistoryShockMarkers shockMarkers = history.HistoryShockMarkers;
        if (shockMarkers.CurrentSupportCrash || shockMarkers.SupportCrashMonthsLast3Months > 0) shocks.Add("support crash remains recent");
        if (shockMarkers.CurrentDisplacement || shockMarkers.DisplacementMonthsLast3Months > 0) shocks.Add("displacement remains unresolved");
        if (shockMarkers.CurrentSettlementLoss || shockMarkers.SettlementLossMonthsLast3Months > 0) shocks.Add("recent settlement loss still matters");
        if (shockMarkers.CurrentCollapseMarker || shockMarkers.CollapseMonthsLast3Months > 0) shocks.Add("collapse pressure remains active");
        if (shockMarkers.CurrentIdentityBreak || shockMarkers.IdentityBreakMonthsLast3Months > 0) shocks.Add("identity fracture remains recent");
        return shocks.ToArray();
    }

    private static IReadOnlyList<string> BuildOpportunitySummaries(
        PeopleHistoryWindowSnapshot? history,
        NeighborContextSnapshot? neighbors,
        PlayerEntryCandidateSummary candidate)
    {
        List<string> opportunities = [];
        if (history?.CurrentPeopleState.HasExpansionOpportunity == true) opportunities.Add("expansion room is already visible");
        if ((neighbors?.NeighborhoodSummary.ExchangeContextNeighborCount ?? 0) > 0) opportunities.Add("exchange context is already present");
        if (!string.IsNullOrWhiteSpace(candidate.DefiningPressureOrOpportunity)) opportunities.Add(candidate.DefiningPressureOrOpportunity);
        return opportunities.Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToArray();
    }
}
