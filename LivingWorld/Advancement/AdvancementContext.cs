using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Advancement;

public sealed class AdvancementContext
{
    public World World { get; }
    public Polity Polity { get; }
    public Region Region { get; }
    public Species Species { get; }
    public double AnnualFoodRatio { get; }
    public double ReserveMonths { get; }
    public double CrowdingRatio { get; }
    public double LocalPopulationRatio { get; }
    public double FoodStressRatio { get; }
    public bool IsMobile { get; }

    public AdvancementContext(
        World world,
        Polity polity,
        Region region,
        Species species,
        double annualFoodRatio,
        double reserveMonths,
        double crowdingRatio,
        double localPopulationRatio,
        double foodStressRatio,
        bool isMobile)
    {
        World = world;
        Polity = polity;
        Region = region;
        Species = species;
        AnnualFoodRatio = annualFoodRatio;
        ReserveMonths = reserveMonths;
        CrowdingRatio = crowdingRatio;
        LocalPopulationRatio = localPopulationRatio;
        FoodStressRatio = foodStressRatio;
        IsMobile = isMobile;
    }
}
