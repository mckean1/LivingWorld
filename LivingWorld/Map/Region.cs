namespace LivingWorld.Map;

public sealed class Region
{
    public int Id { get; }
    public string Name { get; }

    public double Fertility { get; set; }
    public double WaterAvailability { get; set; }

    public double PlantBiomass { get; set; }
    public double AnimalBiomass { get; set; }

    public double MaxPlantBiomass { get; set; }
    public double MaxAnimalBiomass { get; set; }

    public List<int> ConnectedRegionIds { get; } = new();
    public List<RegionSpeciesPopulation> SpeciesPopulations { get; } = new();

    public double CarryingCapacity => 20.0 +
        (Fertility * 80.0) +
        (WaterAvailability * 60.0) +
        ((MaxPlantBiomass + MaxAnimalBiomass) / 20.0);

    public Region(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public double TotalBiomass => PlantBiomass + AnimalBiomass;
    public double TotalBiomassCapacity => MaxPlantBiomass + MaxAnimalBiomass;

    public void AddConnection(int regionId)
    {
        if (regionId == Id)
            return;

        if (!ConnectedRegionIds.Contains(regionId))
            ConnectedRegionIds.Add(regionId);
    }

    public RegionSpeciesPopulation? GetSpeciesPopulation(int speciesId)
        => SpeciesPopulations.FirstOrDefault(population => population.SpeciesId == speciesId);

    public RegionSpeciesPopulation GetOrCreateSpeciesPopulation(int speciesId)
    {
        RegionSpeciesPopulation? existing = GetSpeciesPopulation(speciesId);
        if (existing is not null)
        {
            return existing;
        }

        RegionSpeciesPopulation created = new(speciesId, Id, 0);
        SpeciesPopulations.Add(created);
        return created;
    }
}
