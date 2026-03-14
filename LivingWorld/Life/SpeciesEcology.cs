using LivingWorld.Core;
using LivingWorld.Map;

namespace LivingWorld.Life;

public static class SpeciesEcology
{
    public static double CalculateBaseHabitatSuitability(Species species, Region region)
    {
        RegionEcologyProfile ecology = region.EffectiveEcologyProfile;
        double fertilityFit = 1.0 - Math.Abs(region.Fertility - species.FertilityPreference);
        double waterFit = 1.0 - Math.Abs(region.WaterAvailability - species.WaterPreference);
        double temperatureFit = ResolveToleranceFit(ecology.Temperature, species.TemperaturePreference, species.TemperatureTolerance);
        double moistureFit = ResolveToleranceFit(ecology.Moisture, species.MoisturePreference, species.MoistureTolerance);
        double biomassFit = Math.Clamp(
            (region.MaxPlantBiomass / 1000.0 * species.PlantBiomassAffinity) +
            (region.MaxAnimalBiomass / 400.0 * species.AnimalBiomassAffinity),
            0.0,
            1.4);
        double productivityFit = species.TrophicRole switch
        {
            TrophicRole.Producer => ecology.BasePrimaryProductivity,
            TrophicRole.Herbivore => (ecology.BasePrimaryProductivity * 0.75) + (ecology.HabitabilityScore * 0.25),
            TrophicRole.Omnivore => (ecology.BasePrimaryProductivity * 0.45) + (ecology.HabitabilityScore * 0.35) + (Math.Clamp(region.MaxAnimalBiomass / 400.0, 0.0, 1.0) * 0.20),
            _ => (Math.Clamp(region.MaxAnimalBiomass / 400.0, 0.0, 1.0) * 0.70) + (ecology.HabitabilityScore * 0.30)
        };
        double biomeFit = species.PreferredBiomes.Count == 0
            ? 1.0
            : species.PreferredBiomes.Contains(region.Biome) ? 1.05 : 0.45;
        double harshnessPenalty = Math.Max(0.0, (ecology.TerrainHarshness * 0.18) + (ecology.EnvironmentalVolatility * (0.22 - (species.Resilience * 0.12))));

        return Math.Clamp(
            (fertilityFit * 0.16) +
            (waterFit * 0.12) +
            (temperatureFit * 0.16) +
            (moistureFit * 0.16) +
            (biomassFit * 0.18) +
            (productivityFit * 0.12) +
            (biomeFit * 0.10) +
            (species.Resilience * 0.06) -
            harshnessPenalty,
            0.03,
            1.25);
    }

    public static double CalculateHabitatSuitability(Species species, RegionSpeciesPopulation population, double baseSuitability)
        => PopulationTraitResolver.AdjustHabitatSuitability(species, population, baseSuitability);

    public static int CalculateCarryingCapacity(Species species, RegionSpeciesPopulation population, Region region, double suitability)
    {
        double baseCapacity = species.TrophicRole switch
        {
            TrophicRole.Producer => 140 + (region.EffectiveEcologyProfile.BasePrimaryProductivity * 180.0) + (region.MaxPlantBiomass * 0.22),
            TrophicRole.Herbivore => 40 + (region.MaxPlantBiomass * 0.10) + (region.EffectiveEcologyProfile.HabitabilityScore * 90.0),
            TrophicRole.Omnivore => 28 + (region.TotalBiomassCapacity * 0.038) + (region.EffectiveEcologyProfile.HabitabilityScore * 36.0),
            TrophicRole.Predator => 12 + (region.MaxAnimalBiomass * 0.024) + (region.EffectiveEcologyProfile.HabitabilityScore * 18.0),
            TrophicRole.Apex => 8 + (region.MaxAnimalBiomass * 0.014) + (region.EffectiveEcologyProfile.HabitabilityScore * 12.0),
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

    private static double ResolveToleranceFit(double value, double preference, double tolerance)
    {
        double normalizedTolerance = Math.Max(0.08, tolerance);
        return Math.Clamp(1.0 - (Math.Abs(value - preference) / normalizedTolerance), 0.0, 1.0);
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
