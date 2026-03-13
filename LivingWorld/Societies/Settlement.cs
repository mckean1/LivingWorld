using LivingWorld.Economy;

namespace LivingWorld.Societies;

public sealed class Settlement
{
    private const double SurplusThreshold = 0.20;
    private const double StableThreshold = -0.10;
    private const double DeficitThreshold = -0.40;

    public int Id { get; }
    public int PolityId { get; }
    public int RegionId { get; set; }
    public string Name { get; set; }
    public double CultivatedLand { get; set; }
    public int YearsEstablished { get; set; }
    public double FoodProduced { get; set; }
    public double FoodStored { get; set; }
    public double FoodRequired { get; set; }
    public double FoodBalance { get; private set; }
    public FoodState FoodState { get; private set; }
    public SettlementStarvationStage StarvationStage { get; private set; }
    public SettlementStarvationStage LastRecordedStarvationStage { get; set; }
    public int? LastStarvationRecoveryYear { get; set; }
    public double LastAidReceived { get; set; }
    public double LastAidSent { get; set; }
    public double AidReceivedThisYear { get; set; }
    public double AidSentThisYear { get; set; }
    public double ManagedAnimalFoodThisMonth { get; set; }
    public double ManagedCropFoodThisMonth { get; set; }
    public double ManagedFoodThisYear { get; set; }
    public int ToolProductionTier { get; set; }
    public Dictionary<MaterialType, double> MaterialStockpiles { get; } = [];
    public Dictionary<MaterialType, double> MaterialProducedThisMonth { get; } = [];
    public Dictionary<MaterialType, double> MaterialConsumedThisMonth { get; } = [];
    public Dictionary<MaterialType, double> MaterialProducedThisYear { get; } = [];
    public Dictionary<MaterialType, double> MaterialConsumedThisYear { get; } = [];
    public Dictionary<MaterialType, double> MaterialTargetReserves { get; } = [];
    public Dictionary<MaterialType, MaterialPressureState> MaterialPressureStates { get; } = [];
    public Dictionary<MaterialType, int> LastRecordedMaterialShortageBands { get; } = [];
    public Dictionary<MaterialType, double> MaterialNeedPressures { get; } = [];
    public Dictionary<MaterialType, double> MaterialAvailabilityScores { get; } = [];
    public Dictionary<MaterialType, double> MaterialValueScores { get; } = [];
    public Dictionary<MaterialType, double> MaterialOpportunityScores { get; } = [];
    public Dictionary<MaterialType, double> MaterialExternalPullReadiness { get; } = [];
    public Dictionary<MaterialType, double> MaterialProductionFocusScores { get; } = [];
    public Dictionary<MaterialType, int> LastRecordedHighlyValuedBands { get; } = [];
    public Dictionary<MaterialType, bool> LastRecordedTradeGoodStates { get; } = [];
    public HashSet<MaterialType> HighlyValuedMaterials { get; } = [];
    public HashSet<MaterialType> TradeGoodMaterials { get; } = [];
    public HashSet<MaterialType> LocallyCommonMaterials { get; } = [];
    public MaterialType? DominantProductionFocusMaterial { get; set; }
    public MaterialType? CandidateProductionFocusMaterial { get; set; }
    public int CandidateProductionFocusMonths { get; set; }
    public int ProductionFocusShiftCooldownMonths { get; set; }
    public Dictionary<SettlementSpecializationTag, double> SpecializationScores { get; } = [];
    public HashSet<SettlementSpecializationTag> SpecializationTags { get; } = [];
    public HashSet<string> MaterialMilestonesRecorded { get; } = [];
    public List<ManagedHerd> ManagedHerds { get; } = [];
    public List<CultivatedCrop> CultivatedCrops { get; } = [];

