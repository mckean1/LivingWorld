
using LivingWorld.Core;
using LivingWorld.Generation;

namespace LivingWorld;

class Program
{
    static void Main()
    {
        const int seed = 1;
        const int monthsToSimulate = 12;
        const int monthsInYear = 12;

        WorldGenerator generator = new(seed);
        World world = generator.Generate();

        Simulation simulation = new(world);

        Console.WriteLine("Press any key to start simulation.");
        Console.ReadKey();

        simulation.RunMonths(monthsToSimulate * monthsInYear);

        Console.WriteLine("Simulation complete.");
    }
}


