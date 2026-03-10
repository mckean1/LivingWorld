using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class AdvancementSystem
{
    private readonly Random _random;

    public AdvancementSystem(int seed = 24680)
    {
        _random = new Random(seed);
    }

    public void UpdateAdvancements(World world)
    {
        if (world.Time.Month != 12)
        {
            return;
        }

        foreach (Polity polity in world.Polities.Where(p => p.Population > 0))
        {
            AdvancementContext context = BuildContext(world, polity);

            foreach (AdvancementDefinition definition in AdvancementCatalog.All)
            {
                if (polity.HasAdvancement(definition.Id))
                {
                    continue;
                }

                if (!HasPrerequisites(polity, definition))
                {
                    continue;
                }

                double chance = definition.DiscoveryChance(context);
                if (_random.NextDouble() > chance)
                {
                    continue;
                }

                if (!polity.DiscoverAdvancement(definition.Id))
                {
                    continue;
                }

                definition.OnDiscovered?.Invoke(world, polity);
                world.AddEvent(
                    "ADVANCEMENT",
                    BuildDiscoveryNarrative(polity, definition),
                    $"{polity.Name} discovered {definition.Name} with annual chance {chance:F3}.");
            }
        }
    }

    private static AdvancementContext BuildContext(World world, Polity polity)
    {
        Region region = world.Regions.First(r => r.Id == polity.RegionId);
        Species species = world.Species.First(s => s.Id == polity.SpeciesId);

        double annualFoodRatio = polity.AnnualFoodNeeded <= 0
            ? 1.0
            : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

        double reserveMonths = polity.Population <= 0
            ? 0.0
            : polity.FoodStores / polity.Population;

        int localPopulation = world.Polities
            .Where(p => p.RegionId == polity.RegionId && p.Population > 0)
            .Sum(p => p.Population);

        double crowdingRatio = region.CarryingCapacity <= 0
            ? 1.0
            : localPopulation / region.CarryingCapacity;

        double localPopulationRatio = Math.Clamp(localPopulation / 120.0, 0.0, 1.0);
        double foodStressRatio = Math.Clamp(polity.StarvationMonthsThisYear / 12.0, 0.0, 1.0);
        bool isMobile = polity.MovedThisYear || polity.MigrationPressure >= 0.45;

        return new AdvancementContext(
            world,
            polity,
            region,
            species,
            annualFoodRatio,
            reserveMonths,
            crowdingRatio,
            localPopulationRatio,
            foodStressRatio,
            isMobile);
    }

    private static bool HasPrerequisites(Polity polity, AdvancementDefinition definition)
        => definition.Prerequisites.All(polity.HasAdvancement);

    private static string BuildDiscoveryNarrative(Polity polity, AdvancementDefinition definition)
        => definition.DiscoveryNarrative?.Invoke(polity)
            ?? $"{polity.Name} discovered {definition.Name}";
}
