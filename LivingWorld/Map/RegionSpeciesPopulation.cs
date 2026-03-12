using LivingWorld.Life;

namespace LivingWorld.Map;

public sealed class RegionSpeciesPopulation
{
    public int SpeciesId { get; }
    public int RegionId { get; }
    public int PopulationCount { get; set; }
    public int CarryingCapacity { get; set; }
    public double BaseHabitatSuitability { get; set; }
    public double HabitatSuitability { get; set; }
    public double MigrationPressure { get; set; }
    public double RecentPredationPressure { get; set; }
    public double RecentHuntingPressure { get; set; }
    public double RecentFoodStress { get; set; }
    public int SeasonsUnderPressure { get; set; }
    public bool EstablishedThisSeason { get; set; }
    public bool ReceivedMigrantsThisSeason { get; set; }
    public bool SentMigrantsThisSeason { get; set; }
    public double IntelligenceOffset { get; set; }
    public double SocialityOffset { get; set; }
    public double AggressionOffset { get; set; }
    public double EnduranceOffset { get; set; }
    public double FertilityOffset { get; set; }
    public double DietFlexibilityOffset { get; set; }
    public double ClimateToleranceOffset { get; set; }
    public double SizeOffset { get; set; }
    public double FoodStressMutationPressure { get; set; }
    public double PredationMutationPressure { get; set; }
    public double HuntingMutationPressure { get; set; }
    public double HabitatMismatchMutationPressure { get; set; }
    public double IsolationMutationPressure { get; set; }
    public double CrowdingMutationPressure { get; set; }
    public double DriftMutationPressure { get; set; }
    public double DivergenceScore { get; set; }
    public int IsolationSeasons { get; set; }
    public int MinorMutationCount { get; set; }
    public int MajorMutationCount { get; set; }
    public int LastMutationYear { get; set; } = -1;
    public int LastMajorMutationYear { get; set; } = -1;
    public int LastIsolationEventSeason { get; set; }
    public int LastDivergenceMilestone { get; set; }
    public bool RegionAdaptationRecorded { get; set; }

    public RegionSpeciesPopulation(int speciesId, int regionId, int populationCount)
    {
        SpeciesId = speciesId;
        RegionId = regionId;
        PopulationCount = populationCount;
    }

    public bool IsLocallyExtinct => PopulationCount <= 0;

    public double GetTraitOffset(SpeciesTrait trait)
        => trait switch
        {
            SpeciesTrait.Intelligence => IntelligenceOffset,
            SpeciesTrait.Sociality => SocialityOffset,
            SpeciesTrait.Aggression => AggressionOffset,
            SpeciesTrait.Endurance => EnduranceOffset,
            SpeciesTrait.Fertility => FertilityOffset,
            SpeciesTrait.DietFlexibility => DietFlexibilityOffset,
            SpeciesTrait.ClimateTolerance => ClimateToleranceOffset,
            SpeciesTrait.Size => SizeOffset,
            _ => 0.0
        };

    public void ApplyTraitOffset(SpeciesTrait trait, double delta)
    {
        double clampedDelta = Math.Clamp(delta, -0.20, 0.20);

        switch (trait)
        {
            case SpeciesTrait.Intelligence:
                IntelligenceOffset = Math.Clamp(IntelligenceOffset + clampedDelta, -0.45, 0.45);
                break;
            case SpeciesTrait.Sociality:
                SocialityOffset = Math.Clamp(SocialityOffset + clampedDelta, -0.45, 0.45);
                break;
            case SpeciesTrait.Aggression:
                AggressionOffset = Math.Clamp(AggressionOffset + clampedDelta, -0.45, 0.45);
                break;
            case SpeciesTrait.Endurance:
                EnduranceOffset = Math.Clamp(EnduranceOffset + clampedDelta, -0.45, 0.45);
                break;
            case SpeciesTrait.Fertility:
                FertilityOffset = Math.Clamp(FertilityOffset + clampedDelta, -0.45, 0.45);
                break;
            case SpeciesTrait.DietFlexibility:
                DietFlexibilityOffset = Math.Clamp(DietFlexibilityOffset + clampedDelta, -0.45, 0.45);
                break;
            case SpeciesTrait.ClimateTolerance:
                ClimateToleranceOffset = Math.Clamp(ClimateToleranceOffset + clampedDelta, -0.45, 0.45);
                break;
            case SpeciesTrait.Size:
                SizeOffset = Math.Clamp(SizeOffset + clampedDelta, -0.45, 0.45);
                break;
        }
    }
}
