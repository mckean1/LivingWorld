using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class PopulationSystem
{
    public void UpdatePopulation(World world)
    {
        if (world.Time.Month != 12)
        {
            return;
        }

        foreach (Polity polity in world.Polities)
        {
            if (polity.Population <= 0)
            {
                polity.Population = 0;
                polity.ResetAnnualFoodStats();
                continue;
            }

            double annualFoodRatio = polity.AnnualFoodNeeded <= 0
                ? 1.0
                : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

            int populationChange = annualFoodRatio switch
            {
                >= 1.00 => Math.Max(1, (int)(polity.Population * 0.02)),
                >= 0.90 => 0,
                >= 0.75 => -Math.Max(1, (int)(polity.Population * 0.02)),
                >= 0.50 => -Math.Max(1, (int)(polity.Population * 0.06)),
                _ => -Math.Max(1, (int)(polity.Population * 0.12))
            };

            // Repeated famine worsens mortality.
            if (polity.StarvationMonthsThisYear >= 6)
            {
                populationChange -= Math.Max(1, (int)(polity.Population * 0.03));
            }

            polity.Population += populationChange;

            if (polity.Population < 0)
            {
                polity.Population = 0;
            }
        }
    }
}