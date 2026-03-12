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
    public double LastAidReceived { get; set; }
    public double LastAidSent { get; set; }
    public double AidReceivedThisYear { get; set; }
    public double AidSentThisYear { get; set; }
    public double ManagedAnimalFoodThisMonth { get; set; }
    public double ManagedCropFoodThisMonth { get; set; }
    public double ManagedFoodThisYear { get; set; }
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
