namespace LivingWorld.Generation;

public sealed class WorldGenerationSettings
{
    public int RegionCount { get; init; } = 36;

    public int InitialSpeciesCount { get; init; } = 28;

    public int InitialPolityCount { get; init; } = 10;

    public int ContinentWidth { get; init; } = 6;

    public int ContinentHeight { get; init; } = 6;

    public int MinimumStartingPolityRegionSpacing { get; init; } = 1;

    public int HomelandSupportRadius { get; init; } = 1;

    public int MinimumAccessibleHomelandSupportSpecies { get; init; } = 2;

    public bool StartPolitiesWithHomeSettlements { get; init; } = true;

    public int StartingSettlementAgeYears { get; init; } = 0;
}
