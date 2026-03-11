using LivingWorld.Advancement;

namespace LivingWorld.Societies;

public sealed class Polity
{
    public int Id { get; }
    public string Name { get; }
    public int SpeciesId { get; set; }
    public int RegionId { get; set; }
    public int Population { get; set; }
    public int LineageId { get; set; }
    public int? ParentPolityId { get; set; }
    public PolityStage Stage { get; set; }

    public int YearsSinceFounded { get; set; }
    public int YearsInCurrentRegion { get; set; }

    public double FoodStores { get; set; }
    public double MigrationPressure { get; set; }
    public double EventDrivenMigrationPressureBonus { get; set; }
    public double FragmentationPressure { get; set; }
    public double EventDrivenFragmentationPressureBonus { get; set; }
    public double EventDrivenSettlementChanceBonus { get; set; }
    public int FoodStressYears { get; set; }
    public int SplitCooldownYears { get; set; }
    public int MigrationPressureBonusMonthsRemaining { get; set; }
    public int FragmentationPressureBonusMonthsRemaining { get; set; }
    public int SettlementChanceBonusMonthsRemaining { get; set; }
    public long? LastLearnedAgricultureEventId { get; set; }

    // Migration tracking
    public int PreviousRegionId { get; set; }
    public bool MovedThisYear { get; set; }
    public int MovesThisYear { get; set; }

    // Monthly food tracking
    public double FoodGatheredThisMonth { get; set; }
    public double FoodFarmedThisMonth { get; set; }
    public double FoodConsumedThisMonth { get; set; }
    public double FoodNeededThisMonth { get; set; }
    public double FoodShortageThisMonth { get; set; }
    public double FoodSurplusThisMonth { get; set; }
    public double FoodSatisfactionThisMonth { get; set; }

    // Annual food tracking
    public double AnnualFoodNeeded { get; set; }
    public double AnnualFoodConsumed { get; set; }
    public double AnnualFoodShortage { get; set; }
    public double AnnualFoodGathered { get; set; }
    public double AnnualFoodFarmed { get; set; }
    public double AnnualFoodImported { get; set; }
    public double AnnualFoodExported { get; set; }
    public double AnnualFoodImportedInternal { get; set; }
    public double AnnualFoodImportedExternal { get; set; }
    public int TradeReliefMonthsThisYear { get; set; }
    public int TradePartialReliefMonthsThisYear { get; set; }
    public int TradeFullReliefMonthsThisYear { get; set; }
    public int TradePartnerCountThisYear { get; set; }
    public double AnnualTradeNeedMitigated { get; set; }

    // Agriculture tracking
    public double CultivatedLand { get; set; }
    public double AnnualCultivatedLandTotal { get; set; }
    public int FarmingMonthsThisYear { get; set; }
    public double LastYearAverageCultivatedLand { get; set; }
    public int ConsecutiveFarmingYears { get; set; }
    public int AgricultureEventCooldownYears { get; set; }

    // Ongoing stress
    public int StarvationMonthsThisYear { get; set; }

    public SettlementStatus SettlementStatus { get; set; }
    public int SettlementCount { get; set; }
    public int YearsSinceFirstSettlement { get; set; }
    public FoodStateSummary? LastResolvedFoodState { get; set; }
    public int? LastResolvedFoodStateYear { get; set; }

    public HashSet<AdvancementId> Advancements { get; }
    public PolityCapabilities Capabilities { get; private set; }
    public bool HasSettlements => SettlementCount > 0;

