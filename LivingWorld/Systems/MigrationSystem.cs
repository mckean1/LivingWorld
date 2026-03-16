using LivingWorld.Core;
using LivingWorld.Map;
using LivingWorld.Life;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class MigrationSystem
{
    private readonly Random _random;

    public MigrationSystem(int seed = 12345)
    {
        _random = new Random(seed);
    }

    public void UpdateMigration(World world)
    {
        WorldLookup lookup = new(world);

        foreach (Polity polity in world.Polities)
        {
            if (polity.Population <= 0)
                continue;

            Region currentRegion = lookup.GetRequiredRegion(polity.RegionId, "Migration update");
            Species species = lookup.GetRequiredSpecies(polity.SpeciesId, "Migration update");

            polity.MigrationPressure = CalculateMigrationPressure(lookup, polity, currentRegion);

            if (polity.MigrationPressure >= 0.65)
            {
                Region? target = FindBestConnectedRegion(lookup, polity, currentRegion);

                if (target is not null && target.Id != currentRegion.Id)
                {
                    polity.PreviousRegionId = polity.RegionId;
                    polity.RegionId = target.Id;
                    if (polity.HasSettlements)
                    {
                        polity.RelocateSettlements(target.Id, index => index == 0
                            ? $"{target.Name} Hearth"
                            : $"{target.Name} Outpost {index + 1}");
                    }

                    polity.MovedThisMonth = true;
                    polity.MovedThisYear = true;
                    polity.MovesThisYear++;
                    double moveCostRate = Math.Clamp(0.25 * polity.Capabilities.TravelCostMultiplier, 0.05, 0.90);
                    polity.FoodStores *= 1.0 - moveCostRate;

                    world.AddEvent(
                        WorldEventType.Migration,
                        WorldEventSeverity.Major,
                        $"{polity.Name} migrated to {target.Name}",
                        $"{polity.Name} migrated from Region {currentRegion.Id} to Region {target.Id}.",
                        reason: "migration_pressure",
                        scope: WorldEventScope.Regional,
                        polityId: polity.Id,
                        polityName: polity.Name,
                        speciesId: polity.SpeciesId,
                        speciesName: species.Name,
                        regionId: target.Id,
                        regionName: target.Name,
                        before: new Dictionary<string, string>
                        {
                            ["regionId"] = currentRegion.Id.ToString(),
                            ["foodStores"] = (polity.FoodStores / (1.0 - moveCostRate)).ToString("F1")
                        },
                        after: new Dictionary<string, string>
                        {
                            ["regionId"] = target.Id.ToString(),
                            ["foodStores"] = polity.FoodStores.ToString("F1")
                        },
                        metadata: new Dictionary<string, string>
                        {
                            ["migrationPressure"] = polity.MigrationPressure.ToString("F2"),
                            ["moveCostRate"] = moveCostRate.ToString("F2")
                        }
                    );
                }
            }
        }
    }

    private double CalculateMigrationPressure(WorldLookup lookup, Polity polity, Region region)
    {
        double monthlyFoodNeed = Math.Max(1, polity.Population);
        double foodSafety = Math.Min(1.0, polity.FoodStores / monthlyFoodNeed);

        double ecologyRatio = region.TotalBiomassCapacity <= 0
            ? 0
            : region.TotalBiomass / region.TotalBiomassCapacity;

        int localPopulation = lookup.GetActivePopulationInRegion(region.Id);

        double crowdingRatio = region.CarryingCapacity <= 0
            ? 1.0
            : localPopulation / region.CarryingCapacity;

        double shortagePressure = 1.0 - polity.FoodSatisfactionThisMonth;
        double foodStorePressure = 1.0 - foodSafety;
        double ecologyPressure = 1.0 - ecologyRatio;
        double crowdingPressure = Math.Clamp(crowdingRatio - 1.0, 0.0, 1.0);
        double settlementAnchor = polity.SettlementStatus switch
        {
            SettlementStatus.Settled => 0.18,
            SettlementStatus.SemiSettled => 0.08,
            _ => 0.0
        };
        double durableSettlementGravity = ResolveDurableSettlementGravity(polity);
        double unsupportedSprawlPressure = ResolveUnsupportedSprawlPressure(polity);

        double pressure =
            (shortagePressure * 0.40) +
            (foodStorePressure * 0.25) +
            (ecologyPressure * 0.20) +
            (crowdingPressure * 0.15) -
            settlementAnchor -
            durableSettlementGravity +
            unsupportedSprawlPressure;

        pressure += polity.EventDrivenMigrationPressureBonus;

        return Math.Clamp(pressure, 0.0, 1.0);
    }

    private Region? FindBestConnectedRegion(WorldLookup lookup, Polity polity, Region currentRegion)
    {
        if (currentRegion.ConnectedRegionIds.Count == 0)
            return null;

        Region? bestRegion = null;
        double bestScore = double.MinValue;
        int preferredCoreRegionId = ResolvePreferredCoreRegionId(polity);
        double durableSettlementGravity = ResolveDurableSettlementGravity(polity);

        foreach (int regionId in currentRegion.ConnectedRegionIds)
        {
            if (!lookup.TryGetRegion(regionId, out Region? region) || region is null)
            {
                continue;
            }

            int localPopulation = lookup.GetActivePopulationInRegion(region.Id);

            double crowdingPenalty = localPopulation * 0.7;
            int hopDistanceFromCore = ComputeMinimumSettlementHopDistance(lookup, polity, region.Id);
            double returnBias = region.Id == polity.PreviousRegionId
                ? 140.0 * (0.25 + durableSettlementGravity)
                : 0.0;
            double coreBias = region.Id == preferredCoreRegionId
                ? 120.0 * (0.20 + durableSettlementGravity)
                : 0.0;
            double corridorBias = hopDistanceFromCore <= 1
                ? 65.0 * (0.20 + durableSettlementGravity)
                : 0.0;
            double antiSprawlPenalty = Math.Max(0, hopDistanceFromCore - 1) * 85.0 * (0.15 + durableSettlementGravity);

            double score =
                region.PlantBiomass +
                region.AnimalBiomass +
                (region.Fertility * 200) +
                (region.WaterAvailability * 150) +
                (region.CarryingCapacity * 2.0) -
                crowdingPenalty +
                returnBias +
                coreBias +
                corridorBias -
                antiSprawlPenalty;

            // Small randomness so ties don't always pick the same region
            score += _random.NextDouble() * 10.0;

            if (score > bestScore)
            {
                bestScore = score;
                bestRegion = region;
            }
        }

        return bestRegion;
    }

    private static double ResolveDurableSettlementGravity(Polity polity)
    {
        if (!polity.HasSettlements)
        {
            return 0.0;
        }

        double averageSettlementAgeMonths = polity.Settlements.Average(settlement => settlement.EstablishedMonths);
        double oldestSettlementAgeMonths = polity.Settlements.Max(settlement => settlement.EstablishedMonths);
        double dominantClusterShare = polity.Settlements
            .GroupBy(settlement => settlement.RegionId)
            .Select(group => group.Count() / (double)polity.SettlementCount)
            .DefaultIfEmpty(1.0)
            .Max();
        double gravity = Math.Clamp(averageSettlementAgeMonths / 48.0, 0.0, 0.12)
            + Math.Clamp(oldestSettlementAgeMonths / 72.0, 0.0, 0.08)
            + Math.Max(0.0, dominantClusterShare - 0.45) * 0.10
            + (polity.Stage >= PolityStage.Tribe ? 0.04 : 0.0);
        return Math.Clamp(gravity, 0.0, 0.22);
    }

    private static double ResolveUnsupportedSprawlPressure(Polity polity)
    {
        if (!polity.HasSettlements)
        {
            return 0.0;
        }

        int distinctSettlementRegions = polity.Settlements.Select(settlement => settlement.RegionId).Distinct().Count();
        double weakSettlementShare = polity.Settlements.Count(settlement => settlement.FoodState is FoodState.Deficit or FoodState.Starving)
            / Math.Max(1.0, polity.SettlementCount);
        double sprawl = Math.Max(0, distinctSettlementRegions - 2) * 0.03;
        return Math.Clamp(sprawl + (weakSettlementShare * 0.06), 0.0, 0.16);
    }

    private static int ResolvePreferredCoreRegionId(Polity polity)
        => !polity.HasSettlements
            ? polity.RegionId
            : polity.Settlements
                .GroupBy(settlement => settlement.RegionId)
                .Select(group => new
                {
                    RegionId = group.Key,
                    Count = group.Count(),
                    OldestSettlementAgeMonths = group.Max(settlement => settlement.EstablishedMonths)
                })
                .OrderByDescending(entry => entry.Count)
                .ThenByDescending(entry => entry.OldestSettlementAgeMonths)
                .ThenBy(entry => entry.RegionId)
                .First()
                .RegionId;

    private static int ComputeMinimumSettlementHopDistance(WorldLookup lookup, Polity polity, int targetRegionId)
    {
        if (!polity.HasSettlements)
        {
            return targetRegionId == polity.RegionId ? 0 : 1;
        }

        int minimumDistance = int.MaxValue;
        foreach (int settlementRegionId in polity.Settlements.Select(settlement => settlement.RegionId).Distinct())
        {
            int distance = ComputeHopDistance(lookup, settlementRegionId, targetRegionId);
            minimumDistance = Math.Min(minimumDistance, distance);
        }

        return minimumDistance;
    }

    private static int ComputeHopDistance(WorldLookup lookup, int sourceRegionId, int targetRegionId)
    {
        if (sourceRegionId == targetRegionId)
        {
            return 0;
        }

        Queue<(int RegionId, int Depth)> frontier = new();
        HashSet<int> visited = [sourceRegionId];
        frontier.Enqueue((sourceRegionId, 0));

        while (frontier.Count > 0)
        {
            (int regionId, int depth) = frontier.Dequeue();
            if (!lookup.TryGetRegion(regionId, out Region? region) || region is null)
            {
                continue;
            }

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

                frontier.Enqueue((neighborId, nextDepth));
            }
        }

        return int.MaxValue;
    }
}
