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
            foreach (var polity in _world.Polities)
            {
                if (polity.Population > 0)
                {
                    polity.YearsSinceFounded++;
                }
            }

            _populationSystem.UpdatePopulation(_world);
            _expansionSystem.UpdateExpansion(_world);

            foreach (var polity in _world.Polities.Where(p => p.Population <= 0))
            {
                _world.AddEvent(
                    "COLLAPSE",
                    $"{polity.Name} collapsed in Region {polity.RegionId}.");
            }

            _world.Polities.RemoveAll(p => p.Population <= 0);

            PrintYearSummary();
            PrintYearEvents();
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

    private void PrintYearEvents()
    {
        var eventsThisYear = _world.Events
            .Where(e => e.Year == _world.Time.Year)
            .ToList();

        if (eventsThisYear.Count == 0)
        {
            return;
        }

        Console.WriteLine("Events:");
        foreach (var worldEvent in eventsThisYear)
        {
            Console.WriteLine($"  {worldEvent}");
        }
    }

    private void PrintFoodAlerts()
    {
        var stressed = _world.Polities
            .Where(p => p.Population > 0 &&
                        (p.FoodShortageThisMonth > 0 || p.StarvationMonthsThisYear > 0))
            .OrderByDescending(p => p.StarvationMonthsThisYear)
            .ToList();

        if (stressed.Count == 0)
        {
            return;
        }

        Console.WriteLine("Food Stress:");
        foreach (var polity in stressed)
        {
            Console.WriteLine(
                $"  - {polity.Name}: " +
                $"Need={polity.FoodNeededThisMonth:F1}, " +
                $"Consumed={polity.FoodConsumedThisMonth:F1}, " +
                $"Shortage={polity.FoodShortageThisMonth:F1}, " +
                $"AFR={(polity.AnnualFoodNeeded <= 0 ? 1.0 : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded):F2}, " +
                $"StarvationMonths={polity.StarvationMonthsThisYear}");
        }
    }

    private void PrintYearSummary()
    {
        int activePolities = _world.Polities.Count(p => p.Population > 0);
        int totalPopulation = _world.Polities.Where(p => p.Population > 0).Sum(p => p.Population);

        Console.WriteLine();
        Console.WriteLine($"=== YEAR {_world.Time.Year} SUMMARY ===");
        Console.WriteLine($"Active Polities: {activePolities} | Total Population: {totalPopulation}");

        foreach (var polity in _world.Polities.OrderByDescending(p => p.Population).ThenBy(p => p.Name))
        {
            double annualFoodRatio = polity.AnnualFoodNeeded <= 0
                ? 1.0
                : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

            Console.WriteLine(
                $"- {polity.Name,-22} " +
                $"Pop={polity.Population,3} " +
                $"Age={polity.YearsSinceFounded,3} " +
                $"Reg={polity.RegionId,2} " +
                $"Food={polity.FoodStores,6:F1} " +
                $"AFR={annualFoodRatio,4:F2} " +
                $"Starve={polity.StarvationMonthsThisYear,2} " +
                $"Move={polity.MigrationPressure,4:F2}");
        }
    }
}