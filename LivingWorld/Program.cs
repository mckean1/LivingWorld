
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

        Simulation simulation = new(world, options);

        Console.WriteLine("Press any key to start simulation.");
        Console.ReadKey();

        simulation.RunMonths(yearsToSimulate * monthsInYear);

        Console.WriteLine("Simulation complete.");
    }
}


