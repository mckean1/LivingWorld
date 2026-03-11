
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
    private readonly List<SpeciesTemplate> _speciesTemplates;

    public WorldGenerator(int seed)
    {
        _random = new Random(seed);
        _regionNames = new Queue<string>(BuildShuffledNames(CreateRegionNames()));
        _polityNames = new Queue<string>(BuildShuffledNames(CreatePolityNames()));
        _speciesTemplates = CreateSpeciesTemplates();
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
        for (int i = 0; i < _speciesTemplates.Count; i++)
        {
            SpeciesTemplate template = _speciesTemplates[i];
            Species species = new(i, template.Name, template.Intelligence, template.Cooperation)
            {
                IsSapient = template.IsSapient,
                TrophicRole = template.TrophicRole,
                FertilityPreference = template.FertilityPreference,
                WaterPreference = template.WaterPreference,
                PlantBiomassAffinity = template.PlantBiomassAffinity,
                AnimalBiomassAffinity = template.AnimalBiomassAffinity,
                BaseCarryingCapacityFactor = template.BaseCarryingCapacityFactor,
                MigrationCapability = template.MigrationCapability,
                ExpansionPressure = template.ExpansionPressure,
                BaseReproductionRate = template.BaseReproductionRate,
                BaseDeclineRate = template.BaseDeclineRate,
                SpringReproductionModifier = template.SpringModifier,
                SummerReproductionModifier = template.SummerModifier,
                AutumnReproductionModifier = template.AutumnModifier,
                WinterReproductionModifier = template.WinterModifier,
                MeatYield = template.MeatYield,
                HuntingDifficulty = template.HuntingDifficulty,
                HuntingDanger = template.HuntingDanger,
                IsToxicToEat = template.IsToxicToEat,
                DomesticationAffinity = template.DomesticationAffinity
            };
            species.DietSpeciesIds.AddRange(template.DietSpeciesIds);
            world.Species.Add(species);
        }
    }

    private void GeneratePolities(World world)
    {
        List<Species> politySpecies = world.Species.Where(species => species.IsSapient).ToList();
        for (int i = 0; i < 5; i++)
        {
            int speciesId = politySpecies[_random.Next(politySpecies.Count)].Id;
            int regionId = _random.Next(world.Regions.Count);

            Polity polity = new(i, NextPolityName(i), speciesId, regionId, _random.Next(30, 80), lineageId: i);

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

    private static List<SpeciesTemplate> CreateSpeciesTemplates()
        => new()
        {
            new SpeciesTemplate("Humans", 0.82, 0.74, true, TrophicRole.Omnivore, 0.58, 0.56, 0.40, 0.45, 0.95, 0.24, 0.18, 0.07, 0.03, 1.15, 1.00, 0.88, 0.62, 10, 0.30, 0.18, false, 0.45, [3, 4, 6]),
            new SpeciesTemplate("Wolfkin", 0.70, 0.68, true, TrophicRole.Predator, 0.46, 0.42, 0.28, 0.66, 0.75, 0.28, 0.22, 0.06, 0.04, 1.08, 1.02, 0.92, 0.66, 14, 0.36, 0.30, false, 0.18, [4, 6, 7]),
            new SpeciesTemplate("Horsefolk", 0.66, 0.79, true, TrophicRole.Herbivore, 0.55, 0.48, 0.62, 0.18, 0.90, 0.30, 0.24, 0.08, 0.03, 1.22, 1.08, 0.92, 0.58, 16, 0.26, 0.22, false, 0.52, [3, 5]),
            new SpeciesTemplate("River Reed", 0.08, 0.05, false, TrophicRole.Producer, 0.72, 0.82, 0.88, 0.02, 1.15, 0.10, 0.22, 0.16, 0.02, 1.35, 1.12, 0.90, 0.52, 0, 0.95, 0.00, false, 0.00, []),
            new SpeciesTemplate("Stonehorn Elk", 0.20, 0.34, false, TrophicRole.Herbivore, 0.56, 0.58, 0.78, 0.10, 1.05, 0.22, 0.18, 0.09, 0.03, 1.26, 1.04, 0.90, 0.56, 22, 0.34, 0.28, false, 0.62, [3]),
            new SpeciesTemplate("Redcap Mushroom", 0.01, 0.00, false, TrophicRole.Producer, 0.44, 0.76, 0.68, 0.00, 0.82, 0.06, 0.10, 0.12, 0.03, 1.18, 0.98, 1.04, 0.74, 0, 0.98, 0.00, true, 0.00, []),
            new SpeciesTemplate("Ashfang Wolf", 0.14, 0.42, false, TrophicRole.Predator, 0.40, 0.38, 0.22, 0.74, 0.72, 0.32, 0.26, 0.07, 0.05, 1.04, 1.00, 0.92, 0.68, 14, 0.42, 0.44, false, 0.12, [4, 7]),
            new SpeciesTemplate("Ridge Lion", 0.18, 0.20, false, TrophicRole.Apex, 0.36, 0.30, 0.16, 0.82, 0.54, 0.34, 0.28, 0.05, 0.05, 1.00, 1.02, 0.94, 0.70, 30, 0.56, 0.72, false, 0.04, [4, 6])
        };

    private sealed record SpeciesTemplate(
        string Name,
        double Intelligence,
        double Cooperation,
        bool IsSapient,
        TrophicRole TrophicRole,
        double FertilityPreference,
        double WaterPreference,
        double PlantBiomassAffinity,
        double AnimalBiomassAffinity,
        double BaseCarryingCapacityFactor,
        double MigrationCapability,
        double ExpansionPressure,
        double BaseReproductionRate,
        double BaseDeclineRate,
        double SpringModifier,
        double SummerModifier,
        double AutumnModifier,
        double WinterModifier,
        double MeatYield,
        double HuntingDifficulty,
        double HuntingDanger,
        bool IsToxicToEat,
        double DomesticationAffinity,
        IReadOnlyList<int> DietSpeciesIds);
}
