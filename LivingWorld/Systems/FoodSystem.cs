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

        double animalSeasonMultiplier = world.Time.Season switch
        {
            Season.Winter => 0.60,
            Season.Spring => 0.90,
            Season.Summer => 1.00,
            _ => 0.85
        };

        foreach (Region region in world.Regions)
        {
            double plantGrowth =
                ((region.Fertility * 20.0) + (region.WaterAvailability * 15.0))
                * plantSeasonMultiplier;

            double animalGrowth =
                ((region.Fertility * 6.0) + (region.WaterAvailability * 4.0))
                * animalSeasonMultiplier;

            region.PlantBiomass = Math.Min(region.MaxPlantBiomass, region.PlantBiomass + plantGrowth);
            region.AnimalBiomass = Math.Min(region.MaxAnimalBiomass, region.AnimalBiomass + animalGrowth);
        }
    }

    public void GatherFood(World world)
    {
        foreach (Polity polity in world.Polities)
        {
            if (polity.Population <= 0)
            {
                polity.FoodGatheredThisMonth = 0;
                polity.FoodConsumedThisMonth = 0;
                polity.FoodNeededThisMonth = 0;
                polity.FoodShortageThisMonth = 0;
                polity.FoodSurplusThisMonth = 0;
                polity.FoodSatisfactionThisMonth = 1.0;
                continue;
            }

            polity.FoodGatheredThisMonth = 0;
            polity.FoodConsumedThisMonth = 0;
            polity.FoodShortageThisMonth = 0;
            polity.FoodSurplusThisMonth = 0;
            polity.FoodNeededThisMonth = polity.Population * polity.Capabilities.FoodNeedMultiplier;
        }

        foreach (Region region in world.Regions)
        {
            List<Polity> localPolities = world.Polities
                .Where(p => p.RegionId == region.Id && p.Population > 0)
                .ToList();

            if (localPolities.Count == 0)
            {
                continue;
            }

            Dictionary<int, double> plantDemand = localPolities.ToDictionary(
                p => p.Id,
                p => p.Population * 0.85 * p.Capabilities.HarvestEfficiencyMultiplier);

            Dictionary<int, double> animalDemand = localPolities.ToDictionary(
                p => p.Id,
                p => p.Population * 0.35 * p.Capabilities.HarvestEfficiencyMultiplier);

            double totalPlantDemand = plantDemand.Values.Sum();
            double totalAnimalDemand = animalDemand.Values.Sum();

            double startingPlantBiomass = region.PlantBiomass;
            double startingAnimalBiomass = region.AnimalBiomass;

            double actualPlantHarvest = 0;
            double actualAnimalHarvest = 0;

            foreach (Polity polity in localPolities)
            {
                double plantShare = totalPlantDemand <= 0
                    ? 0
                    : startingPlantBiomass * (plantDemand[polity.Id] / totalPlantDemand);

                double animalShare = totalAnimalDemand <= 0
                    ? 0
                    : startingAnimalBiomass * (animalDemand[polity.Id] / totalAnimalDemand);

                double gatheredPlants = Math.Min(plantShare, plantDemand[polity.Id]);
                double gatheredAnimals = Math.Min(animalShare, animalDemand[polity.Id]);

                double totalFood = gatheredPlants + gatheredAnimals;
                totalFood += CalculateFarmYield(polity, region, world.Time.Season);

                polity.FoodGatheredThisMonth += totalFood;
                polity.FoodStores += totalFood;

                actualPlantHarvest += gatheredPlants;
                actualAnimalHarvest += gatheredAnimals;
            }

            region.PlantBiomass = Math.Max(0, region.PlantBiomass - actualPlantHarvest);
            region.AnimalBiomass = Math.Max(0, region.AnimalBiomass - actualAnimalHarvest);
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

    private static double CalculateFarmYield(Polity polity, Region region, Season season)
    {
        if (!polity.Capabilities.CanFarm || polity.Population <= 0)
        {
            return 0.0;
        }

        double farmlandQuality = (region.Fertility * 0.70) + (region.WaterAvailability * 0.30);
        double seasonalFactor = GetFarmSeasonalFactor(season);

        return polity.Population
            * polity.Capabilities.FarmingYieldPerPerson
            * farmlandQuality
            * seasonalFactor;
    }

    private static double GetFarmSeasonalFactor(Season season)
        => season switch
        {
            Season.Winter => 0.25,
            Season.Spring => 1.20,
            Season.Summer => 1.00,
            _ => 0.75
        };
}
