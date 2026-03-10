using LivingWorld.Core;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class ExpansionSystem
{
    private readonly Random _random;

    public ExpansionSystem(int seed = 54321)
    {
        _random = new Random(seed);
    }

    public void UpdateExpansion(World world)
    {
        if (world.Time.Month != 12)
        {
            return;
        }

        List<Polity> newPolities = new();
        int nextId = world.Polities.Count == 0 ? 0 : world.Polities.Max(p => p.Id) + 1;

        foreach (Polity polity in world.Polities.Where(p => p.Population > 0).ToList())
        {
            Region home = world.Regions.First(r => r.Id == polity.RegionId);

            int localPopulation = world.Polities
                .Where(p => p.RegionId == home.Id && p.Population > 0)
                .Sum(p => p.Population);

            bool overcrowded = localPopulation > home.CarryingCapacity * 0.85;
            bool largeEnoughToSplit = polity.Population >= 35;

            double annualFoodRatio = polity.AnnualFoodNeeded <= 0
                ? 1.0
                : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

            bool wellFed = annualFoodRatio >= 0.90;

            bool expansionPressure = overcrowded || largeEnoughToSplit;

            if (!expansionPressure || !wellFed)
            {
                continue;
            }

            List<Region> candidates = world.Regions
                .Where(r =>
                    r.Id != home.Id &&
                    home.ConnectedRegionIds.Contains(r.Id))
                .ToList();

            if (candidates.Count == 0)
            {
                continue;
            }

            Region target = candidates
                .OrderByDescending(r =>
                {
                    int targetPopulation = world.Polities
                        .Where(p => p.RegionId == r.Id && p.Population > 0)
                        .Sum(p => p.Population);

                    return r.TotalBiomass
                        + (r.Fertility * 200)
                        + (r.WaterAvailability * 150)
                        + (r.CarryingCapacity * 2.0)
                        - (targetPopulation * 0.8)
                        + (_random.NextDouble() * 10.0);
                })
                .First();

            int splitPopulation = Math.Max(10, polity.Population / 3);
            polity.Population -= splitPopulation;

            Polity child = new(
                nextId++,
                $"{polity.Name} Colony",
                polity.SpeciesId,
                target.Id,
                splitPopulation);

            child.FoodStores = polity.FoodStores * 0.30;
            child.InheritAdvancements(polity.Advancements);
            polity.FoodStores *= 0.70;

            newPolities.Add(child);

            world.AddEvent(
                "COLONY",
                $"{polity.Name} founded {child.Name} in {target.Name}",
                $"{polity.Name} founded {child.Name} in Region {target.Id} with population {splitPopulation}."
            );
        }

        world.Polities.AddRange(newPolities);
    }
}
