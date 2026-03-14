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

    public int PhaseBMinimumBootstrapYears { get; init; } = 180;

    public int PhaseBMaximumBootstrapYears { get; init; } = 900;

    public double MinimumPhaseAOccupiedRegionPercentage { get; init; } = 0.78;

    public double MinimumPhaseAProducerCoverage { get; init; } = 0.88;

    public double MinimumPhaseAConsumerCoverage { get; init; } = 0.52;

    public double MinimumPhaseAPredatorCoverage { get; init; } = 0.14;

    public int MinimumPhaseBMatureLineageCount { get; init; } = 3;

    public int MinimumPhaseBSpeciationCount { get; init; } = 2;

    public int MinimumPhaseBExtinctLineageCount { get; init; } = 1;

    public int MinimumPhaseBAncestryDepth { get; init; } = 1;

    public int MinimumPhaseBMatureRegionalDivergenceCount { get; init; } = 4;

    public int MinimumPhaseBSentienceCapableLineageCount { get; init; } = 1;

    public int MinimumPhaseBStableRegionCount { get; init; } = 12;

    public double PhaseBMatureRegionalDivergenceThreshold { get; init; } = 1.60;
}
