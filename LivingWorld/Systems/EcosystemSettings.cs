namespace LivingWorld.Systems;

public sealed class EcosystemSettings
{
    public double MigrationPressureThreshold { get; init; } = 0.45;

    public double PredatorMigrationPressureThreshold { get; init; } = 0.56;

    public double HerbivoreExpansionCapacityRatioThreshold { get; init; } = 0.54;

    public double PredatorExpansionCapacityRatioThreshold { get; init; } = 0.68;

    public double MinimumTargetSuitability { get; init; } = 0.58;

    public double FrontierTargetSuitability { get; init; } = 0.66;

    public double PredatorTargetSuitability { get; init; } = 0.64;

    public int MinimumSourcePopulationForMigration { get; init; } = 10;

    public int FounderPopulationMinimum { get; init; } = 2;

    public int PredatorFounderPopulationMinimum { get; init; } = 4;

    public int ApexFounderPopulationMinimum { get; init; } = 3;

    public double FounderPopulationShare { get; init; } = 0.04;

    public double PredatorFounderPopulationShare { get; init; } = 0.05;

    public double ApexFounderPopulationShare { get; init; } = 0.04;

    public int MigrationCooldownSeasons { get; init; } = 2;

    public double EmptyFaunaFrontierBonus { get; init; } = 0.22;

    public double HerbivoreFrontierBonus { get; init; } = 0.10;

    public int PredatorMinimumPreyPopulation { get; init; } = 24;

    public int ApexMinimumPreyPopulation { get; init; } = 36;

    public double PredatorMigrationSupportRatioThreshold { get; init; } = 1.05;

    public double ApexMigrationSupportRatioThreshold { get; init; } = 1.12;

    public double PredatorPreyPerPredatorRequired { get; init; } = 4.2;

    public double ApexPreyPerPredatorRequired { get; init; } = 5.0;

    public int PredatorEstablishmentPopulationThreshold { get; init; } = 9;

    public int ApexEstablishmentPopulationThreshold { get; init; } = 6;

    public int PredatorFounderSeasons { get; init; } = 5;

    public double PredatorEstablishmentSupportThreshold { get; init; } = 1.18;

    public double PredatorFounderGrowthBonus { get; init; } = 0.22;

    public double PredatorFounderFailureSupportThreshold { get; init; } = 0.78;

    public double PredatorFounderFailureDeclinePenalty { get; init; } = 0.16;

    public double PredatorCompetitionPenalty { get; init; } = 0.08;

    public double PredatorGlobalRangePenalty { get; init; } = 0.10;

    public double RecolonizationTargetScoreThreshold { get; init; } = 0.62;

    public int MaxMigrationTargetsPerPopulation { get; init; } = 1;
}
