using LivingWorld.Societies;
using LivingWorld.Presentation;
using LivingWorld.Systems;
using System.Threading;

namespace LivingWorld.Core;

public sealed class Simulation
{
    private readonly World _world;
    private readonly FoodSystem _foodSystem;
    private readonly AgricultureSystem _agricultureSystem;
    private readonly PopulationSystem _populationSystem;
    private readonly MigrationSystem _migrationSystem;
    private readonly AdvancementSystem _advancementSystem;
    private readonly SettlementSystem _settlementSystem;
    private readonly FragmentationSystem _fragmentationSystem;
    private readonly PolityStageSystem _polityStageSystem;
    private readonly SimulationOptions _options;
    private readonly NarrativeRenderer _narrativeRenderer;

    public Simulation(World world, SimulationOptions? options = null)
    {
        _world = world;
        _foodSystem = new FoodSystem();
        _agricultureSystem = new AgricultureSystem();
        _populationSystem = new PopulationSystem();
        _migrationSystem = new MigrationSystem();
        _advancementSystem = new AdvancementSystem();
        _settlementSystem = new SettlementSystem();
        _fragmentationSystem = new FragmentationSystem();
        _polityStageSystem = new PolityStageSystem();
        _options = options ?? new SimulationOptions();
        _narrativeRenderer = new NarrativeRenderer();
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

        // Monthly systems
        _foodSystem.UpdateRegionEcology(_world);
        _foodSystem.GatherFood(_world);
        _agricultureSystem.ProduceFarmFood(_world);
        _foodSystem.ConsumeFood(_world);
        _migrationSystem.UpdateMigration(_world);
        PrintTickReport();

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
            _advancementSystem.UpdateAdvancements(_world);
            _settlementSystem.UpdateSettlements(_world);
            _fragmentationSystem.UpdateFragmentation(_world);
            _polityStageSystem.UpdatePolityStages(_world);
            _agricultureSystem.UpdateAnnualAgriculture(_world);

            AddYearlyFoodStressEvents();

            _world.Polities.RemoveAll(p => p.Population <= 0);

            PrintYearReport();
            ResetAnnualStats();

            if (_options.PauseAfterEachYear)
            {
                Console.ReadKey();
            }
        }

        _world.Time.AdvanceOneMonth();
    }

    private void AddYearlyFoodStressEvents()
    {
        foreach (var polity in _world.Polities.Where(p => p.Population > 0))
        {
            if (polity.StarvationMonthsThisYear < 2)
            {
                continue;
            }

            double annualFoodRatio = polity.AnnualFoodNeeded <= 0
                ? 1.0
                : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

            _world.AddEvent(
                "FOOD-STRESS",
                BuildFoodStressNarrative(polity, annualFoodRatio),
                $"{polity.Name} endured {polity.StarvationMonthsThisYear} starvation months; AFR={annualFoodRatio:F2}."
            );
        }
    }

    private void ResetAnnualStats()
    {
        foreach (var polity in _world.Polities)
        {
            polity.ResetAnnualFoodStats();
        }
    }

    private void PrintYearReport()
    {
        if (_options.OutputMode == OutputMode.Debug)
        {
            PrintDebugYearSummary();
            PrintDebugYearEvents();
            return;
        }

        foreach (string line in _narrativeRenderer.RenderYearReport(_world))
        {
            Console.WriteLine(line);
        }
    }

    private void PrintTickReport()
    {
        if (!_options.StreamTickChronicle)
        {
            return;
        }

        if (_options.OutputMode == OutputMode.Debug)
        {
            return;
        }

        foreach (string line in _narrativeRenderer.RenderTickChronicle(_world))
        {
            Console.WriteLine(line);
        }

        if (_options.TickDelayMilliseconds > 0)
        {
            Thread.Sleep(_options.TickDelayMilliseconds);
        }
    }

    private void PrintDebugYearEvents()
    {
        var eventsThisYear = _world.Events
            .Where(e => e.Year == _world.Time.Year)
            .OrderBy(e => e.Month)
            .ThenBy(e => e.Type)
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

    private void PrintDebugYearSummary()
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
                $"Wild={polity.AnnualFoodGathered,6:F0} " +
                $"Farm={polity.AnnualFoodFarmed,6:F0} " +
                $"Starve={polity.StarvationMonthsThisYear,2} " +
                $"Move={polity.MigrationPressure,4:F2} " +
                $"Frag={polity.FragmentationPressure,4:F2} " +
                $"Cool={polity.SplitCooldownYears,2} " +
                $"Know={polity.Advancements.Count,2} " +
                $"Settle={polity.SettlementStatus,-11} " +
                $"Stage={polity.Stage,-14}");
        }
    }

    private static string BuildFoodStressNarrative(Polity polity, double annualFoodRatio)
    {
        if (annualFoodRatio < 0.50)
        {
            return $"{polity.Name} fell into famine";
        }

        if (annualFoodRatio < 0.75)
        {
            return $"{polity.Name} endured a year of hunger";
        }

        return $"{polity.Name} faced repeated food shortages";
    }
}
