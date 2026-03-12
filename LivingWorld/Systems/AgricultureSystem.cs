using LivingWorld.Core;
using LivingWorld.Map;
using LivingWorld.Life;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class AgricultureSystem
{
    private const double BaseRegionalArableFactor = 0.60;
    private const double BaseYieldPerCultivatedLand = 1.35;
    private readonly DomesticationSystem? _domesticationSystem;

    public AgricultureSystem(DomesticationSystem? domesticationSystem = null)
    {
        _domesticationSystem = domesticationSystem;
    }

    public void ProduceFarmFood(World world)
    {
        WorldLookup lookup = new(world);

        foreach (Polity polity in world.Polities)
        {
            polity.CultivatedLand = polity.Settlements.Sum(settlement => settlement.CultivatedLand);
        }

        foreach (Region region in world.Regions)
        {
            double regionalArableCapacity = CalculateRegionalArableCapacity(region);
            IReadOnlyList<Settlement> localSettlements = lookup.GetSettlementsInRegion(region.Id)
                .Where(settlement => lookup.TryGetPolity(settlement.PolityId, out Polity? owner)
                    && owner is not null
                    && owner.Population > 0
                    && CanFarmInSettlementContext(owner))
                .ToList();

            if (localSettlements.Count == 0 || regionalArableCapacity <= 0)
            {
                continue;
            }

            Dictionary<int, Polity> ownersBySettlementId = localSettlements.ToDictionary(
                settlement => settlement.Id,
                settlement => lookup.GetRequiredPolity(settlement.PolityId, "Agriculture settlement owner"));
            Dictionary<int, int> settlementCountsByPolity = localSettlements
                .GroupBy(settlement => settlement.PolityId)
                .ToDictionary(group => group.Key, group => group.Count());
            Dictionary<int, double> desiredCultivation = localSettlements.ToDictionary(
                settlement => settlement.Id,
                settlement => CalculateDesiredCultivation(ownersBySettlementId[settlement.Id], settlementCountsByPolity[settlement.PolityId]));

            double totalDesired = desiredCultivation.Values.Sum();
            if (totalDesired <= 0)
            {
                continue;
            }

            foreach (Settlement settlement in localSettlements)
            {
                Polity polity = ownersBySettlementId[settlement.Id];
                double share = desiredCultivation[settlement.Id] / totalDesired;
                double allocatedLand = regionalArableCapacity * share;

                UpdateCultivation(polity, settlement, allocatedLand);

                double monthlyYield = CalculateSeasonalYield(polity, region, settlement, world.Time.Season, _domesticationSystem);
                if (monthlyYield <= 0)
                {
                    continue;
                }

                double managedCropBonus = _domesticationSystem?.GetCropManagedFoodBonus(settlement, monthlyYield) ?? 0.0;
                polity.FoodFarmedThisMonth += monthlyYield;
                polity.AnnualFoodFarmed += monthlyYield;
                polity.FoodManagedThisMonth += managedCropBonus;
                polity.AnnualFoodManaged += managedCropBonus;
                polity.AnnualCultivatedLandTotal += settlement.CultivatedLand;
                polity.FarmingMonthsThisYear++;
                polity.FoodStores += monthlyYield + managedCropBonus;
                settlement.ManagedCropFoodThisMonth += managedCropBonus;
                settlement.ManagedFoodThisYear += managedCropBonus;
            }
        }

        foreach (Polity polity in world.Polities)
        {
            polity.CultivatedLand = polity.Settlements.Sum(settlement => settlement.CultivatedLand);
        }
    }

    public void UpdateAnnualAgriculture(World world)
    {
        if (world.Time.Month != 12)
        {
            return;
        }

        WorldLookup lookup = new(world);

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

            Region region = lookup.GetRequiredRegion(polity.RegionId, "Annual agriculture");
            Species species = lookup.GetRequiredSpecies(polity.SpeciesId, "Annual agriculture");

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
                    speciesId: polity.SpeciesId,
                    speciesName: species.Name,
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
                    speciesId: polity.SpeciesId,
                    speciesName: species.Name,
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
                        speciesId: polity.SpeciesId,
                        speciesName: species.Name,
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
                        speciesId: polity.SpeciesId,
                        speciesName: species.Name,
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

    private static double CalculateDesiredCultivation(Polity polity, int settlementCount)
    {
        double settlementLaborShare = polity.SettlementStatus switch
        {
            SettlementStatus.Settled => 0.58,
            SettlementStatus.SemiSettled => 0.40,
            _ => 0.0
        };

        double knowledgeScale = 1.0 + (polity.Capabilities.FarmingYieldPerPerson * 0.80);
        double localPopulation = settlementCount <= 0
            ? 0.0
            : polity.Population / (double)settlementCount;
        double laborDrivenTarget = localPopulation * settlementLaborShare * knowledgeScale;

        return Math.Max(0, laborDrivenTarget);
    }

    private static void UpdateCultivation(Polity polity, Settlement settlement, double targetCultivation)
    {
        if (targetCultivation <= 0)
        {
            settlement.CultivatedLand *= 0.60;
            return;
        }

        double expansionRate = polity.SettlementStatus switch
        {
            SettlementStatus.Settled => 0.16,
            SettlementStatus.SemiSettled => 0.09,
            _ => 0.0
        };

        expansionRate += Math.Clamp(polity.YearsSinceFirstSettlement / 40.0, 0.0, 0.04);

        if (settlement.CultivatedLand < targetCultivation)
        {
            settlement.CultivatedLand += (targetCultivation - settlement.CultivatedLand) * expansionRate;
        }
        else
        {
            settlement.CultivatedLand -= (settlement.CultivatedLand - targetCultivation) * 0.30;
        }

        settlement.CultivatedLand = Math.Max(0, settlement.CultivatedLand);
    }

    private static double CalculateSeasonalYield(Polity polity, Region region, Settlement settlement, Season season, DomesticationSystem? domesticationSystem)
    {
        double cultivatedLand = settlement.CultivatedLand;
        if (cultivatedLand <= 0)
        {
            return 0;
        }

        double soilWaterQuality = (region.Fertility * 0.72) + (region.WaterAvailability * 0.28);
        double seasonalFactor = season switch
        {
            Season.Winter => 0.12 + (domesticationSystem?.GetCropSeasonalResilience(settlement) ?? 0.0),
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
