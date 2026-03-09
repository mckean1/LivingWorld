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
        Console.WriteLine("=================================================");
        Console.WriteLine($"YEAR {_world.Time.Year} SUMMARY");
        Console.WriteLine("=================================================");

        int livingPolities = _world.Polities.Count(p => p.Population > 0);
        int totalPopulation = _world.Polities.Sum(p => p.Population);

        int movedThisYear = _world.Polities.Count(p => p.MovedThisYear);
        int totalMoves = _world.Polities.Sum(p => p.MovesThisYear);

        double avgMigrationPressure =
            _world.Polities.Average(p => p.MigrationPressure);

        Console.WriteLine();
        Console.WriteLine("WORLD STATS");
        Console.WriteLine($"Living Societies: {livingPolities}");
        Console.WriteLine($"Total Population: {totalPopulation}");
        Console.WriteLine($"Societies Moved This Year: {movedThisYear}");
        Console.WriteLine($"Total Moves: {totalMoves}");
        Console.WriteLine($"Average Migration Pressure: {avgMigrationPressure:F2}");

        Console.WriteLine();
        Console.WriteLine("SOCIETIES");

        foreach (var polity in _world.Polities.OrderByDescending(p => p.Population))
        {
            string moved = polity.MovedThisYear ? "YES" : "NO";

            Console.WriteLine(
                $"{polity.Name,-20} " +
                $"Pop:{polity.Population,-5} " +
                $"Region:{polity.PreviousRegionId}->{polity.RegionId} " +
                $"Moved:{moved,-3} " +
                $"Pressure:{polity.MigrationPressure:F2} " +
                $"Food:{polity.FoodStores:F1}"
            );
        }

        PrintRegionReport();

        ResetYearTracking();
    }

    private void PrintRegionReport()
    {
        Console.WriteLine();
        Console.WriteLine("REGION STATUS");

        foreach (var region in _world.Regions)
        {
            int population =
                _world.Polities
                    .Where(p => p.RegionId == region.Id)
                    .Sum(p => p.Population);

            Console.WriteLine(
                $"Region {region.Id,-3} " +
                $"Pop:{population,-5} " +
                $"Plants:{region.PlantBiomass,8:F0} " +
                $"Animals:{region.AnimalBiomass,8:F0}"
            );
        }
    }

    private void ResetYearTracking()
    {
        foreach (var polity in _world.Polities)
        {
            polity.MovedThisYear = false;
            polity.MovesThisYear = 0;
            polity.PreviousRegionId = polity.RegionId;
        }
    }
}