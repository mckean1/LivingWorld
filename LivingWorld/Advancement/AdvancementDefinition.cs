using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Advancement;

public sealed class AdvancementDefinition
{
    public AdvancementId Id { get; }
    public string Name { get; }
    public string Description { get; }
    public IReadOnlyList<AdvancementId> Prerequisites { get; }
    public Func<AdvancementContext, double> DiscoveryChance { get; }
    public Action<World, Polity>? OnDiscovered { get; }

    public AdvancementDefinition(
        AdvancementId id,
        string name,
        string description,
        IEnumerable<AdvancementId>? prerequisites,
        Func<AdvancementContext, double> discoveryChance,
        Action<World, Polity>? onDiscovered = null)
    {
        Id = id;
        Name = name;
        Description = description;
        Prerequisites = prerequisites?.ToArray() ?? Array.Empty<AdvancementId>();
        DiscoveryChance = discoveryChance;
        OnDiscovered = onDiscovered;
    }
}
