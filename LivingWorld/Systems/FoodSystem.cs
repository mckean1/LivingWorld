using LivingWorld.Core;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class FoodSystem
{
    public void UpdateRegionEcology(World world)
    {
        foreach (Region region in world.Regions)
        {
            double plantGrowth =
                (region.Fertility * 20.0) +
                (region.WaterAvailability * 15.0);

            double animalGrowth =
                (region.Fertility * 6.0) +
                (region.WaterAvailability * 4.0);

            region.PlantBiomass = Math.Min(region.MaxPlantBiomass, region.PlantBiomass + plantGrowth);
            region.AnimalBiomass = Math.Min(region.MaxAnimalBiomass, region.AnimalBiomass + animalGrowth);
        }
    }

    public void GatherFood(World world)
    {
        foreach (Polity polity in world.Polities)
        {
            Region region = world.Regions.First(r => r.Id == polity.RegionId);

            polity.FoodGatheredThisMonth = 0;
            polity.FoodConsumedThisMonth = 0;
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
            double need = polity.FoodNeededThisMonth;
            double eaten = Math.Min(polity.FoodStores, need);

            polity.FoodConsumedThisMonth = eaten;
            polity.FoodStores -= eaten;
        }
    }
}