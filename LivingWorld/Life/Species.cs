
namespace LivingWorld.Life;

public sealed class Species
{
    public int Id { get; }
    public string Name { get; }

    public double Intelligence { get; }
    public double Cooperation { get; }

    public Species(int id, string name, double intelligence, double cooperation)
    {
        Id = id;
        Name = name;
        Intelligence = intelligence;
        Cooperation = cooperation;
    }
}
