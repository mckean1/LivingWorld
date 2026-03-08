
using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class PopulationSystem
{
    public void UpdatePopulation(World world)
    {
        foreach (Polity polity in world.Polities)
        {
            if (world.Time.Month == 12)
            {
                polity.Population += (int)(polity.Population * 0.02);
            }
        }
    }
}
