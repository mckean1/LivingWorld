using LivingWorld.Core;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class AgricultureSystem
{
    private const double BaseRegionalArableFactor = 0.60;
    private const double BaseYieldPerCultivatedLand = 1.35;

    public void ProduceFarmFood(World world)
    {
        foreach (Region region in world.Regions)
        {
            List<Polity> farmers = world.Polities
                .Where(polity => polity.Population > 0)
                .Where(polity => polity.RegionId == region.Id)
                .Where(CanFarmInSettlementContext)
                .ToList();

            if (farmers.Count == 0)
            {
                continue;
            }

            double regionalArableCapacity = CalculateRegionalArableCapacity(region);
            Dictionary<int, double> desiredCultivation = farmers.ToDictionary(
                polity => polity.Id,
                polity => CalculateDesiredCultivation(polity));

            double totalDesired = desiredCultivation.Values.Sum();
            if (totalDesired <= 0 || regionalArableCapacity <= 0)
            {
                continue;
            }

            foreach (Polity polity in farmers)
            {
                double share = desiredCultivation[polity.Id] / totalDesired;
                double allocatedLand = regionalArableCapacity * share;

                UpdateCultivation(polity, allocatedLand);

                double monthlyYield = CalculateSeasonalYield(polity, region, polity.CultivatedLand, world.Time.Season);
                if (monthlyYield <= 0)
                {
                    continue;
                }

                polity.FoodFarmedThisMonth += monthlyYield;
                polity.AnnualFoodFarmed += monthlyYield;
                polity.AnnualCultivatedLandTotal += polity.CultivatedLand;
                polity.FarmingMonthsThisYear++;
                polity.FoodStores += monthlyYield;
            }
        }
    }

    public void UpdateAnnualAgriculture(World world)
    {
        if (world.Time.Month != 12)
        {
            return;
        }

        foreach (Polity polity in world.Polities.Where(polity => polity.Population > 0))
        {
            if (polity.AgricultureEventCooldownYears > 0)
            {
                polity.AgricultureEventCooldownYears--;
            }

            double annualProduced = polity.AnnualFoodFarmed;
            double totalProduced = polity.AnnualFoodFarmed + polity.AnnualFoodGathered;
            double farmShare = totalProduced <= 0 ? 0 : polity.AnnualFoodFarmed / totalProduced;
            double annualFoodRatio = polity.AnnualFoodNeeded <= 0
                ? 1.0
                : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;
            double averageCultivatedLand = polity.FarmingMonthsThisYear == 0
                ? 0
                : polity.AnnualCultivatedLandTotal / 12.0;

            if (annualProduced <= 0)
            {
                polity.ConsecutiveFarmingYears = 0;
                polity.LastYearAverageCultivatedLand = 0;
                polity.CultivatedLand *= 0.75;
                continue;
            }

            Region region = world.Regions.First(region => region.Id == polity.RegionId);

            if (polity.ConsecutiveFarmingYears == 0)
            {
                world.AddEvent(
                    WorldEventType.CultivationExpanded,
                    WorldEventSeverity.Notable,
                    $"{polity.Name} established fields in {region.Name}",
                    $"{polity.Name} produced {annualProduced:F0} farm food in {region.Name}.",
                    reason: "first_viable_farming_year",
                    scope: WorldEventScope.Local,
                    polityId: polity.Id,
                    polityName: polity.Name,
                    regionId: region.Id,
                    regionName: region.Name,
                    parentEventIds: polity.LastLearnedAgricultureEventId is long eventId ? [eventId] : null,
                    metadata: new Dictionary<string, string>
                    {
                        ["annualFarmFood"] = annualProduced.ToString("F0")
                    });
            }
            else if (polity.AgricultureEventCooldownYears == 0
                && averageCultivatedLand >= Math.Max(6.0, polity.LastYearAverageCultivatedLand * 1.20))
            {
                world.AddEvent(
                    WorldEventType.CultivationExpanded,
                    WorldEventSeverity.Minor,
                    $"{polity.Name} expanded cultivation in {region.Name}",
                    $"{polity.Name} expanded cultivated land from {polity.LastYearAverageCultivatedLand:F1} to {averageCultivatedLand:F1}.",
                    reason: "cultivation_growth",
                    scope: WorldEventScope.Local,
                    polityId: polity.Id,
                    polityName: polity.Name,
                    regionId: region.Id,
                    regionName: region.Name,
                    parentEventIds: polity.LastLearnedAgricultureEventId is long growthEventId ? [growthEventId] : null,
                    before: new Dictionary<string, string>
                    {
                        ["averageCultivatedLand"] = polity.LastYearAverageCultivatedLand.ToString("F1")
                    },
                    after: new Dictionary<string, string>
                    {
                        ["averageCultivatedLand"] = averageCultivatedLand.ToString("F1")
                    });
                polity.AgricultureEventCooldownYears = 4;
            }

            if (polity.AgricultureEventCooldownYears == 0)
            {
                if (farmShare >= 0.65 && annualFoodRatio >= 1.05 && annualProduced >= polity.Population * 5.0)
                {
                    world.AddEvent(
                        WorldEventType.FoodStabilized,
                        WorldEventSeverity.Notable,
                        $"{polity.Name} gained stable harvests",
                        $"{polity.Name} farm share reached {farmShare:P0} with annual food ratio {annualFoodRatio:F2}.",
                        reason: "strong_harvest",
                        scope: WorldEventScope.Polity,
                        polityId: polity.Id,
                        polityName: polity.Name,
                        regionId: region.Id,
                        regionName: region.Name,
                        parentEventIds: polity.LastLearnedAgricultureEventId is long stabilityEventId ? [stabilityEventId] : null,
                        after: new Dictionary<string, string>
                        {
                            ["farmShare"] = farmShare.ToString("F2"),
                            ["annualFoodRatio"] = annualFoodRatio.ToString("F2")
                        });
                    polity.AgricultureEventCooldownYears = 8;
                }
                else if (farmShare >= 0.30 && annualFoodRatio < 0.85)
                {
                    world.AddEvent(
                        WorldEventType.Harvest,
                        WorldEventSeverity.Notable,
                        $"{polity.Name} suffered a poor harvest",
                        $"{polity.Name} farm share was {farmShare:P0} with annual food ratio {annualFoodRatio:F2}.",
                        reason: "poor_harvest",
                        scope: WorldEventScope.Local,
                        polityId: polity.Id,
                        polityName: polity.Name,
                        regionId: region.Id,
                        regionName: region.Name,
                        after: new Dictionary<string, string>
                        {
                            ["farmShare"] = farmShare.ToString("F2"),
                            ["annualFoodRatio"] = annualFoodRatio.ToString("F2")
                        });
                    polity.AgricultureEventCooldownYears = 8;
                }
            }

            polity.ConsecutiveFarmingYears++;
            polity.LastYearAverageCultivatedLand = averageCultivatedLand;
        }
    }

    private static bool CanFarmInSettlementContext(Polity polity)
        => polity.Capabilities.CanFarm
            && polity.HasSettlements
            && polity.SettlementStatus != SettlementStatus.Nomadic;

    private static double CalculateRegionalArableCapacity(Region region)
    {
        double quality = (region.Fertility * 0.70) + (region.WaterAvailability * 0.30);
        return region.CarryingCapacity * quality * BaseRegionalArableFactor;
    }

    private static double CalculateDesiredCultivation(Polity polity)
    {
        double settlementLaborShare = polity.SettlementStatus switch
        {
            SettlementStatus.Settled => 0.58,
            SettlementStatus.SemiSettled => 0.40,
            _ => 0.0
        };

        double settlementScale = 1.0 + (Math.Max(0, polity.SettlementCount - 1) * 0.18);
        double knowledgeScale = 1.0 + (polity.Capabilities.FarmingYieldPerPerson * 0.80);
        double laborDrivenTarget = polity.Population * settlementLaborShare * settlementScale * knowledgeScale;

        return Math.Max(0, laborDrivenTarget);
    }

    private static void UpdateCultivation(Polity polity, double targetCultivation)
    {
        if (targetCultivation <= 0)
        {
            polity.CultivatedLand *= 0.60;
            return;
        }

        double expansionRate = polity.SettlementStatus switch
        {
            SettlementStatus.Settled => 0.16,
            SettlementStatus.SemiSettled => 0.09,
            _ => 0.0
        };

        expansionRate += Math.Clamp(polity.YearsSinceFirstSettlement / 40.0, 0.0, 0.04);

        if (polity.CultivatedLand < targetCultivation)
        {
            polity.CultivatedLand += (targetCultivation - polity.CultivatedLand) * expansionRate;
        }
        else
        {
            polity.CultivatedLand -= (polity.CultivatedLand - targetCultivation) * 0.30;
        }

        polity.CultivatedLand = Math.Max(0, polity.CultivatedLand);
    }

    private static double CalculateSeasonalYield(Polity polity, Region region, double cultivatedLand, Season season)
    {
        if (cultivatedLand <= 0)
        {
            return 0;
        }

        double soilWaterQuality = (region.Fertility * 0.72) + (region.WaterAvailability * 0.28);
        double seasonalFactor = season switch
        {
            Season.Winter => 0.12,
            Season.Spring => 0.82,
            Season.Summer => 1.36,
            _ => 1.02
        };

        double settlementEfficiency = polity.SettlementStatus switch
        {
            SettlementStatus.Settled => 1.00,
            SettlementStatus.SemiSettled => 0.72,
            _ => 0.0
        };

        double knowledgeEfficiency = 1.0 + (polity.Capabilities.FarmingYieldPerPerson * 1.4);

        return cultivatedLand
            * BaseYieldPerCultivatedLand
            * soilWaterQuality
            * seasonalFactor
            * settlementEfficiency
            * knowledgeEfficiency;
    }
}
