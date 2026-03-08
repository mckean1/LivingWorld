using LivingWorld.Systems;
using LivingWorld.Map;
using LivingWorld.Societies;
using LivingWorld.Life;

namespace LivingWorld.Core;

public sealed class Simulation
{
    private readonly World _world;
    private readonly FoodSystem _foodSystem;
    private readonly PopulationSystem _populationSystem;

    public Simulation(World world)
    {
        _world = world;
        _foodSystem = new FoodSystem();
        _populationSystem = new PopulationSystem();
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

        if (_world.Time.Month == 12)
        {
            PrintYearSummary();

            Console.WriteLine("Press any key to continue.");
            Console.ReadKey();
        }
    }

    private void PrintYearSummary()
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 72));
        Console.WriteLine($"YEAR {_world.Time.Year} SUMMARY");
        Console.WriteLine(new string('=', 72));

        PrintWorldOverview();
        PrintPolityReport();
        PrintRegionReport();
    }

    private void PrintWorldOverview()
    {
        int livingPolities = _world.Polities.Count(p => p.Population > 0);
        int totalPopulation = _world.Polities.Where(p => p.Population > 0).Sum(p => p.Population);
        double totalStoredFood = _world.Polities.Where(p => p.Population > 0).Sum(p => p.FoodStores);

        double totalPlantBiomass = _world.Regions.Sum(r => r.PlantBiomass);
        double totalAnimalBiomass = _world.Regions.Sum(r => r.AnimalBiomass);

        Console.WriteLine("WORLD OVERVIEW");
        Console.WriteLine($"  Living societies : {livingPolities}");
        Console.WriteLine($"  Total population : {totalPopulation}");
        Console.WriteLine($"  Stored food      : {totalStoredFood:F1}");
        Console.WriteLine($"  Plant biomass    : {totalPlantBiomass:F1}");
        Console.WriteLine($"  Animal biomass   : {totalAnimalBiomass:F1}");
        Console.WriteLine();
    }

    private void PrintPolityReport()
    {
        Console.WriteLine("SOCIETIES");
        Console.WriteLine(
            $"{Pad("Name", 18)}" +
            $"{Pad("Species", 14)}" +
            $"{Pad("Region", 12)}" +
            $"{Pad("Pop", 8)}" +
            $"{Pad("Food", 10)}" +
            $"{Pad("Status", 12)}");

        foreach (Polity polity in _world.Polities.OrderByDescending(p => p.Population))
        {
            Species species = _world.Species.First(s => s.Id == polity.SpeciesId);
            Region region = _world.Regions.First(r => r.Id == polity.RegionId);

            string status = GetPolityStatus(polity);

            Console.WriteLine(
                $"{Pad(polity.Name, 18)}" +
                $"{Pad(species.Name, 14)}" +
                $"{Pad(region.Name, 12)}" +
                $"{Pad(polity.Population.ToString(), 8)}" +
                $"{Pad(polity.FoodStores.ToString("F1"), 10)}" +
                $"{Pad(status, 12)}");
        }

        Console.WriteLine();
    }

    private void PrintRegionReport()
    {
        Console.WriteLine("REGIONS");
        Console.WriteLine(
            $"{Pad("Region", 12)}" +
            $"{Pad("Fertility", 12)}" +
            $"{Pad("Water", 10)}" +
            $"{Pad("Plants", 12)}" +
            $"{Pad("Animals", 12)}" +
            $"{Pad("Pop", 8)}");

        foreach (Region region in _world.Regions.OrderBy(r => r.Id))
        {
            int regionPopulation = _world.Polities
                .Where(p => p.RegionId == region.Id && p.Population > 0)
                .Sum(p => p.Population);

            Console.WriteLine(
                $"{Pad(region.Name, 12)}" +
                $"{Pad(region.Fertility.ToString("F2"), 12)}" +
                $"{Pad(region.WaterAvailability.ToString("F2"), 10)}" +
                $"{Pad(region.PlantBiomass.ToString("F1"), 12)}" +
                $"{Pad(region.AnimalBiomass.ToString("F1"), 12)}" +
                $"{Pad(regionPopulation.ToString(), 8)}");
        }

        Console.WriteLine();
    }

    private static string GetPolityStatus(Polity polity)
    {
        if (polity.Population <= 0)
            return "Dead";

        if (polity.FoodStores <= 0)
            return "Starving";

        if (polity.FoodStores < polity.Population * 0.5)
            return "Strained";

        if (polity.FoodStores > polity.Population * 2.0)
            return "Stable";

        return "Holding";
    }

    private static string Pad(string value, int width)
    {
        if (value.Length >= width)
            return value[..(width - 1)] + " ";

        return value.PadRight(width);
    }
}