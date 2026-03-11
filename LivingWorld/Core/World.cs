using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Core;

public sealed class World
{
    private long _nextEventId;
    private EventPropagationCoordinator? _eventPropagationCoordinator;

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

    public void ConfigureEventPropagation(EventPropagationCoordinator coordinator)
    {
        _eventPropagationCoordinator = coordinator;
    }

    public void AddEvent(WorldEvent worldEvent)
    {
        if (_eventPropagationCoordinator is not null)
        {
            _eventPropagationCoordinator.Process(this, worldEvent, RecordEvent);
            return;
        }

        RecordEvent(worldEvent);
    }

    private WorldEvent RecordEvent(WorldEvent worldEvent)
    {
        WorldEvent enriched = new()
        {
            EventId = ++_nextEventId,
            Year = Time.Year,
            Month = Time.Month,
            Season = Time.Season,
            Type = worldEvent.Type,
            Severity = worldEvent.Severity,
            Scope = worldEvent.Scope,
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
            RootEventId = worldEvent.RootEventId,
            PropagationDepth = worldEvent.PropagationDepth,
            ParentEventIds = worldEvent.ParentEventIds.ToList(),
            Before = CopyMap(worldEvent.Before),
            After = CopyMap(worldEvent.After),
            Metadata = CopyMap(worldEvent.Metadata)
        };

        Events.Add(enriched);
        EventRecorded?.Invoke(enriched);
        return enriched;
    }

    public void AddEvent(
        string type,
        WorldEventSeverity severity,
        string narrative,
        string? details = null,
        string? reason = null,
        WorldEventScope scope = WorldEventScope.Polity,
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
        IEnumerable<long>? parentEventIds = null,
        long? rootEventId = null,
        int propagationDepth = 0,
        Dictionary<string, string>? before = null,
        Dictionary<string, string>? after = null,
        Dictionary<string, string>? metadata = null)
    {
        AddEvent(new WorldEvent
        {
            Type = type,
            Severity = severity,
            Scope = scope,
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
            ParentEventIds = parentEventIds is null
                ? Array.Empty<long>()
                : parentEventIds.Distinct().OrderBy(id => id).ToList(),
            RootEventId = rootEventId,
            PropagationDepth = propagationDepth,
            Before = before ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            After = after ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Metadata = metadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        });
    }

    private static Dictionary<string, string> CopyMap(IReadOnlyDictionary<string, string> source)
        => new(source, StringComparer.OrdinalIgnoreCase);
}
