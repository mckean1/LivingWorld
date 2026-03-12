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
    public double LastAidReceived { get; set; }
    public double LastAidSent { get; set; }
    public double AidReceivedThisYear { get; set; }
    public double AidSentThisYear { get; set; }

    public Settlement(int id, int polityId, int regionId, string name)
    {
        Id = id;
        PolityId = polityId;
        RegionId = regionId;
        Name = name;
        FoodState = FoodState.Stable;
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

        return FoodState;
    }

    public double RequestFoodAid()
        => Math.Max(0, FoodRequired - (FoodProduced + FoodStored));

    public double SendFoodAid(double amount)
    {
        double shipped = Math.Min(Math.Max(0, amount), Math.Max(0, FoodStored));
        if (shipped <= 0)
        {
            return 0;
        }

        FoodStored -= shipped;
        LastAidSent = shipped;
        AidSentThisYear += shipped;
        CalculateFoodState();
        return shipped;
    }
}