    public Settlement(int id, int polityId, int regionId, string name)
    {
        Id = id;
        PolityId = polityId;
        RegionId = regionId;
        Name = name;
        FoodState = FoodState.Stable;
        StarvationStage = SettlementStarvationStage.None;
        LastRecordedStarvationStage = SettlementStarvationStage.None;
        LastStarvationRecoveryYear = null;

        foreach (MaterialType materialType in Enum.GetValues<MaterialType>())
        {
            MaterialStockpiles[materialType] = 0.0;
            MaterialProducedThisMonth[materialType] = 0.0;
            MaterialConsumedThisMonth[materialType] = 0.0;
            MaterialProducedThisYear[materialType] = 0.0;
            MaterialConsumedThisYear[materialType] = 0.0;
            MaterialTargetReserves[materialType] = 0.0;
            MaterialPressureStates[materialType] = MaterialPressureState.Stable;
            LastRecordedMaterialShortageBands[materialType] = 0;
            MaterialNeedPressures[materialType] = 0.0;
            MaterialAvailabilityScores[materialType] = 0.0;
            MaterialValueScores[materialType] = 0.0;
            MaterialOpportunityScores[materialType] = 0.0;
            MaterialExternalPullReadiness[materialType] = 0.0;
            MaterialProductionFocusScores[materialType] = 0.0;
            LastRecordedHighlyValuedBands[materialType] = 0;
            LastRecordedTradeGoodStates[materialType] = false;
        }
    }

    public FoodState CalculateFoodState()
    {
        FoodBalance = FoodProduced + FoodStored - FoodRequired;
        double ratio = FoodRequired <= 0
            ? 1.0
            : FoodBalance / FoodRequired;

        FoodState = ratio switch
        {
            > SurplusThreshold => FoodState.Surplus,
            >= StableThreshold => FoodState.Stable,
            >= DeficitThreshold => FoodState.Deficit,
            _ => FoodState.Starving
        };
        StarvationStage = ResolveStarvationStage(ratio, FoodState);

        return FoodState;
    }

    public double RequestFoodAid()
        => Math.Max(0, FoodRequired - (FoodProduced + FoodStored));

    public double SendFoodAid(double amount)
    {
        double availableProduced = Math.Max(0, FoodProduced);
        double availableStored = Math.Max(0, FoodStored);
        double availableToShip = availableProduced + availableStored;
        double shipped = Math.Min(Math.Max(0, amount), availableToShip);
        if (shipped <= 0)
        {
            return 0;
        }

        double shippedFromStored = Math.Min(availableStored, shipped);
        double shippedFromProduced = shipped - shippedFromStored;
        FoodStored -= shippedFromStored;
        FoodProduced -= shippedFromProduced;
        LastAidSent = shipped;
        AidSentThisYear += shipped;
        CalculateFoodState();
        return shipped;
    }

    public ManagedHerd? GetManagedHerd(int speciesId)
        => ManagedHerds.FirstOrDefault(herd => herd.BaseSpeciesId == speciesId);

    public CultivatedCrop? GetCultivatedCrop(int speciesId)
        => CultivatedCrops.FirstOrDefault(crop => crop.BaseSpeciesId == speciesId);

    public void ResetMonthlyMaterialStats()
    {
        foreach (MaterialType materialType in Enum.GetValues<MaterialType>())
        {
            MaterialProducedThisMonth[materialType] = 0.0;
            MaterialConsumedThisMonth[materialType] = 0.0;
        }

        HighlyValuedMaterials.Clear();
        TradeGoodMaterials.Clear();
        LocallyCommonMaterials.Clear();

        if (ProductionFocusShiftCooldownMonths > 0)
        {
            ProductionFocusShiftCooldownMonths--;
        }
    }

    public void ResetAnnualMaterialStats()
    {
        foreach (MaterialType materialType in Enum.GetValues<MaterialType>())
        {
            MaterialProducedThisYear[materialType] = 0.0;
            MaterialConsumedThisYear[materialType] = 0.0;
            MaterialProducedThisMonth[materialType] = 0.0;
            MaterialConsumedThisMonth[materialType] = 0.0;
        }

        SpecializationScores.Clear();
    }

    public void SetMaterialEconomySignals(
        MaterialType materialType,
        double needPressure,
        double availabilityScore,
        double valueScore,
        double opportunityScore,
        double externalPullReadiness,
        double productionFocusScore)
    {
        MaterialNeedPressures[materialType] = Math.Max(0.0, needPressure);
        MaterialAvailabilityScores[materialType] = Math.Max(0.0, availabilityScore);
        MaterialValueScores[materialType] = Math.Max(0.0, valueScore);
        MaterialOpportunityScores[materialType] = Math.Max(0.0, opportunityScore);
        MaterialExternalPullReadiness[materialType] = Math.Max(0.0, externalPullReadiness);
        MaterialProductionFocusScores[materialType] = Math.Max(0.0, productionFocusScore);
    }

