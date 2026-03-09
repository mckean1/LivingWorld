using LivingWorld.Core;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class FoodSystem
{
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

            Region region = world.Regions.First(r => r.Id == polity.RegionId);

            polity.FoodGatheredThisMonth = 0;
            polity.FoodConsumedThisMonth = 0;
            polity.FoodShortageThisMonth = 0;
            polity.FoodSurplusThisMonth = 0;

            polity.FoodNeededThisMonth = polity.Population;

            double plantFoodTarget = polity.Population * 0.85;
            double animalFoodTarget = polity.Population * 0.35;

            double gatheredPlants = Math.Min(region.PlantBiomass, plantFoodTarget);
            double gatheredAnimals = Math.Min(region.AnimalBiomass, animalFoodTarget);

            region.PlantBiomass -= gatheredPlants;
            region.AnimalBiomass -= gatheredAnimals;

            double totalFood = gatheredPlants + gatheredAnimals;
            polity.FoodGatheredThisMonth = totalFood;
            polity.FoodStores += totalFood;
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
            double surplus = Math.Max(0, polity.FoodStores - eaten);

            polity.FoodConsumedThisMonth = eaten;
            polity.FoodShortageThisMonth = shortage;
            polity.FoodSurplusThisMonth = surplus;
            polity.FoodSatisfactionThisMonth = need <= 0 ? 1.0 : eaten / need;

            polity.FoodStores -= eaten;

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