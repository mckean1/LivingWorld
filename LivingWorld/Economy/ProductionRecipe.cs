using LivingWorld.Advancement;

namespace LivingWorld.Economy;

public sealed record ProductionRecipe(
    string Id,
    MaterialType Output,
    double OutputAmount,
    IReadOnlyDictionary<MaterialType, double> Inputs,
    IReadOnlyCollection<AdvancementId> RequiredAdvancements)
{
    public bool IsAvailable(IReadOnlyCollection<AdvancementId> advancements)
        => RequiredAdvancements.All(advancements.Contains);
}
