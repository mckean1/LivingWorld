namespace LivingWorld.Systems;

public sealed class MutationSettings
{
    public double MinorMutationThreshold { get; init; } = 1.15;
    public double MajorMutationThreshold { get; init; } = 2.75;
    public double MinorMutationChanceScale { get; init; } = 0.11;
    public double MajorMutationChanceScale { get; init; } = 0.05;
    public double DivergenceDecayPerSeason { get; init; } = 0.01;
    public double DivergencePressureDecay { get; init; } = 0.90;
    public double DivergencePressureScale { get; init; } = 0.18;
    public double MinorMutationDivergenceImpact { get; init; } = 1.4;
    public double MajorMutationDivergenceImpact { get; init; } = 2.4;

    public double SpeciationDivergenceThreshold { get; init; } = 2.85;
    public int SpeciationIsolationSeasonsThreshold { get; init; } = 16;
    public int SpeciationReadinessSeasonsThreshold { get; init; } = 14;
    public int SpeciationMinimumPopulation { get; init; } = 24;
    public int SpeciationMinimumGlobalPopulation { get; init; } = 52;
    public int SpeciationMinimumMutations { get; init; } = 3;
    public int SpeciationMinimumMajorMutations { get; init; } = 1;
    public int SpeciationCooldownYears { get; init; } = 18;
    public int MinimumSpeciesAgeYearsForSpeciation { get; init; } = 60;
    public int DescendantSpeciesStabilizationYears { get; init; } = 72;
    public int RegionalRootSpeciationCooldownYears { get; init; } = 28;
    public int RegionalRootLineageSoftCap { get; init; } = 3;
    public int RegionalRootLineageHardCap { get; init; } = 5;
    public double RegionalCrowdingExtraDivergencePerSpecies { get; init; } = 0.30;
    public int RegionalCrowdingExtraReadinessSeasonsPerSpecies { get; init; } = 4;
    public int RegionalCrowdingExtraGlobalPopulationPerSpecies { get; init; } = 20;
    public double SpeciationFounderPopulationShare { get; init; } = 0.72;
    public int SpeciationFounderPopulationMinimum { get; init; } = 10;
    public double DescendantBaselineTraitShare { get; init; } = 0.65;
    public double DescendantResidualOffsetShare { get; init; } = 0.35;
    public double ParentPostSpeciationDivergenceRetention { get; init; } = 0.42;
    public double DescendantStartingDivergenceRetention { get; init; } = 0.08;
    public double MajorPressureForSpeciationBonus { get; init; } = 1.40;
    public double DescendantStartingDivergencePressureRetention { get; init; } = 0.08;
    public double DescendantStartingIsolationPressureRetention { get; init; } = 0.12;
    public double DescendantStartingStressPressureRetention { get; init; } = 0.18;
    public int DescendantStartingIsolationSeasons { get; init; } = 0;
    public int DescendantStartingReadinessSeasons { get; init; } = 0;
    public int IsolationEventBaseThresholdSeasons { get; init; } = 12;
    public int MinorMutationEventCooldownYears { get; init; } = 3;
    public double DomesticationDiscoveryInterestThreshold { get; init; } = 0.18;
    public double DomesticationDiscoveryAffinityThreshold { get; init; } = 0.45;
}
