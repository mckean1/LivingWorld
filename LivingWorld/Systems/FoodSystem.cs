using LivingWorld.Core;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class FoodSystem
{
    private const double BaseSpoilageRate = 0.12;

    public void UpdateRegionEcology(World world)
    {
        double plantSeasonMultiplier = world.Time.Season switch
        {
            Season.Winter => 0.35,
            Season.Spring => 1.25,
            Season.Summer => 1.00,
            _ => 0.65
        };

        foreach (Region region in world.Regions)
        {
            double plantGrowth =
                ((region.Fertility * 20.0) + (region.WaterAvailability * 15.0))
                * plantSeasonMultiplier;

            region.PlantBiomass = Math.Min(region.MaxPlantBiomass, region.PlantBiomass + plantGrowth);
        }
    }

    public void GatherFood(World world)
    {
        WorldLookup lookup = new(world);

        foreach (Polity polity in world.Polities)
        {
            if (polity.Population <= 0)
            {
                polity.FoodGatheredThisMonth = 0;
                polity.FoodFarmedThisMonth = 0;
                polity.FoodConsumedThisMonth = 0;
                polity.FoodNeededThisMonth = 0;
                polity.FoodShortageThisMonth = 0;
                polity.FoodSurplusThisMonth = 0;
                polity.FoodSatisfactionThisMonth = 1.0;
                continue;
            }

            polity.FoodGatheredThisMonth = 0;
            polity.FoodFarmedThisMonth = 0;
            polity.FoodConsumedThisMonth = 0;
            polity.FoodShortageThisMonth = 0;
            polity.FoodSurplusThisMonth = 0;
            polity.FoodNeededThisMonth = polity.Population * polity.Capabilities.FoodNeedMultiplier;
        }

        foreach (Region region in world.Regions)
        {
            IReadOnlyList<Polity> localPolities = lookup.GetActivePolitiesInRegion(region.Id);

            if (localPolities.Count == 0)
            {
                continue;
            }

            Dictionary<int, double> plantDemand = localPolities.ToDictionary(
                p => p.Id,
                p => p.Population * 0.85 * p.Capabilities.HarvestEfficiencyMultiplier);

            double totalPlantDemand = plantDemand.Values.Sum();

            double startingPlantBiomass = region.PlantBiomass;

            double actualPlantHarvest = 0;

            foreach (Polity polity in localPolities)
            {
                // Generic monthly gathering is plant-foraging only. Animal food now
                // enters polity stores exclusively through species-level hunting.
                double plantShare = totalPlantDemand <= 0
                    ? 0
                    : startingPlantBiomass * (plantDemand[polity.Id] / totalPlantDemand);

                double gatheredPlants = Math.Min(plantShare, plantDemand[polity.Id]);

                polity.FoodGatheredThisMonth += gatheredPlants;
                polity.AnnualFoodGathered += gatheredPlants;
                polity.FoodStores += gatheredPlants;

                actualPlantHarvest += gatheredPlants;
            }

            region.PlantBiomass = Math.Max(0, region.PlantBiomass - actualPlantHarvest);
        }
    }

    public void ConsumeFood(World world)
    {
        foreach (Polity polity in world.Polities)
        {
            if (polity.Population <= 0)
            {
                continue;
            }

            double need = polity.FoodNeededThisMonth;
            double eaten = Math.Min(polity.FoodStores, need);
            double shortage = Math.Max(0, need - eaten);

            polity.FoodConsumedThisMonth = eaten;
            polity.FoodShortageThisMonth = shortage;

            polity.FoodStores -= eaten;

            double spoiled = polity.FoodStores * BaseSpoilageRate;
            spoiled *= polity.Capabilities.FoodSpoilageMultiplier;
            polity.FoodStores = Math.Max(0, polity.FoodStores - spoiled);

            polity.FoodSurplusThisMonth = polity.FoodStores;
            polity.FoodSatisfactionThisMonth = need <= 0 ? 1.0 : eaten / need;

            polity.AnnualFoodNeeded += need;
            polity.AnnualFoodConsumed += eaten;
            polity.AnnualFoodShortage += shortage;

            if (shortage > 0)
            {
                polity.StarvationMonthsThisYear++;
            }
        }
    }
}
