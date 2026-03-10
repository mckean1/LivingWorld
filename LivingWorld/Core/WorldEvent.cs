namespace LivingWorld.Core;

public sealed class WorldEvent
{
    public long EventId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public Season Season { get; init; }
    public string Type { get; init; } = WorldEventType.WorldEvent;
    public WorldEventSeverity Severity { get; init; } = WorldEventSeverity.Normal;
    public string Narrative { get; init; } = string.Empty;
    public string? Details { get; init; }
    public string? Reason { get; init; }
    public int? PolityId { get; init; }
    public string? PolityName { get; init; }
    public int? RelatedPolityId { get; init; }
    public string? RelatedPolityName { get; init; }
    public int? SpeciesId { get; init; }
    public string? SpeciesName { get; init; }
    public int? RegionId { get; init; }
    public string? RegionName { get; init; }
    public int? SettlementId { get; init; }
    public string? SettlementName { get; init; }
    public Dictionary<string, string> Before { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> After { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public string HistoricalText => FormatHistoricalEvent(Year, Narrative);

    public static string FormatHistoricalEvent(int year, string narrative)
        => $"Year {year} - {NormalizeNarrative(narrative)}";

    public static string NormalizeNarrative(string narrative)
    {
        string trimmed = narrative.Trim();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        char lastCharacter = trimmed[^1];
        return lastCharacter is '.' or '!' or '?'
            ? trimmed
            : $"{trimmed}.";
    }

    public override string ToString()
        => Details is null
            ? $"[{Year:D3}-{Month:D2}] [{Type}/{Severity}] {NormalizeNarrative(Narrative)}"
            : $"[{Year:D3}-{Month:D2}] [{Type}/{Severity}] {Details}";
}