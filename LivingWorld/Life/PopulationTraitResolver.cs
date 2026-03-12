using LivingWorld.Map;

namespace LivingWorld.Life;

public static class PopulationTraitResolver
{
    public static double GetEffectiveTrait(Species species, RegionSpeciesPopulation population, SpeciesTrait trait)
        => Math.Clamp(GetBaselineTrait(species, trait) + population.GetTraitOffset(trait), 0.02, 1.35);

    public static double GetBaselineTrait(Species species, SpeciesTrait trait)
    {
        return trait switch
        {
            SpeciesTrait.Intelligence => Math.Clamp(species.Intelligence, 0.02, 1.0),
            SpeciesTrait.Sociality => Math.Clamp(species.Cooperation, 0.02, 1.0),
            SpeciesTrait.Aggression => ResolveAggression(species),
            SpeciesTrait.Endurance => ResolveEndurance(species),
            SpeciesTrait.Fertility => Math.Clamp(species.BaseReproductionRate / 0.16, 0.08, 1.0),
            SpeciesTrait.DietFlexibility => ResolveDietFlexibility(species),
            SpeciesTrait.ClimateTolerance => ResolveClimateTolerance(species),
            SpeciesTrait.Size => Math.Clamp(species.MeatYield / 30.0, 0.05, 1.1),
            _ => 0.5
        };
    }

    public static double GetEffectiveHuntingDifficulty(Species species, RegionSpeciesPopulation population)
    {
        double endurance = GetEffectiveTrait(species, population, SpeciesTrait.Endurance);
        double intelligence = GetEffectiveTrait(species, population, SpeciesTrait.Intelligence);
        double sociality = GetEffectiveTrait(species, population, SpeciesTrait.Sociality);
        double size = GetEffectiveTrait(species, population, SpeciesTrait.Size);

        return Math.Clamp(
            species.HuntingDifficulty +
            (endurance * 0.12) +
            (intelligence * 0.05) +
            (sociality * 0.06) +
            (Math.Max(0.0, size - 0.45) * 0.08),
            0.04,
            0.95);
    }

    public static double GetEffectiveHuntingDanger(Species species, RegionSpeciesPopulation population)
    {
        double aggression = GetEffectiveTrait(species, population, SpeciesTrait.Aggression);
        double sociality = GetEffectiveTrait(species, population, SpeciesTrait.Sociality);
        double size = GetEffectiveTrait(species, population, SpeciesTrait.Size);

        return Math.Clamp(
            species.HuntingDanger +
            (aggression * 0.18) +
            (sociality * 0.05) +
            (size * 0.10),
            0.0,
            0.98);
    }

    public static double GetEffectiveMeatYield(Species species, RegionSpeciesPopulation population)
    {
        double size = GetEffectiveTrait(species, population, SpeciesTrait.Size);
        return species.MeatYield * (0.72 + (size * 0.48));
    }

    public static double GetEffectiveMigrationCapability(Species species, RegionSpeciesPopulation population)
    {
        double endurance = GetEffectiveTrait(species, population, SpeciesTrait.Endurance);
        double climateTolerance = GetEffectiveTrait(species, population, SpeciesTrait.ClimateTolerance);
        double dietFlexibility = GetEffectiveTrait(species, population, SpeciesTrait.DietFlexibility);

        return Math.Clamp(
            species.MigrationCapability *
            (0.82 + (endurance * 0.22) + (climateTolerance * 0.18) + (dietFlexibility * 0.08)),
            0.0,
            1.0);
    }

    public static double AdjustHabitatSuitability(Species species, RegionSpeciesPopulation population, double baseSuitability)
    {
        double climateTolerance = GetEffectiveTrait(species, population, SpeciesTrait.ClimateTolerance);
        double dietFlexibility = GetEffectiveTrait(species, population, SpeciesTrait.DietFlexibility);
        double size = GetEffectiveTrait(species, population, SpeciesTrait.Size);
        double mismatch = Math.Max(0.0, 0.82 - baseSuitability);

        return Math.Clamp(
            baseSuitability +
            (mismatch * climateTolerance * 0.42) +
            (mismatch * dietFlexibility * 0.22) -
            (Math.Max(0.0, size - 0.55) * mismatch * 0.14),
            0.05,
            1.30);
    }

    public static double ResolvePreyDefense(Species species, RegionSpeciesPopulation population)
    {
        double endurance = GetEffectiveTrait(species, population, SpeciesTrait.Endurance);
        double sociality = GetEffectiveTrait(species, population, SpeciesTrait.Sociality);
        double aggression = GetEffectiveTrait(species, population, SpeciesTrait.Aggression);
        double size = GetEffectiveTrait(species, population, SpeciesTrait.Size);

        return 0.70 + (endurance * 0.18) + (sociality * 0.12) + (aggression * 0.06) + (size * 0.05);
    }

