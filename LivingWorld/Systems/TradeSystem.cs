using LivingWorld.Core;
using LivingWorld.Economy;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class TradeSystem
{
    private const double ExporterReserveMonths = 1.20;
    private const double ExportShareLimitPerMonth = 0.55;
    private const double ImportShortageThreshold = 0.90;
    private const int MaxTradeHops = 2;
    private const int ExtendedLinkHops = 3;
    private const int LinkInactivityMonthsForCollapse = 18;
    private const double AnnualDependencyThreshold = 0.30;
    private const int AnnualDependencyReliefMonths = 4;
    private const double NotableLinkStartQuantity = 8.0;
    private const double PartialReliefThreshold = 0.35;

    private readonly Dictionary<string, TradeLink> _links = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<(int exporterId, int importerId)> _pairsUsedThisMonth = new();
    private readonly Dictionary<int, HashSet<int>> _partnersByPolityThisYear = new();
    private readonly HashSet<int> _dependencyEventRaisedThisYear = new();
    private int _partnerTrackingYear = int.MinValue;

    public void UpdateTrade(World world)
    {
        _pairsUsedThisMonth.Clear();
        EnsurePartnerBuckets(world);
        WorldLookup lookup = new(world);

        List<Polity> activePolities = world.Polities
            .Where(polity => polity.Population > 0)
            .ToList();

        List<Polity> importers = activePolities
            .Where(IsFoodImporter)
            .OrderByDescending(EstimateImportNeed)
            .ToList();

        if (importers.Count == 0)
        {
            TickInactiveLinks(world);
            return;
        }

        List<Polity> exporters = activePolities
            .Where(IsFoodExporter)
            .OrderByDescending(EstimateExportCapacity)
            .ToList();

        if (exporters.Count == 0)
        {
            TickInactiveLinks(world);
            return;
        }

        foreach (Polity importer in importers)
        {
            // 1) internal-bloc redistribution first
            TrySatisfyImporter(world, lookup, importer, exporters, internalPriorityOnly: true);
            // 2) external partners after internal options are exhausted
            TrySatisfyImporter(world, lookup, importer, exporters, internalPriorityOnly: false);
        }

        TickInactiveLinks(world);
    }

    public void UpdateAnnualTrade(World world)
    {
        foreach (Polity polity in world.Polities.Where(p => p.Population > 0))
        {
            EmitAnnualDependencyEvent(world, polity);
            EmitAnnualInternalStabilityEvent(world, polity);
        }

        PruneInactiveLinksAndEmitCollapses(world);
        _dependencyEventRaisedThisYear.Clear();
    }

    private void TrySatisfyImporter(
        World world,
        WorldLookup lookup,
        Polity importer,
        IReadOnlyList<Polity> allExporters,
        bool internalPriorityOnly)
    {
        double remainingNeed = EstimateImportNeed(importer);
        if (remainingNeed <= 0)
        {
            return;
        }

        List<Polity> candidateExporters = allExporters
            .Where(exporter => exporter.Id != importer.Id)
            .Where(exporter => EstimateExportCapacity(exporter) > 0)
            .Where(exporter => IsInternalPriorityPartner(world, importer, exporter) == internalPriorityOnly)
            .Where(exporter => AreTradeReachable(world, exporter, importer))
            .OrderBy(exporter => RegionHopDistance(world, exporter.RegionId, importer.RegionId))
            .ThenByDescending(exporter => GetContinuityScore(exporter, importer))
            .ThenByDescending(exporter => EstimateExporterReliabilityAfterTransfer(exporter, remainingNeed))
            .ThenByDescending(EstimateExportCapacity)
            .ToList();

        foreach (Polity exporter in candidateExporters)
        {
            if (remainingNeed <= 0)
            {
                break;
            }

            if (!_pairsUsedThisMonth.Add((exporter.Id, importer.Id)))
            {
                continue;
            }

            double available = EstimateExportCapacity(exporter);
            if (available <= 0)
            {
                continue;
            }

            double transfer = Math.Min(remainingNeed, available * ExportShareLimitPerMonth);
            if (transfer < 0.25)
            {
                continue;
            }

            double shortageBefore = Math.Max(0, importer.FoodNeededThisMonth - importer.FoodStores);
            ExecuteTransfer(world, lookup, exporter, importer, transfer, shortageBefore, internalPriorityOnly);
            remainingNeed = EstimateImportNeed(importer);
        }
    }

    private void ExecuteTransfer(
        World world,
        WorldLookup lookup,
        Polity exporter,
        Polity importer,
        double quantity,
        double shortageBefore,
        bool internalPriorityMode)
    {
        Region exporterRegion = lookup.GetRequiredRegion(exporter.RegionId, "Trade transfer exporter");
        Region importerRegion = lookup.GetRequiredRegion(importer.RegionId, "Trade transfer importer");
        Species exporterSpecies = lookup.GetRequiredSpecies(exporter.SpeciesId, "Trade transfer exporter");
        Species importerSpecies = lookup.GetRequiredSpecies(importer.SpeciesId, "Trade transfer importer");
        TradeEndpoint exporterEndpoint = ResolveEndpoint(exporter, exporterRegion);
        TradeEndpoint importerEndpoint = ResolveEndpoint(importer, importerRegion);

        double exporterBefore = exporter.FoodStores;
        double importerBefore = importer.FoodStores;

        exporter.FoodStores -= quantity;
        importer.FoodStores += quantity;

        exporter.AnnualFoodExported += quantity;
        importer.AnnualFoodImported += quantity;

        if (internalPriorityMode)
        {
            importer.AnnualFoodImportedInternal += quantity;
        }
        else
        {
            importer.AnnualFoodImportedExternal += quantity;
        }

        RegisterPartner(exporter.Id, importer.Id);
        RegisterPartner(importer.Id, exporter.Id);
        exporter.TradePartnersThisMonth.Add(importer.Id);
        importer.TradePartnersThisMonth.Add(exporter.Id);

        exporter.TradePartnerCountThisYear = _partnersByPolityThisYear[exporter.Id].Count;
        importer.TradePartnerCountThisYear = _partnersByPolityThisYear[importer.Id].Count;

        TradeLink link = GetOrCreateLink(world, exporter, importer, quantity, exporterEndpoint, importerEndpoint, internalPriorityMode);
        link.AgeMonths++;
        link.LastActiveTick = world.Time.Tick;
        link.SuccessfulTransfers++;
        link.TotalQuantityMoved += quantity;
        link.InactiveMonths = 0;

        double shortageAfter = Math.Max(0, importer.FoodNeededThisMonth - importer.FoodStores);
        double mitigated = Math.Max(0, shortageBefore - shortageAfter);
        importer.AnnualTradeNeedMitigated += mitigated;

        ReliefOutcome relief = ClassifyRelief(shortageBefore, shortageAfter);
        if (relief is ReliefOutcome.Partial or ReliefOutcome.Full)
        {
            importer.TradePartialReliefMonthsThisYear++;
        }

        if (relief == ReliefOutcome.Full)
        {
            importer.TradeFullReliefMonthsThisYear++;
            importer.TradeReliefMonthsThisYear++;
        }

        world.AddEvent(
            WorldEventType.TradeTransfer,
            WorldEventSeverity.Minor,
            $"{exporter.Name} sent food to {importer.Name}",
            $"{exporter.Name} transferred {quantity:F1} food to {importer.Name}.",
            reason: internalPriorityMode ? "internal_priority_trade_transfer" : "external_trade_transfer",
            polityId: exporter.Id,
            polityName: exporter.Name,
            relatedPolityId: importer.Id,
            relatedPolityName: importer.Name,
            relatedPolitySpeciesId: importer.SpeciesId,
            relatedPolitySpeciesName: importerSpecies.Name,
            speciesId: exporter.SpeciesId,
            speciesName: exporterSpecies.Name,
            regionId: importer.RegionId,
            regionName: importerRegion.Name,
            settlementId: importerEndpoint.SettlementId,
            settlementName: importerEndpoint.SettlementName,
            before: new Dictionary<string, string>
            {
                ["exporterFoodStores"] = exporterBefore.ToString("F1"),
                ["importerFoodStores"] = importerBefore.ToString("F1"),
                ["shortageBefore"] = shortageBefore.ToString("F1")
            },
            after: new Dictionary<string, string>
            {
                ["exporterFoodStores"] = exporter.FoodStores.ToString("F1"),
                ["importerFoodStores"] = importer.FoodStores.ToString("F1"),
                ["shortageAfter"] = shortageAfter.ToString("F1")
            },
            metadata: new Dictionary<string, string>
            {
                ["resource"] = TradeResourceType.Food.ToString(),
                ["quantity"] = quantity.ToString("F1"),
                ["tradeLinkAgeMonths"] = link.AgeMonths.ToString(),
                ["tradeMode"] = internalPriorityMode ? "internal_priority" : "external",
                ["exporterSettlement"] = exporterEndpoint.SettlementName,
                ["importerSettlement"] = importerEndpoint.SettlementName,
                ["reliefOutcome"] = relief.ToString().ToLowerInvariant()
            });

        if (relief is ReliefOutcome.Partial or ReliefOutcome.Full)
        {
            world.AddEvent(
                WorldEventType.TradeRelief,
                relief == ReliefOutcome.Full ? WorldEventSeverity.Notable : WorldEventSeverity.Minor,
                relief == ReliefOutcome.Full
                    ? $"{importer.Name} stabilized through imported food"
                    : $"{importer.Name} eased shortages through imported food",
                $"{importer.Name} shortage changed from {shortageBefore:F1} to {shortageAfter:F1} after receiving {quantity:F1} food from {exporter.Name}.",
                reason: relief == ReliefOutcome.Full ? "trade_full_relief" : "trade_partial_relief",
                polityId: importer.Id,
                polityName: importer.Name,
                relatedPolityId: exporter.Id,
                relatedPolityName: exporter.Name,
                relatedPolitySpeciesId: exporter.SpeciesId,
                relatedPolitySpeciesName: exporterSpecies.Name,
                speciesId: importer.SpeciesId,
                speciesName: importerSpecies.Name,
                regionId: importerRegion.Id,
                regionName: importerRegion.Name,
                settlementId: importerEndpoint.SettlementId,
                settlementName: importerEndpoint.SettlementName,
                metadata: new Dictionary<string, string>
                {
                    ["resource"] = TradeResourceType.Food.ToString(),
                    ["quantity"] = quantity.ToString("F1"),
                    ["reliefOutcome"] = relief.ToString().ToLowerInvariant(),
                    ["tradeMode"] = internalPriorityMode ? "internal_priority" : "external"
                });
        }
    }

    private TradeLink GetOrCreateLink(
        World world,
        Polity exporter,
        Polity importer,
        double initialQuantity,
        TradeEndpoint exporterEndpoint,
        TradeEndpoint importerEndpoint,
        bool internalPriorityMode)
    {
        string key = $"{exporter.Id}:{importer.Id}:{TradeResourceType.Food}";
        if (_links.TryGetValue(key, out TradeLink? existing))
        {
            return existing;
        }

        TradeLink created = new(
            exporter.Id,
            importer.Id,
            TradeResourceType.Food,
            world.Time.Tick,
            exporterEndpoint.SettlementId,
            exporterEndpoint.SettlementName,
            importerEndpoint.SettlementId,
            importerEndpoint.SettlementName,
            internalPriorityMode);

        _links[key] = created;

        WorldLookup lookup = new(world);
        Region exporterRegion = lookup.GetRequiredRegion(exporter.RegionId, "Trade link creation exporter");
        Region importerRegion = lookup.GetRequiredRegion(importer.RegionId, "Trade link creation importer");
        Species exporterSpecies = lookup.GetRequiredSpecies(exporter.SpeciesId, "Trade link creation exporter");
        Species importerSpecies = lookup.GetRequiredSpecies(importer.SpeciesId, "Trade link creation importer");

        world.AddEvent(
            WorldEventType.TradeLinkStarted,
            initialQuantity >= NotableLinkStartQuantity ? WorldEventSeverity.Notable : WorldEventSeverity.Minor,
            $"{exporter.Name} began trading food with {importer.Name}",
            $"{exporter.Name} established a food trade link to {importer.Name}.",
            reason: internalPriorityMode ? "internal_priority_trade_link_created" : "external_trade_link_created",
            polityId: exporter.Id,
            polityName: exporter.Name,
            relatedPolityId: importer.Id,
            relatedPolityName: importer.Name,
            relatedPolitySpeciesId: importer.SpeciesId,
            relatedPolitySpeciesName: importerSpecies.Name,
            speciesId: exporter.SpeciesId,
            speciesName: exporterSpecies.Name,
            regionId: exporterRegion.Id,
            regionName: exporterRegion.Name,
            settlementId: exporterEndpoint.SettlementId,
            settlementName: exporterEndpoint.SettlementName,
            metadata: new Dictionary<string, string>
            {
                ["resource"] = TradeResourceType.Food.ToString(),
                ["importerRegion"] = importerRegion.Name,
                ["importerSettlement"] = importerEndpoint.SettlementName,
                ["tradeMode"] = internalPriorityMode ? "internal_priority" : "external"
            });

        return created;
    }

    private void TickInactiveLinks(World world)
    {
        foreach (TradeLink link in _links.Values)
        {
            if (link.LastActiveTick < world.Time.Tick)
            {
                link.InactiveMonths++;
            }
        }
    }

    private void PruneInactiveLinksAndEmitCollapses(World world)
    {
        if (_links.Count == 0)
        {
            return;
        }

        List<string> staleKeys = _links
            .Where(entry => entry.Value.InactiveMonths >= LinkInactivityMonthsForCollapse)
            .Select(entry => entry.Key)
            .ToList();

        foreach (string staleKey in staleKeys)
        {
            if (!_links.TryGetValue(staleKey, out TradeLink? staleLink))
            {
                continue;
            }

            Polity? exporter = world.Polities.FirstOrDefault(polity => polity.Id == staleLink.ExporterPolityId);
            Polity? importer = world.Polities.FirstOrDefault(polity => polity.Id == staleLink.ImporterPolityId);
            if (exporter is not null && importer is not null)
            {
                WorldLookup lookup = new(world);
                world.AddEvent(
                    WorldEventType.TradeLinkCollapsed,
                    WorldEventSeverity.Notable,
                    $"Trade between {exporter.Name} and {importer.Name} fell silent",
                    $"Food trade link between {exporter.Name} and {importer.Name} went inactive after {staleLink.AgeMonths} active months.",
                    reason: "trade_link_inactive",
                    polityId: exporter.Id,
                    polityName: exporter.Name,
                    relatedPolityId: importer.Id,
                    relatedPolityName: importer.Name,
                    relatedPolitySpeciesId: importer.SpeciesId,
                    relatedPolitySpeciesName: lookup.GetRequiredSpecies(importer.SpeciesId, "Trade link collapse importer").Name,
                    speciesId: exporter.SpeciesId,
                    speciesName: lookup.GetRequiredSpecies(exporter.SpeciesId, "Trade link collapse exporter").Name,
                    settlementId: staleLink.ExporterSettlementId,
                    settlementName: staleLink.ExporterSettlementName,
                    metadata: new Dictionary<string, string>
                    {
                        ["resource"] = staleLink.ResourceType.ToString(),
                        ["ageMonths"] = staleLink.AgeMonths.ToString(),
                        ["totalQuantity"] = staleLink.TotalQuantityMoved.ToString("F1"),
                        ["successfulTransfers"] = staleLink.SuccessfulTransfers.ToString(),
                        ["tradeMode"] = staleLink.IsInternalPriorityLink ? "internal_priority" : "external"
                    });
            }

            _links.Remove(staleKey);
        }
    }

    private void EmitAnnualDependencyEvent(World world, Polity polity)
    {
        if (_dependencyEventRaisedThisYear.Contains(polity.Id))
        {
            return;
        }

        if (polity.AnnualFoodConsumed <= 0)
        {
            return;
        }

        double externalImportShare = polity.AnnualFoodImportedExternal / polity.AnnualFoodConsumed;
        int reliefMonths = polity.TradePartialReliefMonthsThisYear;
        bool hasStableExternalLink = _links.Values.Any(link =>
            link.ImporterPolityId == polity.Id
            && !link.IsInternalPriorityLink
            && link.AgeMonths >= 12
            && link.InactiveMonths <= 2);

        if (externalImportShare < AnnualDependencyThreshold
            || reliefMonths < AnnualDependencyReliefMonths
            || !hasStableExternalLink)
        {
            return;
        }

        _dependencyEventRaisedThisYear.Add(polity.Id);
        WorldLookup lookup = new(world);
        Region region = lookup.GetRequiredRegion(polity.RegionId, "Annual trade dependency");
        Species species = lookup.GetRequiredSpecies(polity.SpeciesId, "Annual trade dependency");

        world.AddEvent(
            WorldEventType.TradeDependency,
            WorldEventSeverity.Notable,
            $"{polity.Name} became dependent on imported food",
            $"{polity.Name} relied on external imports for {externalImportShare:P0} of annual consumption across {reliefMonths} relief months.",
            reason: "annual_trade_dependency",
            polityId: polity.Id,
            polityName: polity.Name,
            speciesId: polity.SpeciesId,
            speciesName: species.Name,
            regionId: region.Id,
            regionName: region.Name,
            after: new Dictionary<string, string>
            {
                ["annualFoodImportedExternal"] = polity.AnnualFoodImportedExternal.ToString("F1"),
                ["annualExternalImportShare"] = externalImportShare.ToString("F2"),
                ["reliefMonths"] = reliefMonths.ToString()
            });
    }

    private static void EmitAnnualInternalStabilityEvent(World world, Polity polity)
    {
        if (polity.AnnualFoodConsumed <= 0)
        {
            return;
        }

        double internalImportShare = polity.AnnualFoodImportedInternal / polity.AnnualFoodConsumed;
        if (internalImportShare < 0.18 || polity.TradePartialReliefMonthsThisYear < 3)
        {
            return;
        }

        WorldLookup lookup = new(world);
        Region region = lookup.GetRequiredRegion(polity.RegionId, "Annual trade stability");
        Species species = lookup.GetRequiredSpecies(polity.SpeciesId, "Annual trade stability");
        world.AddEvent(
            WorldEventType.TradeRelief,
            WorldEventSeverity.Notable,
            $"{polity.Name} remained stable through internal redistribution",
            $"{polity.Name} covered {internalImportShare:P0} of annual consumption through internal-priority redistribution.",
            reason: "annual_internal_trade_stability",
            polityId: polity.Id,
            polityName: polity.Name,
            speciesId: polity.SpeciesId,
            speciesName: species.Name,
            regionId: region.Id,
            regionName: region.Name,
            metadata: new Dictionary<string, string>
            {
                ["annualFoodImportedInternal"] = polity.AnnualFoodImportedInternal.ToString("F1"),
                ["annualInternalImportShare"] = internalImportShare.ToString("F2")
            });
    }

    private static bool IsFoodExporter(Polity polity)
        => polity.Population > 0
           && polity.FoodNeededThisMonth > 0
           && EstimateExportCapacity(polity) > 0;

    private static bool IsFoodImporter(Polity polity)
        => polity.Population > 0
           && polity.FoodNeededThisMonth > 0
           && (polity.FoodStores / polity.FoodNeededThisMonth) < ImportShortageThreshold;

    private static double EstimateExportCapacity(Polity polity)
    {
        double reserveTarget = polity.FoodNeededThisMonth * ExporterReserveMonths;
        return Math.Max(0, polity.FoodStores - reserveTarget);
    }

    private static double EstimateImportNeed(Polity polity)
    {
        double comfortTarget = polity.FoodNeededThisMonth;
        return Math.Max(0, comfortTarget - polity.FoodStores);
    }

    private static ReliefOutcome ClassifyRelief(double shortageBefore, double shortageAfter)
    {
        if (shortageBefore <= 0)
        {
            return ReliefOutcome.None;
        }

        if (shortageAfter <= 0)
        {
            return ReliefOutcome.Full;
        }

        double mitigatedRatio = Math.Clamp((shortageBefore - shortageAfter) / shortageBefore, 0.0, 1.0);
        return mitigatedRatio >= PartialReliefThreshold ? ReliefOutcome.Partial : ReliefOutcome.None;
    }

    private bool AreTradeReachable(World world, Polity exporter, Polity importer)
    {
        int hopDistance = RegionHopDistance(world, exporter.RegionId, importer.RegionId);
        if (hopDistance <= MaxTradeHops)
        {
            return true;
        }

        TradeLink? existing = FindExistingLink(exporter.Id, importer.Id);
        return existing is not null
            && existing.AgeMonths >= 6
            && existing.InactiveMonths <= 2
            && hopDistance <= ExtendedLinkHops;
    }

    private static int RegionHopDistance(World world, int sourceRegionId, int targetRegionId)
    {
        WorldLookup lookup = new(world);
        if (sourceRegionId == targetRegionId)
        {
            return 0;
        }

        Queue<(int regionId, int depth)> queue = new();
        HashSet<int> visited = new();
        queue.Enqueue((sourceRegionId, 0));
        visited.Add(sourceRegionId);

        while (queue.Count > 0)
        {
            (int currentRegionId, int depth) = queue.Dequeue();
            if (!lookup.TryGetRegion(currentRegionId, out Region? current) || current is null)
            {
                continue;
            }

            foreach (int neighborId in current.ConnectedRegionIds)
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

                if (nextDepth <= ExtendedLinkHops)
                {
                    queue.Enqueue((neighborId, nextDepth));
                }
            }
        }

        return int.MaxValue;
    }

    private double GetContinuityScore(Polity exporter, Polity importer)
    {
        TradeLink? link = FindExistingLink(exporter.Id, importer.Id);
        if (link is null)
        {
            return 0;
        }

        double ageScore = Math.Min(1.0, link.AgeMonths / 24.0);
        double recencyScore = Math.Clamp(1.0 - (link.InactiveMonths / 6.0), 0.0, 1.0);
        return (ageScore * 0.7) + (recencyScore * 0.3);
    }

    private TradeLink? FindExistingLink(int exporterPolityId, int importerPolityId)
    {
        string key = $"{exporterPolityId}:{importerPolityId}:{TradeResourceType.Food}";
        return _links.TryGetValue(key, out TradeLink? link) ? link : null;
    }

    private static double EstimateExporterReliabilityAfterTransfer(Polity exporter, double requestedNeed)
    {
        double available = EstimateExportCapacity(exporter);
        if (available <= 0)
        {
            return 0;
        }

        double planned = Math.Min(requestedNeed, available * ExportShareLimitPerMonth);
        double postTransfer = exporter.FoodStores - planned;
        double reserveTarget = exporter.FoodNeededThisMonth * ExporterReserveMonths;

        if (reserveTarget <= 0)
        {
            return 1.0;
        }

        return Math.Clamp(postTransfer / reserveTarget, 0.0, 2.0);
    }

    private static bool IsInternalPriorityPartner(World world, Polity importer, Polity exporter)
    {
        if (importer.Id == exporter.Id)
        {
            return true;
        }

        return importer.LineageId == exporter.LineageId;
    }

    private static TradeEndpoint ResolveEndpoint(World world, Polity polity)
        => throw new NotSupportedException("Use ResolveEndpoint(polity, region) to keep trade endpoints settlement-aware.");

    private static TradeEndpoint ResolveEndpoint(Polity polity, Region region)
    {
        Settlement? settlement = polity.GetPrimarySettlementInRegion(region.Id) ?? polity.GetPrimarySettlement();
        if (settlement is not null)
        {
            return new TradeEndpoint(settlement.Id, settlement.Name);
        }

        string settlementName = polity.SettlementStatus == SettlementStatus.Nomadic
            ? $"{region.Name} Camps"
            : $"{region.Name} Hearth";
        return new TradeEndpoint(null, settlementName);
    }

    private void EnsurePartnerBuckets(World world)
    {
        if (_partnerTrackingYear != world.Time.Year)
        {
            _partnersByPolityThisYear.Clear();
            _partnerTrackingYear = world.Time.Year;
        }

        foreach (Polity polity in world.Polities)
        {
            if (!_partnersByPolityThisYear.ContainsKey(polity.Id))
            {
                _partnersByPolityThisYear[polity.Id] = new HashSet<int>();
            }
        }
    }

    private void RegisterPartner(int polityId, int partnerId)
    {
        if (!_partnersByPolityThisYear.TryGetValue(polityId, out HashSet<int>? partners))
        {
            partners = new HashSet<int>();
            _partnersByPolityThisYear[polityId] = partners;
        }

        partners.Add(partnerId);
    }

    private enum ReliefOutcome
    {
        None,
        Partial,
        Full
    }

    private readonly record struct TradeEndpoint(int? SettlementId, string SettlementName);
}
