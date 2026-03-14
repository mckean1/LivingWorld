using LivingWorld.Life;
using LivingWorld.Map;

namespace LivingWorld.Generation;

public sealed record PrimitiveLineageTemplate(
    string Id,
    string Name,
    TrophicRole TrophicRole,
    string EcologyNiche,
    double TemperaturePreference,
    double TemperatureTolerance,
    double MoisturePreference,
    double MoistureTolerance,
    double FertilityPreference,
    double WaterPreference,
    double PlantBiomassAffinity,
    double AnimalBiomassAffinity,
    double BaseCarryingCapacityFactor,
    double BaseReproductionRate,
    double BaseDeclineRate,
    double MigrationCapability,
    double ExpansionPressure,
    double Resilience,
    double StartingSpreadWeight,
    double MutationPotential,
    double SentiencePotential,
    IReadOnlyCollection<RegionBiome> PreferredBiomes,
    IReadOnlyCollection<string> DietTemplateIds,
    double MeatYield = 0.0,
    double HuntingDifficulty = 0.0,
    double HuntingDanger = 0.0);
