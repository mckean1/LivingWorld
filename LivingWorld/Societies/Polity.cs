using LivingWorld.Advancement;
using LivingWorld.Economy;

namespace LivingWorld.Societies;

public sealed class Polity
{
    private readonly List<Settlement> _settlements;
    private readonly Dictionary<string, CulturalDiscovery> _discoveries;
    private int _nextSettlementSequence;

    public int Id { get; }
    public string Name { get; }
    public int SpeciesId { get; set; }
    public int RegionId { get; set; }
    public int Population { get; set; }
    public int LineageId { get; set; }
    public int? FounderSocietyId { get; set; }
    public int? ParentPolityId { get; set; }
    public PolityStage Stage { get; set; }
    public string? IdentitySeed { get; set; }
    public string? CurrentPressureSummary { get; set; }

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
    public double AnnualFoodManaged { get; set; }
    public double AnnualFoodImported { get; set; }
    public double AnnualFoodExported { get; set; }
    public double AnnualFoodImportedInternal { get; set; }
    public double AnnualFoodImportedExternal { get; set; }
    public int TradeReliefMonthsThisYear { get; set; }
    public int TradePartialReliefMonthsThisYear { get; set; }
    public int TradeFullReliefMonthsThisYear { get; set; }
    public int TradePartnerCountThisYear { get; set; }
    public double AnnualTradeNeedMitigated { get; set; }
    public double FoodManagedThisMonth { get; set; }

    // Agriculture tracking
    public double CultivatedLand { get; set; }
    public double AnnualCultivatedLandTotal { get; set; }
    public int FarmingMonthsThisYear { get; set; }
    public double LastYearAverageCultivatedLand { get; set; }
    public int ConsecutiveFarmingYears { get; set; }
    public int AgricultureEventCooldownYears { get; set; }
    public bool ManagedFoodSupplyEstablished { get; set; }
    public int? ManagedFoodSupplyEstablishedYear { get; set; }

    // Ongoing stress
    public int StarvationMonthsThisYear { get; set; }

    public SettlementStatus SettlementStatus { get; set; }
    public int SettlementCount => _settlements.Count;
    public int YearsSinceFirstSettlement { get; set; }
    public FoodStateSummary? LastResolvedFoodState { get; set; }
    public int? LastResolvedFoodStateYear { get; set; }
    public double FoodHuntedThisYear { get; set; }
    public int HuntingCasualtiesThisYear { get; set; }
    public int LegendaryHuntsThisYear { get; set; }

    public HashSet<AdvancementId> Advancements { get; }
    public PolityCapabilities Capabilities { get; private set; }
    public IReadOnlyCollection<CulturalDiscovery> Discoveries => _discoveries.Values;
    public HashSet<int> KnownEdibleSpeciesIds { get; }
    public HashSet<int> KnownToxicSpeciesIds { get; }
    public HashSet<int> KnownDangerousPreySpeciesIds { get; }
    public Dictionary<int, int> SuccessfulHuntsBySpecies { get; }
    public Dictionary<int, int> FailedHuntsBySpecies { get; }
    public Dictionary<int, double> DomesticationInterestBySpecies { get; }
    public Dictionary<int, double> CultivationFamiliarityBySpecies { get; }
    public Dictionary<MaterialType, double> MaterialMovedThisYear { get; }
    public bool HasSettlements => _settlements.Count > 0;
    public IReadOnlyList<Settlement> Settlements => _settlements;

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
        FounderSocietyId = null;
        ParentPolityId = parentPolityId;
        Stage = stage;
        IdentitySeed = null;
        CurrentPressureSummary = null;

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
        AnnualFoodManaged = 0;
        AnnualFoodImported = 0;
        AnnualFoodExported = 0;
        AnnualFoodImportedInternal = 0;
        AnnualFoodImportedExternal = 0;
        TradeReliefMonthsThisYear = 0;
        TradePartialReliefMonthsThisYear = 0;
        TradeFullReliefMonthsThisYear = 0;
        TradePartnerCountThisYear = 0;
        AnnualTradeNeedMitigated = 0;
        FoodManagedThisMonth = 0;
        StarvationMonthsThisYear = 0;

