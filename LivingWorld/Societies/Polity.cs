namespace LivingWorld.Societies;

public sealed class Polity
{
    public int Id { get; }
    public string Name { get; }
    public int SpeciesId { get; set; }
    public int RegionId { get; set; }
    public int Population { get; set; }
    public double FoodStores { get; set; }
    public double MigrationPressure { get; set; }
    public int PreviousRegionId { get; set; }
    public bool MovedThisYear { get; set; }
    public int MovesThisYear { get; set; }

    public Polity(int id, string name, int speciesId, int regionId, int population)
    {
        Id = id;
        Name = name;
        SpeciesId = speciesId;
        RegionId = regionId;
        Population = population;
        FoodStores = 0;
        MigrationPressure = 0;
        PreviousRegionId = regionId;
        MovedThisYear = false;
        MovesThisYear = 0;
    }
}