using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class PopulationSystem
{
    private static readonly int[] PopulationMilestones = [25, 50, 100, 250, 500, 1000];

    public void UpdatePopulation(World world)
    {
        if (world.Time.Month != 12)
        {
            return;
        }

        foreach (Polity polity in world.Polities)
        {
            int previousPopulation = polity.Population;

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

            AddPopulationEvents(world, polity, previousPopulation);
        }
    }

    private static void AddPopulationEvents(World world, Polity polity, int previousPopulation)
    {
        if (previousPopulation > 0 && polity.Population == 0)
        {
            string collapseNarrative = polity.StarvationMonthsThisYear >= 6
                ? $"{polity.Name} collapsed after prolonged famine"
                : $"{polity.Name} collapsed";

            world.AddEvent(
                "COLLAPSE",
                collapseNarrative,
                $"{polity.Name} fell from population {previousPopulation} to 0."
            );

            return;
        }

        int? milestone = PopulationMilestones
            .Where(value => previousPopulation < value && polity.Population >= value)
            .OrderBy(value => value)
            .FirstOrDefault();

        if (milestone is null || milestone.Value == 0)
        {
            return;
        }

        world.AddEvent(
            "POPULATION",
            $"{polity.Name} grew to {milestone.Value} people",
            $"{polity.Name} grew from population {previousPopulation} to {polity.Population}, crossing milestone {milestone.Value}."
        );
    }
}
