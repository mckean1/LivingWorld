
using LivingWorld.Core;
using LivingWorld.Generation;
using LivingWorld.Presentation;

namespace LivingWorld;

class Program
{
    static void Main()
    {
        const int seed = 1;
        const int yearsToSimulate = 12;
        const int monthsInYear = 12;

        WorldGenerator generator = new(seed);
        World world = generator.Generate();

        SimulationOptions options = SimulationOptions.NarrativeChronicle();
        using Simulation simulation = new(world, options);

        if (options.PauseBeforeStart)
        {
            Console.WriteLine("Press any key to start simulation.");
            Console.ReadKey();
        }

        simulation.RunMonths(yearsToSimulate * monthsInYear);
        if (options.WriteStructuredHistory)
        {
            Console.WriteLine($"History file: {Path.GetFullPath(options.HistoryFilePath)}");
        }
        Console.WriteLine("Simulation complete.");
    }
}


