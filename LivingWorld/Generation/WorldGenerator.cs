using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Generation;

public sealed class WorldGenerator
{
    private readonly Random _random;
    private readonly Queue<string> _regionNames;
    private readonly Queue<string> _polityNames;
    private readonly List<SpeciesTemplate> _speciesTemplates;
    private readonly WorldGenerationSettings _settings;
    private Dictionary<int, Species>? _speciesById;
    private Dictionary<int, Region>? _regionsById;

    public WorldGenerator(int seed, WorldGenerationSettings? settings = null)
    {
        _random = new Random(seed);
        _settings = settings ?? new WorldGenerationSettings();
        _regionNames = new Queue<string>(BuildShuffledNames(WorldGenerationCatalog.CreateRegionNames()));
        _polityNames = new Queue<string>(BuildShuffledNames(WorldGenerationCatalog.CreatePolityNames()));
        _speciesTemplates = WorldGenerationCatalog.CreateSpeciesTemplates();
    }

    public World Generate()
    {
        ValidateSettings();

        World world = new(new WorldTime());
        GenerateRegions(world);
        ConnectRegions(world);
        _regionsById = world.Regions.ToDictionary(region => region.Id);
        GenerateSpecies(world);
        _speciesById = world.Species.ToDictionary(species => species.Id);
        AssignInitialSpeciesRanges(world);
        EnsureFertileRegionsHaveFauna(world);
        ConstrainPredatorRangesToPreySupportedRegions(world);
        GeneratePolities(world);

        return world;
    }

    private void GenerateRegions(World world)
    {
        RegionBiome[,] biomeGrid = WorldGenerationCatalog.CreateBiomeGrid();

        for (int row = 0; row < _settings.ContinentHeight; row++)
        {
            for (int column = 0; column < _settings.ContinentWidth; column++)
            {
                int regionId = (row * _settings.ContinentWidth) + column;
                RegionBiome biome = biomeGrid[row, column];
                Region region = new(regionId, NextRegionName(regionId))
                {
                    Biome = biome
                };

                ApplyRegionProfile(region, biome);
                world.Regions.Add(region);
            }
        }
    }

    private void ConnectRegions(World world)
    {
        for (int row = 0; row < _settings.ContinentHeight; row++)
        {
            for (int column = 0; column < _settings.ContinentWidth; column++)
            {
                Region region = world.Regions[(row * _settings.ContinentWidth) + column];

                if (column + 1 < _settings.ContinentWidth)
                {
                    AddConnection(region, world.Regions[(row * _settings.ContinentWidth) + column + 1]);
                }

                if (row + 1 < _settings.ContinentHeight)
                {
                    AddConnection(region, world.Regions[((row + 1) * _settings.ContinentWidth) + column]);
                }

                if (column % 2 == 0 && column + 1 < _settings.ContinentWidth && row + 1 < _settings.ContinentHeight)
                {
                    AddConnection(region, world.Regions[((row + 1) * _settings.ContinentWidth) + column + 1]);
                }
            }
        }

        int riverColumn = Math.Clamp(_settings.ContinentWidth / 2, 0, _settings.ContinentWidth - 1);
        for (int row = 0; row < _settings.ContinentHeight - 1; row++)
        {
            AddConnection(
                world.Regions[(row * _settings.ContinentWidth) + riverColumn],
                world.Regions[((row + 1) * _settings.ContinentWidth) + riverColumn]);
        }
    }

    private void GenerateSpecies(World world)
    {
        foreach ((SpeciesTemplate template, int index) in _speciesTemplates.Take(_settings.InitialSpeciesCount).Select((template, index) => (template, index)))
        {
            Species species = new(index, template.Name, template.Intelligence, template.Cooperation)
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

            foreach (RegionBiome biome in template.PreferredBiomes)
            {
                species.PreferredBiomes.Add(biome);
            }

            world.Species.Add(species);
        }

        Dictionary<string, Species> speciesByName = world.Species.ToDictionary(species => species.Name, StringComparer.Ordinal);
        foreach (SpeciesTemplate template in _speciesTemplates.Take(_settings.InitialSpeciesCount))
        {
            Species species = speciesByName[template.Name];
            foreach (string dietSpeciesName in template.DietSpeciesNames)
            {
                if (speciesByName.TryGetValue(dietSpeciesName, out Species? prey))
                {
                    species.DietSpeciesIds.Add(prey.Id);
                }
            }
        }
    }

