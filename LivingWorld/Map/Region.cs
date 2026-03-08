
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

    public Region(int id, string name)
    {
        Id = id;
        Name = name;
    }
}
