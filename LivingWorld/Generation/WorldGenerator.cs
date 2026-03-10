
using LivingWorld.Core;
using LivingWorld.Map;
using LivingWorld.Life;
using LivingWorld.Societies;

namespace LivingWorld.Generation;

public sealed class WorldGenerator
{
    private readonly Random _random;
    private readonly Queue<string> _regionNames;
    private readonly Queue<string> _polityNames;

    public WorldGenerator(int seed)
    {
        _random = new Random(seed);
        _regionNames = new Queue<string>(BuildShuffledNames(CreateRegionNames()));
        _polityNames = new Queue<string>(BuildShuffledNames(CreatePolityNames()));
    }

    public World Generate()
    {
        World world = new(new WorldTime());

        GenerateRegions(world);
        ConnectRegions(world);
        GenerateSpecies(world);
        GeneratePolities(world);

        return world;
    }

    private void GenerateRegions(World world)
    {
        for (int i = 0; i < 10; i++)
        {
            Region region = new(i, NextRegionName(i))
            {
                Fertility = _random.NextDouble(),
                WaterAvailability = _random.NextDouble(),
                PlantBiomass = 500,
                AnimalBiomass = 200,
                MaxPlantBiomass = 1000,
                MaxAnimalBiomass = 400
            };

            world.Regions.Add(region);
        }
    }

    private void GenerateSpecies(World world)
    {
        for (int i = 0; i < 3; i++)
        {
            Species species = new(i, $"Species {i}", _random.NextDouble(), _random.NextDouble());

            world.Species.Add(species);
        }
    }

    private void GeneratePolities(World world)
    {
        for (int i = 0; i < 5; i++)
        {
            int speciesId = _random.Next(world.Species.Count);
            int regionId = _random.Next(world.Regions.Count);

            Polity polity = new(i, NextPolityName(i), speciesId, regionId, _random.Next(30, 80));

            world.Polities.Add(polity);
        }
    }

    private void ConnectRegions(World world)
    {
        // Guaranteed chain so every region is reachable
        for (int i = 0; i < world.Regions.Count - 1; i++)
        {
            AddConnection(world.Regions[i], world.Regions[i + 1]);
        }

        // Add a few extra random connections
        int extraConnections = Math.Max(2, world.Regions.Count / 3);

        for (int i = 0; i < extraConnections; i++)
        {
            Region a = world.Regions[_random.Next(world.Regions.Count)];
            Region b = world.Regions[_random.Next(world.Regions.Count)];

            if (a.Id != b.Id)
            {
                AddConnection(a, b);
            }
        }
    }

    private static void AddConnection(Region a, Region b)
    {
        a.AddConnection(b.Id);
        b.AddConnection(a.Id);
    }

    private string NextRegionName(int index)
        => _regionNames.Count > 0 ? _regionNames.Dequeue() : $"Reach {index}";

    private string NextPolityName(int index)
        => _polityNames.Count > 0 ? _polityNames.Dequeue() : $"Clan {index}";

    private IEnumerable<string> BuildShuffledNames(IReadOnlyList<string> names)
        => names.OrderBy(_ => _random.Next()).ToArray();

    private static IReadOnlyList<string> CreateRegionNames()
        => new[]
        {
            "Ashen Vale",
            "Stonewater",
            "Red Marsh",
            "Sun Hollow",
            "Frostmere",
            "Green Barrow",
            "Ironwood",
            "Mistral Steppe",
            "Brightfen",
            "Raven Coast",
            "Thornfield",
            "Amber Reach"
        };

    private static IReadOnlyList<string> CreatePolityNames()
        => new[]
        {
            "Riverwatch Clan",
            "Emberfall Kin",
            "Stone Antler Tribe",
            "Moss Hearth Folk",
            "Red Reed Circle",
            "Sky Elk People",
            "Winter Oak Clan",
            "Dawnfire Band",
            "Cinder Brook Kin",
            "Deepfield Tribe"
        };
}
