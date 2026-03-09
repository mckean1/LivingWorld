using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class PopulationSystem
{
    public void UpdatePopulation(World world)
    {
        foreach (Polity polity in world.Polities)
        {
            if (world.Time.Month != 12)
                continue;

            double foodRatio = 1.0;

            if (polity.FoodNeededThisMonth > 0)
            {
                foodRatio = polity.FoodConsumedThisMonth / polity.FoodNeededThisMonth;
            }

            if (foodRatio >= 1.0)
            {
                int growth = Math.Max(1, (int)(polity.Population * 0.02));
                polity.Population += growth;
            }
            else if (foodRatio >= 0.75)
            {
                // Stable year, no change.
            }
            else if (foodRatio >= 0.50)
            {
                int losses = Math.Max(1, (int)(polity.Population * 0.03));
                polity.Population -= losses;
            }
            else
            {
                int losses = Math.Max(1, (int)(polity.Population * 0.08));
                polity.Population -= losses;
            }

            if (polity.Population < 0)
            {
                polity.Population = 0;
            }
        }
    }
}