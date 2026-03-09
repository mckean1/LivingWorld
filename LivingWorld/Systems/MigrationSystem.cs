using LivingWorld.Core;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class MigrationSystem
{
    private readonly Random _random;

    public MigrationSystem(int seed = 12345)
    {
        _random = new Random(seed);
    }

    public void UpdateMigration(World world)
    {
        foreach (Polity polity in world.Polities)
        {
            if (polity.Population <= 0)
                continue;

            Region currentRegion = world.Regions.First(r => r.Id == polity.RegionId);

            polity.MigrationPressure = CalculateMigrationPressure(polity, currentRegion);

            if (polity.MigrationPressure >= 0.65)
            {
                Region? target = FindBestConnectedRegion(world, polity, currentRegion);

                if (target is not null && target.Id != currentRegion.Id)
                {
                    polity.PreviousRegionId = polity.RegionId;
                    polity.RegionId = target.Id;

                    polity.MovedThisYear = true;
                    polity.MovesThisYear++;
                    polity.FoodStores *= 0.75; // moving costs food
                }
            }
        }
    }

    private double CalculateMigrationPressure(Polity polity, Region region)
    {
        double monthlyFoodNeed = Math.Max(1, polity.Population);
        double foodSafety = Math.Min(1.0, polity.FoodStores / monthlyFoodNeed);

        double ecologyRatio = 0;
        if (region.TotalBiomassCapacity > 0)
        {
            ecologyRatio = region.TotalBiomass / region.TotalBiomassCapacity;
        }

        double foodPressure = 1.0 - foodSafety;
        double ecologyPressure = 1.0 - ecologyRatio;

        double pressure =
            (foodPressure * 0.7) +
            (ecologyPressure * 0.3);

        return Math.Clamp(pressure, 0.0, 1.0);
    }

    private Region? FindBestConnectedRegion(World world, Polity polity, Region currentRegion)
    {
        if (currentRegion.ConnectedRegionIds.Count == 0)
            return null;

        List<Region> candidates = world.Regions
            .Where(r => currentRegion.ConnectedRegionIds.Contains(r.Id))
            .ToList();

        if (candidates.Count == 0)
            return null;

        Region? bestRegion = null;
        double bestScore = double.MinValue;

        foreach (Region region in candidates)
        {
            int localPopulation = world.Polities
                .Where(p => p.RegionId == region.Id && p.Population > 0)
                .Sum(p => p.Population);

            double crowdingPenalty = localPopulation * 0.5;

            double score =
                region.PlantBiomass +
                region.AnimalBiomass +
                (region.Fertility * 200) +
                (region.WaterAvailability * 150) -
                crowdingPenalty;

            // Small randomness so ties don't always pick the same region
            score += _random.NextDouble() * 10.0;

            if (score > bestScore)
            {
                bestScore = score;
                bestRegion = region;
            }
        }

        return bestRegion;
    }
}