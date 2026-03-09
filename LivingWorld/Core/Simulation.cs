using LivingWorld.Systems;

namespace LivingWorld.Core;

public sealed class Simulation
{
    private readonly World _world;
    private readonly FoodSystem _foodSystem;
    private readonly PopulationSystem _populationSystem;
    private readonly MigrationSystem _migrationSystem;

    public Simulation(World world)
    {
        _world = world;
        _foodSystem = new FoodSystem();
        _populationSystem = new PopulationSystem();
        _migrationSystem = new MigrationSystem();
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

        _foodSystem.UpdateRegionEcology(_world);
        _foodSystem.GatherFood(_world);
        _foodSystem.ConsumeFood(_world);
        _populationSystem.UpdatePopulation(_world);
        _migrationSystem.UpdateMigration(_world);

        if (_world.Time.Month == 12)
        {
            PrintYearSummary();
            Console.ReadKey();
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