    public bool IsHighlyValued(MaterialType materialType)
        => HighlyValuedMaterials.Contains(materialType);

    public bool IsTradeGood(MaterialType materialType)
        => TradeGoodMaterials.Contains(materialType);

    public bool IsLocallyCommon(MaterialType materialType)
        => LocallyCommonMaterials.Contains(materialType);

    public double GetMaterialStockpile(MaterialType materialType)
        => MaterialStockpiles.TryGetValue(materialType, out double value)
            ? value
            : 0.0;

    public void SetMaterialTargetReserve(MaterialType materialType, double targetReserve)
        => MaterialTargetReserves[materialType] = Math.Max(0.0, targetReserve);

    public void AddMaterial(MaterialType materialType, double amount)
    {
        if (amount <= 0)
        {
            return;
        }

        MaterialStockpiles[materialType] = GetMaterialStockpile(materialType) + amount;
        MaterialProducedThisMonth[materialType] += amount;
        MaterialProducedThisYear[materialType] += amount;
    }

    public double ConsumeMaterial(MaterialType materialType, double amount)
    {
        double available = GetMaterialStockpile(materialType);
        double consumed = Math.Min(Math.Max(0.0, amount), available);
        if (consumed <= 0)
        {
            return 0.0;
        }

        MaterialStockpiles[materialType] = available - consumed;
        MaterialConsumedThisMonth[materialType] += consumed;
        MaterialConsumedThisYear[materialType] += consumed;
        return consumed;
    }

    public MaterialPressureState CalculateMaterialPressure(MaterialType materialType)
    {
        double targetReserve = MaterialTargetReserves.TryGetValue(materialType, out double target)
            ? target
            : 0.0;
        double stockpile = GetMaterialStockpile(materialType);
        MaterialPressureState pressureState = targetReserve <= 0.01
            ? MaterialPressureState.Stable
            : stockpile < targetReserve * 0.75
                ? MaterialPressureState.Deficit
                : stockpile > targetReserve * 1.35
                    ? MaterialPressureState.Surplus
                    : MaterialPressureState.Stable;
        MaterialPressureStates[materialType] = pressureState;
        return pressureState;
    }

    public int ResolveMaterialShortageBand(MaterialType materialType)
    {
        double targetReserve = MaterialTargetReserves.TryGetValue(materialType, out double target)
            ? target
            : 0.0;
        if (targetReserve <= 0.01)
        {
            return 0;
        }

        double ratio = GetMaterialStockpile(materialType) / targetReserve;
        return ratio switch
        {
            < 0.35 => 2,
            < 0.75 => 1,
            _ => 0
        };
    }

    public double ResolveToolEffectiveness()
    {
        double stockpileCoverage = Math.Clamp(GetMaterialStockpile(MaterialType.SimpleTools) / Math.Max(1.0, FoodRequired * 0.12), 0.0, 1.0);
        double qualityBonus = ToolProductionTier switch
        {
            >= 2 => 0.18,
            1 => 0.10,
            _ => 0.0
        };
        return 1.0 + (stockpileCoverage * 0.16) + qualityBonus;
    }

    public double ResolveStorageMultiplier()
        => 1.0 + Math.Clamp(GetMaterialStockpile(MaterialType.Pottery) / Math.Max(1.0, FoodRequired * 0.10), 0.0, 1.0) * 0.28;

    public double ResolveTransportAidMultiplier()
        => 1.0 + Math.Clamp(
            (GetMaterialStockpile(MaterialType.Rope) / Math.Max(1.0, FoodRequired * 0.06))
            + (GetMaterialStockpile(MaterialType.Textiles) / Math.Max(1.0, FoodRequired * 0.04)),
            0.0,
            1.0) * 0.10;

    private static SettlementStarvationStage ResolveStarvationStage(double foodBalanceRatio, FoodState foodState)
    {
        if (foodState != FoodState.Starving)
        {
            return SettlementStarvationStage.None;
        }

        return foodBalanceRatio switch
        {
            < -0.85 => SettlementStarvationStage.Collapsing,
            < -0.60 => SettlementStarvationStage.Severe,
            _ => SettlementStarvationStage.Starving
        };
    }
}
