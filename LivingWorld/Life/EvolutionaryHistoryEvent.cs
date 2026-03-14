namespace LivingWorld.Life;

public sealed record EvolutionaryHistoryEvent(
    EvolutionaryHistoryEventType Type,
    int Year,
    int Month,
    int LineageId,
    int? ParentLineageId,
    int? SpeciesId,
    int? RegionId,
    string Summary,
    string? Reason,
    IReadOnlyDictionary<string, string> Data)
{
    public static readonly IReadOnlyDictionary<string, string> EmptyData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
