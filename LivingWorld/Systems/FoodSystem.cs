
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
            region.PlantBiomass = Math.Min(region.MaxPlantBiomass, region.PlantBiomass + 10);
            region.AnimalBiomass = Math.Min(region.MaxAnimalBiomass, region.AnimalBiomass + 5);
        }
    }

    public void GatherFood(World world)
    {
        foreach (Polity polity in world.Polities)
        {
            Region region = world.Regions.First(r => r.Id == polity.RegionId);

            double food = Math.Min(region.PlantBiomass, polity.Population * 0.5);

            region.PlantBiomass -= food;
            polity.FoodStores += food;
        }
    }

    public void ConsumeFood(World world)
    {
        foreach (Polity polity in world.Polities)
        {
            double need = polity.Population;

            double eaten = Math.Min(polity.FoodStores, need);

            polity.FoodStores -= eaten;
        }
    }
}
