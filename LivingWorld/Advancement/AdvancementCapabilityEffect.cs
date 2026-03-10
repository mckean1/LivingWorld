namespace LivingWorld.Advancement;

public sealed class AdvancementCapabilityEffect
{
    public bool EnablesFarming { get; init; }
    public double HarvestEfficiencyBonus { get; init; }
    public double FoodSpoilageMultiplier { get; init; } = 1.0;
    public double FoodNeedMultiplier { get; init; } = 1.0;
    public double FarmingYieldPerPerson { get; init; }
    public double TravelCostMultiplier { get; init; } = 1.0;
    public double TradeEfficiencyBonus { get; init; }
    public double MilitaryPowerBonus { get; init; }
}
