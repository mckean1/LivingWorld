namespace LivingWorld.Systems;

public sealed class DomesticationSettings
{
    public double AnimalCandidateSuitabilityThreshold { get; init; } = 0.54;
    public double AnimalCandidateInterestThreshold { get; init; } = 0.18;
    public double HerdEstablishmentSuitabilityThreshold { get; init; } = 0.62;
    public double HerdEstablishmentInterestThreshold { get; init; } = 0.32;
    public int MinimumSuccessfulHuntsForCandidate { get; init; } = 2;
    public int MinimumSuccessfulHuntsForHerd { get; init; } = 3;
    public double PlantDiscoveryThreshold { get; init; } = 0.22;
    public double CropEstablishmentThreshold { get; init; } = 0.34;
    public double BaseCultivationFamiliarityGain { get; init; } = 0.035;
    public double FoodStressCultivationBonus { get; init; } = 0.020;
    public double ManagedHerdFoodFactor { get; init; } = 0.22;
    public double ManagedHerdGrowthFactor { get; init; } = 0.06;
    public double CropYieldBonusScale { get; init; } = 0.24;
    public double CropStabilityBonusScale { get; init; } = 0.14;
    public double AnnualManagedFoodStabilityShare { get; init; } = 0.18;
    public int SpreadMinimumSettlementCount { get; init; } = 2;
}
