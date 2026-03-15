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

        double pressure =
            (shortagePressure * 0.40) +
            (foodStorePressure * 0.25) +
            (ecologyPressure * 0.20) +
            (crowdingPressure * 0.15) -
            settlementAnchor;

        pressure += polity.EventDrivenMigrationPressureBonus;

        return Math.Clamp(pressure, 0.0, 1.0);
    }

    private Region? FindBestConnectedRegion(WorldLookup lookup, Polity polity, Region currentRegion)
    {
        if (currentRegion.ConnectedRegionIds.Count == 0)
            return null;

        Region? bestRegion = null;
        double bestScore = double.MinValue;

        foreach (int regionId in currentRegion.ConnectedRegionIds)
        {
            if (!lookup.TryGetRegion(regionId, out Region? region) || region is null)
            {
                continue;
            }

            int localPopulation = lookup.GetActivePopulationInRegion(region.Id);

            double crowdingPenalty = localPopulation * 0.7;

            double score =
                region.PlantBiomass +
                region.AnimalBiomass +
                (region.Fertility * 200) +
                (region.WaterAvailability * 150) +
                (region.CarryingCapacity * 2.0) -
                crowdingPenalty;

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
}
