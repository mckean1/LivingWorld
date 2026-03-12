namespace LivingWorld.Systems;

public sealed class EcosystemSettings
{
    public double MigrationPressureThreshold { get; init; } = 0.45;

    public double HerbivoreExpansionCapacityRatioThreshold { get; init; } = 0.54;

    public double PredatorExpansionCapacityRatioThreshold { get; init; } = 0.68;

    public double MinimumTargetSuitability { get; init; } = 0.58;

    public double FrontierTargetSuitability { get; init; } = 0.66;

    public int MinimumSourcePopulationForMigration { get; init; } = 10;

    public int FounderPopulationMinimum { get; init; } = 2;

    public double FounderPopulationShare { get; init; } = 0.04;

    public int MigrationCooldownSeasons { get; init; } = 2;

    public double EmptyFaunaFrontierBonus { get; init; } = 0.22;

    public double HerbivoreFrontierBonus { get; init; } = 0.10;

    public int PredatorMinimumPreyPopulation { get; init; } = 24;

    public int ApexMinimumPreyPopulation { get; init; } = 36;

    public double RecolonizationTargetScoreThreshold { get; init; } = 0.62;

    public int MaxMigrationTargetsPerPopulation { get; init; } = 1;
}