        CultivatedLand = 0;
        AnnualCultivatedLandTotal = 0;
        FarmingMonthsThisYear = 0;
        LastYearAverageCultivatedLand = 0;
        ConsecutiveFarmingYears = 0;
        AgricultureEventCooldownYears = 0;
        ManagedFoodSupplyEstablished = false;
        ManagedFoodSupplyEstablishedYear = null;

        _nextSettlementSequence = 1;
        _settlements = [];
        ClearSettlementState();
        LastResolvedFoodState = null;
        LastResolvedFoodStateYear = null;
        FoodHuntedThisYear = 0;
        HuntingCasualtiesThisYear = 0;
        LegendaryHuntsThisYear = 0;
        Advancements = new HashSet<AdvancementId>();
        Capabilities = PolityCapabilities.FromAdvancements(Advancements);
        KnownEdibleSpeciesIds = [];
        KnownToxicSpeciesIds = [];
        KnownDangerousPreySpeciesIds = [];
        SuccessfulHuntsBySpecies = new Dictionary<int, int>();
        FailedHuntsBySpecies = new Dictionary<int, int>();
        DomesticationInterestBySpecies = new Dictionary<int, double>();
        CultivationFamiliarityBySpecies = new Dictionary<int, double>();
        MaterialMovedThisYear = new Dictionary<MaterialType, double>();
        _discoveries = new Dictionary<string, CulturalDiscovery>(StringComparer.OrdinalIgnoreCase);
    }

    public void ResetAnnualFoodStats()
    {
        AnnualFoodNeeded = 0;
        AnnualFoodConsumed = 0;
        AnnualFoodShortage = 0;
        AnnualFoodGathered = 0;
        AnnualFoodFarmed = 0;
        AnnualFoodManaged = 0;
        AnnualFoodImported = 0;
        AnnualFoodExported = 0;
        AnnualFoodImportedInternal = 0;
        AnnualFoodImportedExternal = 0;
        TradeReliefMonthsThisYear = 0;
        TradePartialReliefMonthsThisYear = 0;
        TradeFullReliefMonthsThisYear = 0;
        TradePartnerCountThisYear = 0;
        AnnualTradeNeedMitigated = 0;
        FoodManagedThisMonth = 0;
        AnnualCultivatedLandTotal = 0;
        FarmingMonthsThisYear = 0;
        StarvationMonthsThisYear = 0;
        MovedThisYear = false;
        MovesThisYear = 0;
        FoodHuntedThisYear = 0;
        HuntingCasualtiesThisYear = 0;
        LegendaryHuntsThisYear = 0;

        foreach (Settlement settlement in _settlements)
        {
            settlement.AidReceivedThisYear = 0;
            settlement.AidSentThisYear = 0;
            settlement.LastAidReceived = 0;
            settlement.LastAidSent = 0;
            settlement.ManagedAnimalFoodThisMonth = 0;
            settlement.ManagedCropFoodThisMonth = 0;
            settlement.ManagedFoodThisYear = 0;
            settlement.ResetAnnualMaterialStats();
        }

        MaterialMovedThisYear.Clear();
    }

    public void ResetBootstrapRuntimeState()
    {
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
        AnnualFoodManaged = 0;
        AnnualFoodImported = 0;
        AnnualFoodExported = 0;
        AnnualFoodImportedInternal = 0;
        AnnualFoodImportedExternal = 0;
        TradeReliefMonthsThisYear = 0;
        TradePartialReliefMonthsThisYear = 0;
        TradeFullReliefMonthsThisYear = 0;
        TradePartnerCountThisYear = 0;
        AnnualTradeNeedMitigated = 0;
        FoodManagedThisMonth = 0;
        AnnualCultivatedLandTotal = 0;
        FarmingMonthsThisYear = 0;
        StarvationMonthsThisYear = 0;
        MovedThisYear = false;
        MovesThisYear = 0;
        FoodHuntedThisYear = 0;
        HuntingCasualtiesThisYear = 0;
        LegendaryHuntsThisYear = 0;

        foreach (Settlement settlement in _settlements)
        {
            settlement.ResetBootstrapRuntimeState();
        }

        MaterialMovedThisYear.Clear();
    }

    public bool HasAdvancement(AdvancementId advancementId)
        => Advancements.Contains(advancementId);

    public bool LearnAdvancement(AdvancementId advancementId)
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
        YearsSinceFirstSettlement = 0;
        _settlements.Clear();
    }

    public Settlement EstablishFirstSettlement(int regionId, string name)
    {
        SettlementStatus = SettlementStatus.SemiSettled;
        YearsSinceFirstSettlement = 0;
        _settlements.Clear();
        return AddSettlement(regionId, name);
    }

    public Settlement AddSettlement(int regionId, string name)
    {
        Settlement settlement = new(CreateSettlementId(), Id, regionId, name);
        _settlements.Add(settlement);
        return settlement;
    }

    public Settlement GetOrCreatePrimarySettlement(int regionId, string defaultName)
    {
        Settlement? existing = _settlements.FirstOrDefault(settlement => settlement.RegionId == regionId);
        if (existing is not null)
        {
            return existing;
        }

        if (_settlements.Count == 0)
        {
            return EstablishFirstSettlement(regionId, defaultName);
        }

        return AddSettlement(regionId, defaultName);
    }

    public Settlement? GetPrimarySettlementInRegion(int regionId)
        => _settlements.FirstOrDefault(settlement => settlement.RegionId == regionId);

    public Settlement? GetPrimarySettlement()
        => _settlements.FirstOrDefault();

    public void RelocateSettlements(int regionId, Func<int, string> nameFactory)
    {
        for (int index = 0; index < _settlements.Count; index++)
        {
            Settlement settlement = _settlements[index];
            settlement.RegionId = regionId;
            settlement.Name = nameFactory(index);
        }
    }

    public void AdvanceSettlementAges()
    {
        foreach (Settlement settlement in _settlements)
        {
            settlement.EstablishedMonths += 12;
        }
    }

    public void AdvanceSettlementMonths()
    {
        foreach (Settlement settlement in _settlements)
        {
            settlement.AdvanceOneMonthOfAge();
        }
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

    public void RecordSuccessfulHunt(int speciesId)
        => SuccessfulHuntsBySpecies[speciesId] = GetTrackedValue(SuccessfulHuntsBySpecies, speciesId) + 1;

    public void RecordFailedHunt(int speciesId)
        => FailedHuntsBySpecies[speciesId] = GetTrackedValue(FailedHuntsBySpecies, speciesId) + 1;

    public bool AddDiscovery(CulturalDiscovery discovery)
    {
        if (_discoveries.ContainsKey(discovery.Key))
        {
            return false;
        }

        _discoveries.Add(discovery.Key, discovery);
        return true;
    }

    public bool HasDiscovery(string discoveryKey)
        => _discoveries.ContainsKey(discoveryKey);

    public void IncreaseDomesticationInterest(int speciesId, double amount)
    {
        double current = DomesticationInterestBySpecies.TryGetValue(speciesId, out double existing)
            ? existing
            : 0.0;
        DomesticationInterestBySpecies[speciesId] = Math.Max(0.0, current + amount);
    }

    public void IncreaseCultivationFamiliarity(int speciesId, double amount)
    {
        double current = CultivationFamiliarityBySpecies.TryGetValue(speciesId, out double existing)
            ? existing
            : 0.0;
        CultivationFamiliarityBySpecies[speciesId] = Math.Max(0.0, current + amount);
    }

    private static int GetTrackedValue(IReadOnlyDictionary<int, int> values, int key)
        => values.TryGetValue(key, out int existing)
            ? existing
            : 0;

    private int CreateSettlementId()
        => (Id * 1000) + _nextSettlementSequence++;

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
