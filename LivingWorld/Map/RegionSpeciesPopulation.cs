namespace LivingWorld.Map;

public sealed class RegionSpeciesPopulation
{
    public int SpeciesId { get; }
    public int RegionId { get; }
    public int PopulationCount { get; set; }
    public int CarryingCapacity { get; set; }
    public double HabitatSuitability { get; set; }
    public double MigrationPressure { get; set; }
    public double RecentPredationPressure { get; set; }
    public double RecentHuntingPressure { get; set; }
    public double RecentFoodStress { get; set; }
    public int SeasonsUnderPressure { get; set; }
    public bool EstablishedThisSeason { get; set; }

    public RegionSpeciesPopulation(int speciesId, int regionId, int populationCount)
    {
        SpeciesId = speciesId;
        RegionId = regionId;
        PopulationCount = populationCount;
    }

    public bool IsLocallyExtinct => PopulationCount <= 0;
}