    public Polity(
        int id,
        string name,
        int speciesId,
        int regionId,
        int population,
        int? lineageId = null,
        int? parentPolityId = null,
        PolityStage stage = PolityStage.Band)
    {
        Id = id;
        Name = name;
        SpeciesId = speciesId;
        RegionId = regionId;
        Population = population;
        LineageId = lineageId ?? id;
        ParentPolityId = parentPolityId;
        Stage = stage;

        YearsSinceFounded = 0;
        YearsInCurrentRegion = 0;

        FoodStores = 0;
        MigrationPressure = 0;
        EventDrivenMigrationPressureBonus = 0;
        FragmentationPressure = 0;
        EventDrivenFragmentationPressureBonus = 0;
        EventDrivenSettlementChanceBonus = 0;
        FoodStressYears = 0;
        SplitCooldownYears = 0;
        MigrationPressureBonusMonthsRemaining = 0;
        FragmentationPressureBonusMonthsRemaining = 0;
        SettlementChanceBonusMonthsRemaining = 0;
        LastLearnedAgricultureEventId = null;

        PreviousRegionId = regionId;
        MovedThisYear = false;
        MovesThisYear = 0;

        FoodGatheredThisMonth = 0;
        FoodFarmedThisMonth = 0;
        FoodConsumedThisMonth = 0;
        FoodNeededThisMonth = 0;
        FoodShortageThisMonth = 0;
        FoodSurplusThisMonth = 0;
        FoodSatisfactionThisMonth = 1.0;

        AnnualFoodNeeded = 0;
        AnnualFoodConsumed = 0;
        AnnualFoodShortage = 0;
        AnnualFoodGathered = 0;
        AnnualFoodFarmed = 0;
        AnnualFoodImported = 0;
        AnnualFoodExported = 0;
        AnnualFoodImportedInternal = 0;
        AnnualFoodImportedExternal = 0;
        TradeReliefMonthsThisYear = 0;
        TradePartialReliefMonthsThisYear = 0;
        TradeFullReliefMonthsThisYear = 0;
        TradePartnerCountThisYear = 0;
        AnnualTradeNeedMitigated = 0;
        StarvationMonthsThisYear = 0;

        CultivatedLand = 0;
        AnnualCultivatedLandTotal = 0;
        FarmingMonthsThisYear = 0;
        LastYearAverageCultivatedLand = 0;
        ConsecutiveFarmingYears = 0;
        AgricultureEventCooldownYears = 0;

        ClearSettlementState();
        LastResolvedFoodState = null;
        LastResolvedFoodStateYear = null;
        Advancements = new HashSet<AdvancementId>();
        Capabilities = PolityCapabilities.FromAdvancements(Advancements);
    }

    public void ResetAnnualFoodStats()
    {
        AnnualFoodNeeded = 0;
        AnnualFoodConsumed = 0;
        AnnualFoodShortage = 0;
        AnnualFoodGathered = 0;
        AnnualFoodFarmed = 0;
        AnnualFoodImported = 0;
        AnnualFoodExported = 0;
        AnnualFoodImportedInternal = 0;
        AnnualFoodImportedExternal = 0;
        TradeReliefMonthsThisYear = 0;
        TradePartialReliefMonthsThisYear = 0;
        TradeFullReliefMonthsThisYear = 0;
        TradePartnerCountThisYear = 0;
        AnnualTradeNeedMitigated = 0;
        AnnualCultivatedLandTotal = 0;
        FarmingMonthsThisYear = 0;
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

    public void TickPropagationState()
    {
        (MigrationPressureBonusMonthsRemaining, EventDrivenMigrationPressureBonus) = TickBonus(
            MigrationPressureBonusMonthsRemaining,
            EventDrivenMigrationPressureBonus);
        (FragmentationPressureBonusMonthsRemaining, EventDrivenFragmentationPressureBonus) = TickBonus(
            FragmentationPressureBonusMonthsRemaining,
            EventDrivenFragmentationPressureBonus);
        (SettlementChanceBonusMonthsRemaining, EventDrivenSettlementChanceBonus) = TickBonus(
            SettlementChanceBonusMonthsRemaining,
            EventDrivenSettlementChanceBonus);
    }

    private static (int monthsRemaining, double bonus) TickBonus(int monthsRemaining, double bonus)
    {
        if (monthsRemaining <= 0)
        {
            return (0, 0);
        }

        monthsRemaining--;
        if (monthsRemaining == 0)
        {
            bonus = 0;
        }

        return (monthsRemaining, bonus);
    }
}
