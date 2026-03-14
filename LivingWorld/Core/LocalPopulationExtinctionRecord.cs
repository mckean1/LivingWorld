namespace LivingWorld.Core;

public sealed record LocalPopulationExtinctionRecord(
    int RegionId,
    string RegionName,
    int SpeciesId,
    string SpeciesName,
    int Year,
    int Month,
    string Reason,
    int PopulationBeforeLoss,
    double StressScore,
    double HabitatSuitability);