    private void AssignInitialSpeciesRanges(World world)
    {
        foreach (Species species in world.Species)
        {
            List<(Region Region, double Score)> viableRegions = world.Regions
                .Select(region => (Region: region, Score: ScoreRegionForSpecies(species, region)))
                .Where(entry => entry.Score >= ResolveRangeThreshold(species))
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.Region.Id)
                .ToList();

            if (viableRegions.Count == 0)
            {
                Region fallbackRegion = world.Regions.OrderByDescending(region => ScoreRegionForSpecies(species, region)).First();
                species.InitialRangeRegionIds.Add(fallbackRegion.Id);
                continue;
            }

            int targetRangeSize = ResolveTargetRangeSize(species, viableRegions.Count);
            Region seedRegion = viableRegions[_random.Next(Math.Min(3, viableRegions.Count))].Region;
            foreach (int regionId in BuildClusteredRange(world, viableRegions, seedRegion.Id, targetRangeSize))
            {
                species.InitialRangeRegionIds.Add(regionId);
            }
        }
    }

    private void EnsureFertileRegionsHaveFauna(World world)
    {
        List<Species> herbivores = world.Species
            .Where(species => species.TrophicRole == TrophicRole.Herbivore)
            .ToList();
        HashSet<int> herbivoreCoveredRegions = herbivores
            .SelectMany(species => species.InitialRangeRegionIds)
            .ToHashSet();

        foreach (Region region in world.Regions
                     .Where(IsFertileFaunaTarget)
                     .OrderByDescending(ResolveFertileFaunaScore)
                     .ThenBy(region => region.Id))
        {
            if (herbivoreCoveredRegions.Contains(region.Id))
            {
                continue;
            }

            Species? bestHerbivore = null;
            List<int>? bestExpansionPath = null;
            double bestFit = double.MinValue;
            int bestPathLength = int.MaxValue;

            foreach (Species herbivore in herbivores)
            {
                double fit = ScoreRegionForSpecies(herbivore, region);
                if (fit < 0.70)
                {
                    continue;
                }

                List<int>? path = BuildRangeExpansionPath(world, herbivore, region.Id);
                if (path is null)
                {
                    continue;
                }

                if (bestHerbivore is null
                    || path.Count < bestPathLength
                    || (path.Count == bestPathLength && fit > bestFit))
                {
                    bestHerbivore = herbivore;
                    bestExpansionPath = path;
                    bestFit = fit;
                    bestPathLength = path.Count;
                }
            }

            if (bestHerbivore is null || bestExpansionPath is null)
            {
                continue;
            }

            foreach (int regionId in bestExpansionPath)
            {
                if (bestHerbivore.InitialRangeRegionIds.Contains(regionId))
                {
                    continue;
                }

                bestHerbivore.InitialRangeRegionIds.Add(regionId);
                herbivoreCoveredRegions.Add(regionId);
            }
        }
    }

    private void ConstrainPredatorRangesToPreySupportedRegions(World world)
    {
        HashSet<int> herbivoreCoveredRegions = world.Species
            .Where(species => species.TrophicRole == TrophicRole.Herbivore)
            .SelectMany(species => species.InitialRangeRegionIds)
            .ToHashSet();

        foreach (Species predator in world.Species.Where(species => species.TrophicRole is TrophicRole.Predator or TrophicRole.Apex))
        {
            List<int> supportedRegions = predator.InitialRangeRegionIds
                .Where(herbivoreCoveredRegions.Contains)
                .Distinct()
                .ToList();

            if (supportedRegions.Count == 0)
            {
                Region? fallbackRegion = world.Regions
                    .Where(region => herbivoreCoveredRegions.Contains(region.Id))
                    .OrderByDescending(region => ScoreRegionForSpecies(predator, region))
                    .FirstOrDefault();
                if (fallbackRegion is not null)
                {
                    supportedRegions.Add(fallbackRegion.Id);
                }
            }

            predator.InitialRangeRegionIds.Clear();
            foreach (int regionId in supportedRegions)
            {
                predator.InitialRangeRegionIds.Add(regionId);
            }
        }
    }

    private void GeneratePolities(World world)
    {
        List<Species> sapientSpecies = world.Species.Where(species => species.IsSapient).OrderBy(species => species.Name, StringComparer.Ordinal).ToList();
        List<int> occupiedRegionIds = [];

        for (int polityIndex = 0; polityIndex < _settings.InitialPolityCount; polityIndex++)
        {
            Species species = sapientSpecies[polityIndex % sapientSpecies.Count];
            Region region = SelectStartingPolityRegion(world, species, occupiedRegionIds);
            occupiedRegionIds.Add(region.Id);

            int population = _random.Next(42, 91);
            Polity polity = new(polityIndex, NextPolityName(polityIndex), species.Id, region.Id, population, lineageId: polityIndex)
            {
                FoodStores = population * (1.20 + (_random.NextDouble() * 0.85)),
                YearsSinceFounded = _random.Next(0, 4),
                YearsInCurrentRegion = _random.Next(1, 6)
            };

            if (_settings.StartPolitiesWithHomeSettlements)
            {
                Settlement settlement = polity.EstablishFirstSettlement(region.Id, $"{region.Name} Hearth");
                settlement.YearsEstablished = _settings.StartingSettlementAgeYears;
                polity.YearsSinceFirstSettlement = _settings.StartingSettlementAgeYears;
            }

            world.Polities.Add(polity);
        }
    }

    private Region SelectStartingPolityRegion(World world, Species species, IReadOnlyCollection<int> occupiedRegionIds)
    {
        List<Region> spacedCandidates = world.Regions
            .Where(region => species.InitialRangeRegionIds.Contains(region.Id))
            .Where(region => !occupiedRegionIds.Contains(region.Id))
            .Where(region => IsFarEnoughFromOccupiedRegions(world, region.Id, occupiedRegionIds))
            .OrderByDescending(region => ScoreStartingPolityRegion(species, region))
            .ThenBy(region => region.Id)
            .ToList();

        if (spacedCandidates.Count > 0)
        {
            return spacedCandidates[0];
        }

        return world.Regions
            .Where(region => !occupiedRegionIds.Contains(region.Id))
            .OrderByDescending(region => ScoreStartingPolityRegion(species, region))
            .ThenBy(region => region.Id)
            .First();
    }

    private bool IsFarEnoughFromOccupiedRegions(World world, int regionId, IReadOnlyCollection<int> occupiedRegionIds)
        => occupiedRegionIds.All(occupiedRegionId => FindRegionDistance(world, regionId, occupiedRegionId) >= _settings.MinimumStartingPolityRegionSpacing);

    private IEnumerable<int> BuildClusteredRange(
        World world,
        IReadOnlyList<(Region Region, double Score)> viableRegions,
        int seedRegionId,
        int targetRangeSize)
    {
        HashSet<int> selected = [seedRegionId];
        Queue<int> frontier = new();
        frontier.Enqueue(seedRegionId);

        while (frontier.Count > 0 && selected.Count < targetRangeSize)
        {
            int sourceId = frontier.Dequeue();
            Region sourceRegion = world.Regions[sourceId];

            foreach (int neighborId in sourceRegion.ConnectedRegionIds
                         .OrderByDescending(id => viableRegions.FirstOrDefault(entry => entry.Region.Id == id).Score)
                         .ThenBy(id => id))
            {
                if (selected.Count >= targetRangeSize)
                {
                    break;
                }

                if (!viableRegions.Any(entry => entry.Region.Id == neighborId) || !selected.Add(neighborId))
                {
                    continue;
                }

                frontier.Enqueue(neighborId);
            }
        }

        foreach ((Region region, _) in viableRegions)
        {
            if (selected.Count >= targetRangeSize)
            {
                break;
            }

            selected.Add(region.Id);
        }

        return selected;
    }

    private int FindRegionDistance(World world, int startRegionId, int targetRegionId)
    {
        if (startRegionId == targetRegionId)
        {
            return 0;
        }

        Queue<(int RegionId, int Distance)> frontier = new();
        HashSet<int> visited = [startRegionId];
        frontier.Enqueue((startRegionId, 0));

        while (frontier.Count > 0)
        {
            (int regionId, int distance) = frontier.Dequeue();
            foreach (int neighborId in world.Regions[regionId].ConnectedRegionIds)
            {
                if (!visited.Add(neighborId))
                {
                    continue;
                }

                if (neighborId == targetRegionId)
                {
                    return distance + 1;
                }

                frontier.Enqueue((neighborId, distance + 1));
            }
        }

        return int.MaxValue;
    }

    private List<int>? BuildRangeExpansionPath(World world, Species species, int targetRegionId)
    {
        if (species.InitialRangeRegionIds.Contains(targetRegionId))
        {
            return [];
        }

        HashSet<int> occupied = species.InitialRangeRegionIds.ToHashSet();
        Queue<int> frontier = new();
        Dictionary<int, int?> parents = new();

        foreach (int seedRegionId in occupied)
        {
            frontier.Enqueue(seedRegionId);
            parents[seedRegionId] = null;
        }

        while (frontier.Count > 0)
        {
            int currentRegionId = frontier.Dequeue();
            if (currentRegionId == targetRegionId)
            {
                break;
            }

            foreach (int neighborId in world.Regions[currentRegionId].ConnectedRegionIds
                         .OrderByDescending(id => ScoreRegionForSpecies(species, world.Regions[id]))
                         .ThenBy(id => id))
            {
                if (parents.ContainsKey(neighborId))
                {
                    continue;
                }

                if (ScoreRegionForSpecies(species, world.Regions[neighborId]) < ResolveRangeThreshold(species))
                {
                    continue;
                }

                parents[neighborId] = currentRegionId;
                frontier.Enqueue(neighborId);
            }
        }

        if (!parents.ContainsKey(targetRegionId))
        {
            return null;
        }

        List<int> path = [];
        int? cursor = targetRegionId;
        while (cursor is not null)
        {
            int regionId = cursor.Value;
            if (!occupied.Contains(regionId))
            {
                path.Add(regionId);
            }

            cursor = parents[regionId];
        }

        path.Reverse();
        return path;
    }

    private void ApplyRegionProfile(Region region, RegionBiome biome)
    {
        (double fertilityMin, double fertilityMax, double waterMin, double waterMax, int plantMin, int plantMax, int animalMin, int animalMax) = biome switch
        {
            RegionBiome.Coast => (0.46, 0.72, 0.78, 0.98, 760, 1120, 260, 420),
            RegionBiome.RiverValley => (0.74, 0.95, 0.78, 0.96, 980, 1320, 320, 500),
            RegionBiome.Plains => (0.58, 0.82, 0.42, 0.66, 760, 1080, 230, 380),
            RegionBiome.Forest => (0.54, 0.80, 0.60, 0.84, 900, 1260, 220, 360),
            RegionBiome.Highlands => (0.34, 0.60, 0.34, 0.58, 520, 860, 180, 320),
            RegionBiome.Mountains => (0.18, 0.42, 0.20, 0.42, 340, 620, 160, 300),
            RegionBiome.Wetlands => (0.60, 0.84, 0.76, 0.96, 880, 1220, 240, 360),
            RegionBiome.Drylands => (0.18, 0.40, 0.12, 0.28, 260, 520, 150, 280),
            _ => (0.50, 0.70, 0.40, 0.60, 680, 1000, 220, 360)
        };

        region.Fertility = Roll(fertilityMin, fertilityMax);
        region.WaterAvailability = Roll(waterMin, waterMax);
        region.MaxPlantBiomass = _random.Next(plantMin, plantMax + 1);
        region.MaxAnimalBiomass = _random.Next(animalMin, animalMax + 1);
        region.PlantBiomass = region.MaxPlantBiomass * Roll(0.52, 0.72);
        region.AnimalBiomass = region.MaxAnimalBiomass * Roll(0.48, 0.68);
    }

    private double ScoreStartingPolityRegion(Species species, Region region)
    {
        int accessibleSupportSpecies = CountAccessibleHomelandSupportSpecies(species, region);

        return ScoreRegionForSpecies(species, region)
            + (region.Fertility * 0.45)
            + (region.WaterAvailability * 0.40)
            + (region.CarryingCapacity / 180.0)
            + (Math.Min(4, region.ConnectedRegionIds.Count) * 0.05)
            + (accessibleSupportSpecies * 0.22)
            + (accessibleSupportSpecies >= _settings.MinimumAccessibleHomelandSupportSpecies ? 0.18 : 0.0)
            + ResolveBiomeSettlementBonus(region.Biome);
    }

    private int CountAccessibleHomelandSupportSpecies(Species sapientSpecies, Region region)
    {
        if (_speciesById is null || sapientSpecies.DietSpeciesIds.Count == 0)
        {
            return 0;
        }

        HashSet<int> accessibleRegionIds = CollectRegionsWithinRadius(region, _settings.HomelandSupportRadius);
        int supportSpeciesCount = 0;

        foreach (int supportSpeciesId in sapientSpecies.DietSpeciesIds)
        {
            if (!_speciesById.TryGetValue(supportSpeciesId, out Species? supportCandidate) || supportCandidate.IsSapient)
            {
                continue;
            }

            if (supportCandidate.InitialRangeRegionIds.Any(accessibleRegionIds.Contains))
            {
                supportSpeciesCount++;
            }
        }

        return supportSpeciesCount;
    }

    private HashSet<int> CollectRegionsWithinRadius(Region seedRegion, int radius)
    {
        if (_regionsById is null)
        {
            return [seedRegion.Id];
        }

        HashSet<int> visited = [seedRegion.Id];
        if (radius <= 0)
        {
            return visited;
        }

        Queue<(int RegionId, int Distance)> frontier = new();
        frontier.Enqueue((seedRegion.Id, 0));

        while (frontier.Count > 0)
        {
            (int regionId, int distance) = frontier.Dequeue();
            if (distance >= radius)
            {
                continue;
            }

            if (!_regionsById.TryGetValue(regionId, out Region? currentRegion))
            {
                continue;
            }

            foreach (int neighborId in currentRegion.ConnectedRegionIds)
            {
                if (!visited.Add(neighborId))
                {
                    continue;
                }

                frontier.Enqueue((neighborId, distance + 1));
            }
        }

        return visited;
    }

    private static double ScoreRegionForSpecies(Species species, Region region)
    {
        double fertilityFit = 1.0 - Math.Abs(region.Fertility - species.FertilityPreference);
        double waterFit = 1.0 - Math.Abs(region.WaterAvailability - species.WaterPreference);
        double biomassFit = Math.Clamp(
            (region.MaxPlantBiomass / 1300.0 * species.PlantBiomassAffinity) +
            (region.MaxAnimalBiomass / 520.0 * species.AnimalBiomassAffinity),
            0.0,
            1.5);
        double biomeFit = species.PreferredBiomes.Count == 0
            ? 1.0
            : species.PreferredBiomes.Contains(region.Biome) ? 1.0 : 0.45;

        return Math.Clamp((fertilityFit * 0.30) + (waterFit * 0.25) + (biomeFit * 0.20) + (biomassFit * 0.25), 0.0, 1.5);
    }

    private static bool IsFertileFaunaTarget(Region region)
        => ResolveFertileFaunaScore(region) >= 0.70;

    private static double ResolveFertileFaunaScore(Region region)
    {
        double plantCapacityFit = Math.Clamp(region.MaxPlantBiomass / 1200.0, 0.0, 1.0);
        return (region.Fertility * 0.42)
            + (region.WaterAvailability * 0.24)
            + (plantCapacityFit * 0.34);
    }

    private static double ResolveBiomeSettlementBonus(RegionBiome biome)
        => biome switch
        {
            RegionBiome.RiverValley => 0.24,
            RegionBiome.Coast => 0.16,
            RegionBiome.Plains => 0.14,
            RegionBiome.Forest => 0.09,
            RegionBiome.Wetlands => 0.05,
            RegionBiome.Highlands => -0.02,
            RegionBiome.Mountains => -0.10,
            RegionBiome.Drylands => -0.08,
            _ => 0.0
        };

    private int ResolveTargetRangeSize(Species species, int viableRegionCount)
    {
        int desired = species.TrophicRole switch
        {
            TrophicRole.Producer => _random.Next(8, 15),
            TrophicRole.Herbivore => _random.Next(6, 11),
            TrophicRole.Omnivore => _random.Next(5, 9),
            TrophicRole.Predator => _random.Next(4, 6),
            TrophicRole.Apex => _random.Next(3, 5),
            _ => _random.Next(4, 8)
        };

        if (species.IsSapient)
        {
            desired = Math.Max(desired, 6);
        }

        return Math.Clamp(desired, 1, viableRegionCount);
    }

    private static double ResolveRangeThreshold(Species species)
        => species.TrophicRole switch
        {
            TrophicRole.Producer => 0.56,
            TrophicRole.Herbivore => 0.60,
            TrophicRole.Omnivore => 0.61,
            TrophicRole.Predator => 0.64,
            TrophicRole.Apex => 0.68,
            _ => 0.60
        };

    private double Roll(double min, double max)
        => min + (_random.NextDouble() * (max - min));

    private void ValidateSettings()
    {
        if (_settings.RegionCount != _settings.ContinentWidth * _settings.ContinentHeight)
        {
            throw new InvalidOperationException("World generation settings require RegionCount to match ContinentWidth * ContinentHeight.");
        }

        if (_settings.InitialSpeciesCount > _speciesTemplates.Count)
        {
            throw new InvalidOperationException("InitialSpeciesCount exceeds the available species templates.");
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
}
