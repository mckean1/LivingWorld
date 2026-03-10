using LivingWorld.Core;
using LivingWorld.Economy;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class TradeSystem
{
    private const double ExporterReserveMonths = 1.15;
    private const double ExportShareLimitPerMonth = 0.60;
    private const double ImportShortageThreshold = 0.85;
    private const int LinkInactivityMonthsForCollapse = 18;
    private const double AnnualDependencyThreshold = 0.25;
    private const double NotableLinkStartQuantity = 8.0;

    private readonly Dictionary<string, TradeLink> _links = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<(int exporterId, int importerId)> _pairsUsedThisMonth = new();
    private readonly Dictionary<int, HashSet<int>> _partnersByPolityThisYear = new();
    private readonly HashSet<int> _dependencyEventRaisedThisYear = new();
    private int _partnerTrackingYear = int.MinValue;

    public void UpdateTrade(World world)
    {
        _pairsUsedThisMonth.Clear();
        EnsurePartnerBuckets(world);

        List<Polity> activePolities = world.Polities
            .Where(polity => polity.Population > 0)
            .ToList();

        List<Polity> importers = activePolities
            .Where(IsFoodImporter)
            .OrderByDescending(EstimateImportNeed)
            .ToList();

        if (importers.Count == 0)
        {
            return;
        }

        List<Polity> exporters = activePolities
            .Where(IsFoodExporter)
            .OrderByDescending(EstimateExportCapacity)
            .ToList();

        if (exporters.Count == 0)
        {
            return;
        }

        foreach (Polity importer in importers)
        {
            TrySatisfyImporter(world, importer, exporters);
        }
    }

    public void UpdateAnnualTrade(World world)
    {
        foreach (Polity polity in world.Polities.Where(p => p.Population > 0))
        {
            if (_dependencyEventRaisedThisYear.Contains(polity.Id))
            {
                continue;
            }

            if (polity.AnnualFoodConsumed <= 0)
            {
                continue;
            }

            double importShare = polity.AnnualFoodImported / polity.AnnualFoodConsumed;
            if (importShare < AnnualDependencyThreshold || polity.TradeReliefMonthsThisYear < 3)
            {
                continue;
            }

            _dependencyEventRaisedThisYear.Add(polity.Id);

            Region region = world.Regions.First(r => r.Id == polity.RegionId);
            world.AddEvent(
                WorldEventType.TradeDependency,
                WorldEventSeverity.Notable,
                $"{polity.Name} survived the year through imported food",
                $"{polity.Name} consumed {polity.AnnualFoodImported:F1} imported food ({importShare:P0} of annual consumption).",
                reason: "annual_trade_dependency",
                polityId: polity.Id,
                polityName: polity.Name,
                regionId: region.Id,
                regionName: region.Name,
                after: new Dictionary<string, string>
                {
                    ["annualFoodImported"] = polity.AnnualFoodImported.ToString("F1"),
                    ["annualImportShare"] = importShare.ToString("F2")
                });
        }

        PruneInactiveLinksAndEmitCollapses(world);
        _dependencyEventRaisedThisYear.Clear();
    }

    private void TrySatisfyImporter(World world, Polity importer, IReadOnlyList<Polity> allExporters)
    {
        double remainingNeed = EstimateImportNeed(importer);
        if (remainingNeed <= 0)
        {
            return;
        }

        List<Polity> candidateExporters = allExporters
            .Where(exporter => exporter.Id != importer.Id)
            .Where(exporter => EstimateExportCapacity(exporter) > 0)
            .Where(exporter => AreTradeReachable(world, exporter.RegionId, importer.RegionId))
            .OrderBy(exporter => RegionDistancePriority(world, exporter.RegionId, importer.RegionId))
            .ThenByDescending(exporter => exporter.SpeciesId == importer.SpeciesId ? 1 : 0)
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

            bool importerWasInShortage = importer.FoodStores < importer.FoodNeededThisMonth;
            ExecuteTransfer(world, exporter, importer, transfer, importerWasInShortage);
            remainingNeed = EstimateImportNeed(importer);
        }
    }

    private void ExecuteTransfer(World world, Polity exporter, Polity importer, double quantity, bool importerWasInShortage)
    {
        Region importerRegion = world.Regions.First(region => region.Id == importer.RegionId);

        double exporterBefore = exporter.FoodStores;
        double importerBefore = importer.FoodStores;

        exporter.FoodStores -= quantity;
        importer.FoodStores += quantity;

        exporter.AnnualFoodExported += quantity;
        importer.AnnualFoodImported += quantity;

        RegisterPartner(exporter.Id, importer.Id);
        RegisterPartner(importer.Id, exporter.Id);

        exporter.TradePartnerCountThisYear = _partnersByPolityThisYear[exporter.Id].Count;
        importer.TradePartnerCountThisYear = _partnersByPolityThisYear[importer.Id].Count;

        TradeLink link = GetOrCreateLink(world, exporter, importer, quantity);
        link.AgeMonths++;
        link.LastActiveTick = world.Time.Tick;

        world.AddEvent(
            WorldEventType.TradeTransfer,
            WorldEventSeverity.Normal,
            $"{exporter.Name} sent food to {importer.Name}",
            $"{exporter.Name} transferred {quantity:F1} food to {importer.Name}.",
            reason: "monthly_trade_transfer",
            polityId: exporter.Id,
            polityName: exporter.Name,
            relatedPolityId: importer.Id,
            relatedPolityName: importer.Name,
            regionId: importer.RegionId,
            regionName: importerRegion.Name,
            before: new Dictionary<string, string>
            {
                ["exporterFoodStores"] = exporterBefore.ToString("F1"),
                ["importerFoodStores"] = importerBefore.ToString("F1")
            },
            after: new Dictionary<string, string>
            {
                ["exporterFoodStores"] = exporter.FoodStores.ToString("F1"),
                ["importerFoodStores"] = importer.FoodStores.ToString("F1")
            },
            metadata: new Dictionary<string, string>
            {
                ["resource"] = TradeResourceType.Food.ToString(),
                ["quantity"] = quantity.ToString("F1"),
                ["tradeLinkAgeMonths"] = link.AgeMonths.ToString()
            });

        bool importerRecoveredThisMonth = importerWasInShortage && importer.FoodStores >= importer.FoodNeededThisMonth;
        if (importerRecoveredThisMonth)
        {
            importer.TradeReliefMonthsThisYear++;

            world.AddEvent(
                WorldEventType.TradeRelief,
                WorldEventSeverity.Notable,
                $"{importer.Name} stabilized through imported food",
                $"{importer.Name} recovered from immediate shortage after receiving {quantity:F1} food from {exporter.Name}.",
                reason: "trade_shortage_relief",
                polityId: importer.Id,
                polityName: importer.Name,
                relatedPolityId: exporter.Id,
                relatedPolityName: exporter.Name,
                regionId: importerRegion.Id,
                regionName: importerRegion.Name,
                metadata: new Dictionary<string, string>
                {
                    ["resource"] = TradeResourceType.Food.ToString(),
                    ["quantity"] = quantity.ToString("F1")
                });
        }
    }

    private TradeLink GetOrCreateLink(World world, Polity exporter, Polity importer, double initialQuantity)
    {
        string key = $"{exporter.Id}:{importer.Id}:{TradeResourceType.Food}";
        if (_links.TryGetValue(key, out TradeLink? existing))
        {
            return existing;
        }

        TradeLink created = new(exporter.Id, importer.Id, TradeResourceType.Food, world.Time.Tick);
        _links[key] = created;

        Region exporterRegion = world.Regions.First(region => region.Id == exporter.RegionId);
        Region importerRegion = world.Regions.First(region => region.Id == importer.RegionId);

        world.AddEvent(
            WorldEventType.TradeLinkStarted,
            initialQuantity >= NotableLinkStartQuantity ? WorldEventSeverity.Notable : WorldEventSeverity.Normal,
            $"{exporter.Name} began trading food with {importer.Name}",
            $"{exporter.Name} established a food trade link to {importer.Name}.",
            reason: "trade_link_created",
            polityId: exporter.Id,
            polityName: exporter.Name,
            relatedPolityId: importer.Id,
            relatedPolityName: importer.Name,
            regionId: exporterRegion.Id,
            regionName: exporterRegion.Name,
            metadata: new Dictionary<string, string>
            {
                ["resource"] = TradeResourceType.Food.ToString(),
                ["importerRegion"] = importerRegion.Name
            });

        return created;
    }

    private void PruneInactiveLinksAndEmitCollapses(World world)
    {
        if (_links.Count == 0)
        {
            return;
        }

        List<string> staleKeys = _links
            .Where(entry => (world.Time.Tick - entry.Value.LastActiveTick) >= LinkInactivityMonthsForCollapse)
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
                world.AddEvent(
                    WorldEventType.TradeLinkCollapsed,
                    WorldEventSeverity.Notable,
                    $"Trade between {exporter.Name} and {importer.Name} fell silent",
                    $"Food trade link between {exporter.Name} and {importer.Name} went inactive after {staleLink.AgeMonths} months of activity.",
                    reason: "trade_link_inactive",
                    polityId: exporter.Id,
                    polityName: exporter.Name,
                    relatedPolityId: importer.Id,
                    relatedPolityName: importer.Name,
                    metadata: new Dictionary<string, string>
                    {
                        ["resource"] = staleLink.ResourceType.ToString(),
                        ["ageMonths"] = staleLink.AgeMonths.ToString()
                    });
            }

            _links.Remove(staleKey);
        }
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

    private static bool AreTradeReachable(World world, int exporterRegionId, int importerRegionId)
    {
        if (exporterRegionId == importerRegionId)
        {
            return true;
        }

        Region exporterRegion = world.Regions.First(region => region.Id == exporterRegionId);
        return exporterRegion.ConnectedRegionIds.Contains(importerRegionId);
    }

    private static int RegionDistancePriority(World world, int sourceRegionId, int targetRegionId)
    {
        if (sourceRegionId == targetRegionId)
        {
            return 0;
        }

        Region source = world.Regions.First(region => region.Id == sourceRegionId);
        return source.ConnectedRegionIds.Contains(targetRegionId) ? 1 : 99;
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
}
