using LivingWorld.Advancement;

namespace LivingWorld.Societies;

public sealed class Polity
{
    public int Id { get; }
    public string Name { get; }
    public int SpeciesId { get; set; }
    public int RegionId { get; set; }
    public int Population { get; set; }
    public int? ParentPolityId { get; set; }
    public PolityStage Stage { get; set; }

    public int YearsSinceFounded { get; set; }
    public int YearsInCurrentRegion { get; set; }

    public double FoodStores { get; set; }
    public double MigrationPressure { get; set; }
    public double FragmentationPressure { get; set; }
    public int FoodStressYears { get; set; }
    public int SplitCooldownYears { get; set; }

    // Migration tracking
    public int PreviousRegionId { get; set; }
    public bool MovedThisYear { get; set; }
    public int MovesThisYear { get; set; }

    // Monthly food tracking
    public double FoodGatheredThisMonth { get; set; }
    public double FoodConsumedThisMonth { get; set; }
    public double FoodNeededThisMonth { get; set; }
    public double FoodShortageThisMonth { get; set; }
    public double FoodSurplusThisMonth { get; set; }
    public double FoodSatisfactionThisMonth { get; set; }

    // Annual food tracking
    public double AnnualFoodNeeded { get; set; }
    public double AnnualFoodConsumed { get; set; }
    public double AnnualFoodShortage { get; set; }

    // Ongoing stress
    public int StarvationMonthsThisYear { get; set; }

    public SettlementStatus SettlementStatus { get; set; }
    public int SettlementCount { get; set; }
    public int YearsSinceFirstSettlement { get; set; }

    public HashSet<AdvancementId> Advancements { get; }
    public PolityCapabilities Capabilities { get; private set; }
    public bool HasSettlements => SettlementCount > 0;

    public Polity(
        int id,
        string name,
        int speciesId,
        int regionId,
        int population,
        int? parentPolityId = null,
        PolityStage stage = PolityStage.Band)
    {
        Id = id;
        Name = name;
        SpeciesId = speciesId;
        RegionId = regionId;
        Population = population;
        ParentPolityId = parentPolityId;
        Stage = stage;

        YearsSinceFounded = 0;
        YearsInCurrentRegion = 0;

        FoodStores = 0;
        MigrationPressure = 0;
        FragmentationPressure = 0;
        FoodStressYears = 0;
        SplitCooldownYears = 0;

        PreviousRegionId = regionId;
        MovedThisYear = false;
        MovesThisYear = 0;

        FoodGatheredThisMonth = 0;
        FoodConsumedThisMonth = 0;
        FoodNeededThisMonth = 0;
        FoodShortageThisMonth = 0;
        FoodSurplusThisMonth = 0;
        FoodSatisfactionThisMonth = 1.0;

        AnnualFoodNeeded = 0;
        AnnualFoodConsumed = 0;
        AnnualFoodShortage = 0;
        StarvationMonthsThisYear = 0;

        ClearSettlementState();
        Advancements = new HashSet<AdvancementId>();
        Capabilities = PolityCapabilities.FromAdvancements(Advancements);
    }

    public void ResetAnnualFoodStats()
    {
        AnnualFoodNeeded = 0;
        AnnualFoodConsumed = 0;
        AnnualFoodShortage = 0;
        StarvationMonthsThisYear = 0;
        MovedThisYear = false;
        MovesThisYear = 0;
    }

    public bool HasAdvancement(AdvancementId advancementId)
        => Advancements.Contains(advancementId);

    public bool DiscoverAdvancement(AdvancementId advancementId)
    {
        if (!Advancements.Add(advancementId))
        {
            return false;
        }

        RefreshCapabilities();
        return true;
    }

    public void InheritAdvancements(IEnumerable<AdvancementId> advancements)
    {
        bool changed = false;
        foreach (AdvancementId advancement in advancements)
        {
            changed |= Advancements.Add(advancement);
        }

        if (changed)
        {
            RefreshCapabilities();
        }
    }

    public void RefreshCapabilities()
        => Capabilities = PolityCapabilities.FromAdvancements(Advancements);

    public void ClearSettlementState()
    {
        SettlementStatus = SettlementStatus.Nomadic;
        SettlementCount = 0;
        YearsSinceFirstSettlement = 0;
    }
}
