
using LivingWorld.Core;
using LivingWorld.Generation;

namespace LivingWorld;

class Program
{
    static void Main()
    {
        const int seed = 1;

        WorldGenerator generator = new(seed);
        World world = generator.Generate();

        Simulation simulation = new(world);

        simulation.RunMonths(240);

        Console.WriteLine("Simulation complete.");
    }
}
