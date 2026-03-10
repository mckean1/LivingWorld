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
    public IReadOnlyList<AdvancementCapabilityEffect> CapabilityEffects { get; }
    public Func<Polity, string>? DiscoveryNarrative { get; }
    public Action<World, Polity>? OnDiscovered { get; }

    public AdvancementDefinition(
        AdvancementId id,
        string name,
        string description,
        IEnumerable<AdvancementId>? prerequisites,
        Func<AdvancementContext, double> discoveryChance,
        IEnumerable<AdvancementCapabilityEffect>? capabilityEffects = null,
        Func<Polity, string>? discoveryNarrative = null,
        Action<World, Polity>? onDiscovered = null)
    {
        Id = id;
        Name = name;
        Description = description;
        Prerequisites = prerequisites?.ToArray() ?? Array.Empty<AdvancementId>();
        DiscoveryChance = discoveryChance;
        CapabilityEffects = capabilityEffects?.ToArray() ?? Array.Empty<AdvancementCapabilityEffect>();
        DiscoveryNarrative = discoveryNarrative;
        OnDiscovered = onDiscovered;
    }
}
