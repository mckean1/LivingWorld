
using LivingWorld.Core;
using LivingWorld.Map;
using LivingWorld.Life;
using LivingWorld.Societies;

namespace LivingWorld.Generation;

public sealed class WorldGenerator
{
    private readonly Random _random;

    public WorldGenerator(int seed)
    {
        _random = new Random(seed);
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
            Region region = new(i, $"Region {i}")
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

            Polity polity = new(i, $"Society {i}", speciesId, regionId, _random.Next(30, 80));

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
}