    public static double ResolvePredationOffense(Species species, RegionSpeciesPopulation population)
    {
        double aggression = GetEffectiveTrait(species, population, SpeciesTrait.Aggression);
        double endurance = GetEffectiveTrait(species, population, SpeciesTrait.Endurance);
        double intelligence = GetEffectiveTrait(species, population, SpeciesTrait.Intelligence);
        double sociality = GetEffectiveTrait(species, population, SpeciesTrait.Sociality);

        return (0.82 + (aggression * 0.20) + (endurance * 0.16) + (intelligence * 0.10) + (sociality * 0.06));
    }

    public static double ResolveReproductionModifier(Species species, RegionSpeciesPopulation population)
    {
        double fertility = GetEffectiveTrait(species, population, SpeciesTrait.Fertility);
        double endurance = GetEffectiveTrait(species, population, SpeciesTrait.Endurance);
        double dietFlexibility = GetEffectiveTrait(species, population, SpeciesTrait.DietFlexibility);
        double size = GetEffectiveTrait(species, population, SpeciesTrait.Size);

        return 0.82 + (fertility * 0.24) + (endurance * 0.08) + (dietFlexibility * 0.08) - (size * 0.08);
    }

    public static double ResolveDeclineModifier(Species species, RegionSpeciesPopulation population, double habitatSuitability)
    {
        double endurance = GetEffectiveTrait(species, population, SpeciesTrait.Endurance);
        double climateTolerance = GetEffectiveTrait(species, population, SpeciesTrait.ClimateTolerance);
        double size = GetEffectiveTrait(species, population, SpeciesTrait.Size);
        double mismatch = Math.Max(0.0, 0.80 - habitatSuitability);

        return Math.Max(
            0.55,
            1.0 -
            (endurance * 0.15) -
            (climateTolerance * 0.10) +
            (size * 0.08) +
            (mismatch * 0.12));
    }

    private static double ResolveAggression(Species species)
    {
        double trophicAggression = species.TrophicRole switch
        {
            TrophicRole.Producer => 0.04,
            TrophicRole.Herbivore => 0.18,
            TrophicRole.Omnivore => 0.36,
            TrophicRole.Predator => 0.58,
            TrophicRole.Apex => 0.72,
            _ => 0.28
        };

        return Math.Clamp(
            (trophicAggression * 0.60) +
            (species.HuntingDanger * 0.30) +
            ((1.0 - species.DomesticationAffinity) * 0.10),
            0.02,
            1.0);
    }

    private static double ResolveEndurance(Species species)
    {
        double seasonalResilience = Math.Clamp(
            1.0 - Math.Max(0.0, species.SpringReproductionModifier - species.WinterReproductionModifier) / 1.10,
            0.0,
            1.0);

        return Math.Clamp(
            (species.MigrationCapability * 0.36) +
            ((1.0 - Math.Clamp(species.BaseDeclineRate * 10.0, 0.0, 1.0)) * 0.34) +
            (Math.Clamp(species.MeatYield / 30.0, 0.0, 1.0) * 0.12) +
            (seasonalResilience * 0.18),
            0.05,
            1.0);
    }

    private static double ResolveDietFlexibility(Species species)
    {
        double dietBreadth = Math.Clamp(species.DietSpeciesIds.Count / 4.0, 0.0, 1.0);
        double mixedDiet = Math.Min(species.PlantBiomassAffinity, species.AnimalBiomassAffinity) * 1.5;
        double trophicBonus = species.TrophicRole switch
        {
            TrophicRole.Omnivore => 0.25,
            TrophicRole.Predator => 0.08,
            TrophicRole.Herbivore => 0.06,
            _ => 0.0
        };

        return Math.Clamp((dietBreadth * 0.50) + (mixedDiet * 0.30) + trophicBonus + 0.10, 0.05, 1.0);
    }

    private static double ResolveClimateTolerance(Species species)
    {
        double seasonalResilience = Math.Clamp(
            species.WinterReproductionModifier / Math.Max(0.1, species.SpringReproductionModifier),
            0.0,
            1.0);

        return Math.Clamp(
            0.20 +
            (species.MigrationCapability * 0.22) +
            ((1.0 - Math.Clamp(species.BaseDeclineRate * 10.0, 0.0, 1.0)) * 0.20) +
            (seasonalResilience * 0.24) +
            (species.BaseCarryingCapacityFactor * 0.12),
            0.05,
            1.0);
    }
}
