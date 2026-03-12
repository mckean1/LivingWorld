namespace LivingWorld.Societies;

public sealed class Settlement
{
    public int Id { get; }
    public int PolityId { get; }
    public int RegionId { get; set; }
    public string Name { get; set; }
    public double CultivatedLand { get; set; }
    public int YearsEstablished { get; set; }

    public Settlement(int id, int polityId, int regionId, string name)
    {
        Id = id;
        PolityId = polityId;
        RegionId = regionId;
        Name = name;
    }
}
