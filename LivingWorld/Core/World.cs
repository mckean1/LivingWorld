using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Core;

public sealed class World
{
    private long _nextEventId;

    public WorldTime Time { get; }

    public List<Region> Regions { get; } = new();
    public List<Species> Species { get; } = new();
    public List<Polity> Polities { get; } = new();

    public List<WorldEvent> Events { get; } = new();

    public event Action<WorldEvent>? EventRecorded;

    public World(WorldTime time)
    {
        Time = time;
    }

    public void AddEvent(WorldEvent worldEvent)
    {
        WorldEvent enriched = new()
        {
            EventId = ++_nextEventId,
            Year = Time.Year,
            Month = Time.Month,
            Season = Time.Season,
            Type = worldEvent.Type,
            Severity = worldEvent.Severity,
            Narrative = WorldEvent.NormalizeNarrative(worldEvent.Narrative),
            Details = worldEvent.Details,
            Reason = worldEvent.Reason,
            PolityId = worldEvent.PolityId,
            PolityName = worldEvent.PolityName,
            RelatedPolityId = worldEvent.RelatedPolityId,
            RelatedPolityName = worldEvent.RelatedPolityName,
            SpeciesId = worldEvent.SpeciesId,
            SpeciesName = worldEvent.SpeciesName,
            RegionId = worldEvent.RegionId,
            RegionName = worldEvent.RegionName,
            SettlementId = worldEvent.SettlementId,
            SettlementName = worldEvent.SettlementName,
            Before = worldEvent.Before,
            After = worldEvent.After,
            Metadata = worldEvent.Metadata
        };

        Events.Add(enriched);
        EventRecorded?.Invoke(enriched);
    }

    public void AddEvent(
        string type,
        WorldEventSeverity severity,
        string narrative,
        string? details = null,
        string? reason = null,
        int? polityId = null,
        string? polityName = null,
        int? relatedPolityId = null,
        string? relatedPolityName = null,
        int? speciesId = null,
        string? speciesName = null,
        int? regionId = null,
        string? regionName = null,
        int? settlementId = null,
        string? settlementName = null,
        Dictionary<string, string>? before = null,
        Dictionary<string, string>? after = null,
        Dictionary<string, string>? metadata = null)
    {
        AddEvent(new WorldEvent
        {
            Type = type,
            Severity = severity,
            Narrative = narrative,
            Details = details,
            Reason = reason,
            PolityId = polityId,
            PolityName = polityName,
            RelatedPolityId = relatedPolityId,
            RelatedPolityName = relatedPolityName,
            SpeciesId = speciesId,
            SpeciesName = speciesName,
            RegionId = regionId,
            RegionName = regionName,
            SettlementId = settlementId,
            SettlementName = settlementName,
            Before = before ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            After = after ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Metadata = metadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        });
    }
}
