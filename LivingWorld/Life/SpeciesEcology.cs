using LivingWorld.Core;
using LivingWorld.Map;

namespace LivingWorld.Life;

public static class SpeciesEcology
{
    public static double CalculateBaseHabitatSuitability(Species species, Region region)
    {
        double fertilityFit = 1.0 - Math.Abs(region.Fertility - species.FertilityPreference);
        double waterFit = 1.0 - Math.Abs(region.WaterAvailability - species.WaterPreference);
        double biomassFit = Math.Clamp(
            (region.MaxPlantBiomass / 1000.0 * species.PlantBiomassAffinity) +
            (region.MaxAnimalBiomass / 400.0 * species.AnimalBiomassAffinity),
            0.0,
            1.4);
        double biomeFit = species.PreferredBiomes.Count == 0
            ? 1.0
            : species.PreferredBiomes.Contains(region.Biome) ? 1.05 : 0.45;

        return Math.Clamp((fertilityFit * 0.30) + (waterFit * 0.22) + (biomassFit * 0.33) + (biomeFit * 0.15), 0.03, 1.25);
    }

    public static double CalculateHabitatSuitability(Species species, RegionSpeciesPopulation population, double baseSuitability)
        => PopulationTraitResolver.AdjustHabitatSuitability(species, population, baseSuitability);

    public static int CalculateCarryingCapacity(Species species, RegionSpeciesPopulation population, Region region, double suitability)
    {
        double baseCapacity = species.TrophicRole switch
        {
            TrophicRole.Producer => 180 + (region.MaxPlantBiomass * 0.35),
            TrophicRole.Herbivore => 60 + (region.MaxPlantBiomass * 0.11) + (region.Fertility * 70) + (region.WaterAvailability * 40),
            TrophicRole.Omnivore => 32 + (region.TotalBiomassCapacity * 0.045),
            TrophicRole.Predator => 18 + (region.MaxAnimalBiomass * 0.030),
            TrophicRole.Apex => 9 + (region.MaxAnimalBiomass * 0.018),
            _ => 24
        };

        double dietFlexibility = PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.DietFlexibility);
        double climateTolerance = PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.ClimateTolerance);
        double size = PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Size);
        double capacityFactor = 0.84 + (dietFlexibility * 0.16) + (climateTolerance * 0.10) - (size * 0.12);
        return Math.Max(0, (int)Math.Round(baseCapacity * suitability * species.BaseCarryingCapacityFactor * capacityFactor));
    }

    public static int CalculateInitialPopulation(Species species, int carryingCapacity, double habitatSuitability)
    {
        if (carryingCapacity <= 0 || habitatSuitability < 0.12)
        {
            return 0;
        }

        double startingShare = species.TrophicRole switch
        {
            TrophicRole.Producer => 0.72,
            TrophicRole.Herbivore => 0.62,
            TrophicRole.Omnivore => 0.44,
            TrophicRole.Predator => 0.24,
            TrophicRole.Apex => 0.14,
            _ => 0.30
        };

        double establishmentFactor = 0.55 + (habitatSuitability * 0.45);
        return Math.Max(0, (int)Math.Round(carryingCapacity * startingShare * establishmentFactor));
    }

    public static IReadOnlyList<int> GetOccupiedRegionIds(World world, int speciesId)
        => world.Regions
            .Where(region => region.GetSpeciesPopulation(speciesId)?.PopulationCount > 0)
            .Select(region => region.Id)
            .OrderBy(id => id)
            .ToList();

    public static IReadOnlyDictionary<int, double> BuildSuitabilityMap(World world, Species species)
        => world.Regions.ToDictionary(
            region => region.Id,
            region =>
            {
                RegionSpeciesPopulation candidate = region.GetSpeciesPopulation(species.Id) ?? new RegionSpeciesPopulation(species.Id, region.Id, 0);
                double baseSuitability = CalculateBaseHabitatSuitability(species, region);
                return CalculateHabitatSuitability(species, candidate, baseSuitability);
            });
}
