namespace LivingWorld.Societies;

public sealed record CivilizationalHistoryEvent(
    CivilizationalHistoryEventType Type,
    int Year,
    int Month,
    int? GroupId,
    int? SocietyId,
    int? SettlementId,
    int? PolityId,
    int LineageId,
    int RegionId,
    string Summary,
    string? Reason,
    IReadOnlyDictionary<string, string> Data)
{
    public static readonly IReadOnlyDictionary<string, string> EmptyData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
