
using LivingWorld.Map;
using LivingWorld.Life;
using LivingWorld.Societies;

namespace LivingWorld.Core;

public sealed class World
{
    public WorldTime Time { get; }

    public List<Region> Regions { get; } = new();
    public List<Species> Species { get; } = new();
    public List<Polity> Polities { get; } = new();

    public World(WorldTime time)
    {
        Time = time;
    }
}
