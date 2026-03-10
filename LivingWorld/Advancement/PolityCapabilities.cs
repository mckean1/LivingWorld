namespace LivingWorld.Advancement;

public sealed class PolityCapabilities
{
    public bool CanFarm { get; private set; }
    public double HarvestEfficiencyMultiplier { get; private set; } = 1.0;
    public double FoodSpoilageMultiplier { get; private set; } = 1.0;
    public double FoodNeedMultiplier { get; private set; } = 1.0;
    public double FarmingYieldPerPerson { get; private set; }
    public double TravelCostMultiplier { get; private set; } = 1.0;
    public double TradeEfficiencyMultiplier { get; private set; } = 1.0;
    public double MilitaryPowerMultiplier { get; private set; } = 1.0;

    public static PolityCapabilities FromAdvancements(IEnumerable<AdvancementId> advancements)
    {
        PolityCapabilities capabilities = new();

        foreach (AdvancementId advancementId in advancements.OrderBy(id => id))
        {
            AdvancementDefinition definition = AdvancementCatalog.Get(advancementId);
            foreach (AdvancementCapabilityEffect effect in definition.CapabilityEffects)
            {
                capabilities.Apply(effect);
            }
        }

        capabilities.Normalize();
        return capabilities;
    }

    private void Apply(AdvancementCapabilityEffect effect)
    {
        CanFarm |= effect.EnablesFarming;
        HarvestEfficiencyMultiplier += effect.HarvestEfficiencyBonus;
        FoodSpoilageMultiplier *= effect.FoodSpoilageMultiplier;
        FoodNeedMultiplier *= effect.FoodNeedMultiplier;
        FarmingYieldPerPerson += effect.FarmingYieldPerPerson;
        TravelCostMultiplier *= effect.TravelCostMultiplier;
        TradeEfficiencyMultiplier += effect.TradeEfficiencyBonus;
        MilitaryPowerMultiplier += effect.MilitaryPowerBonus;
    }

    private void Normalize()
    {
        HarvestEfficiencyMultiplier = Math.Clamp(HarvestEfficiencyMultiplier, 0.50, 2.50);
        FoodSpoilageMultiplier = Math.Clamp(FoodSpoilageMultiplier, 0.20, 1.25);
        FoodNeedMultiplier = Math.Clamp(FoodNeedMultiplier, 0.70, 1.10);
        FarmingYieldPerPerson = Math.Clamp(FarmingYieldPerPerson, 0.0, 0.40);
        TravelCostMultiplier = Math.Clamp(TravelCostMultiplier, 0.40, 1.60);
        TradeEfficiencyMultiplier = Math.Clamp(TradeEfficiencyMultiplier, 0.50, 2.50);
        MilitaryPowerMultiplier = Math.Clamp(MilitaryPowerMultiplier, 0.50, 2.50);
    }
}
