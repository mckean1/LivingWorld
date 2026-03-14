namespace LivingWorld.Generation;

public sealed class WorldGenerationSettings
{
    public int RegionCount { get; init; } = 36;

    public int InitialSpeciesCount { get; init; } = 7;

    public int InitialPolityCount { get; init; } = 0;

    public int ContinentWidth { get; init; } = 6;

    public int ContinentHeight { get; init; } = 6;

    public int MinimumStartingPolityRegionSpacing { get; init; } = 1;

    public int HomelandSupportRadius { get; init; } = 1;

    public int MinimumAccessibleHomelandSupportSpecies { get; init; } = 2;

    public bool StartPolitiesWithHomeSettlements { get; init; } = true;

    public int StartingSettlementAgeYears { get; init; } = 0;

    public int PhaseAMinimumBootstrapMonths { get; init; } = 18;

    public int PhaseAMaximumBootstrapMonths { get; init; } = 60;

    public double MinimumPhaseAOccupiedRegionPercentage { get; init; } = 0.78;

    public double MinimumPhaseAProducerCoverage { get; init; } = 0.88;

    public double MinimumPhaseAConsumerCoverage { get; init; } = 0.52;

    public double MinimumPhaseAPredatorCoverage { get; init; } = 0.14;
}
