using LivingWorld.Societies;
using LivingWorld.Systems;

namespace LivingWorld.Core;

public sealed class Simulation
{
    private readonly World _world;
    private readonly FoodSystem _foodSystem;
    private readonly PopulationSystem _populationSystem;
    private readonly MigrationSystem _migrationSystem;
    private readonly ExpansionSystem _expansionSystem;

    public Simulation(World world)
    {
        _world = world;
        _foodSystem = new FoodSystem();
        _populationSystem = new PopulationSystem();
        _migrationSystem = new MigrationSystem();
        _expansionSystem = new ExpansionSystem();
    }

    public void RunMonths(int months)
    {
        for (int i = 0; i < months; i++)
        {
            RunTick();
        }
    }

    private void RunTick()
    {
        _world.Time.AdvanceOneMonth();

        // Monthly systems
        _foodSystem.UpdateRegionEcology(_world);
        _foodSystem.GatherFood(_world);
        _foodSystem.ConsumeFood(_world);
        _migrationSystem.UpdateMigration(_world);

        // Year-end systems
        if (_world.Time.Month == 12)
        {
            _populationSystem.UpdatePopulation(_world);
            _expansionSystem.UpdateExpansion(_world);

            PrintYearSummary();

            ResetAnnualStats();

            Console.ReadKey();
        }
    }

    private void ResetAnnualStats()
    {
        foreach (var polity in _world.Polities)
        {
            polity.ResetAnnualFoodStats();
        }
    }

    private void PrintYearSummary()
    {
        Console.WriteLine();
        Console.WriteLine($"=== YEAR {_world.Time.Year} SUMMARY ===");

        foreach (var polity in _world.Polities.OrderByDescending(p => p.Population))
        {
            double annualFoodRatio = polity.AnnualFoodNeeded <= 0
                ? 1.0
                : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

            Console.WriteLine(
                $"- {polity.Name} | " +
                $"Pop={polity.Population} | " +
                $"Region={polity.RegionId} | " +
                $"Food={polity.FoodStores:F1} | " +
                $"Gathered={polity.FoodGatheredThisMonth:F1} | " +
                $"Consumed={polity.FoodConsumedThisMonth:F1} | " +
                $"Need={polity.FoodNeededThisMonth:F1} | " +
                $"Shortage={polity.FoodShortageThisMonth:F1} | " +
                $"AnnualFoodRatio={annualFoodRatio:F2} | " +
                $"StarvationMonths={polity.StarvationMonthsThisYear} | " +
                $"MigrationPressure={polity.MigrationPressure:F2}");
        }
    }
}