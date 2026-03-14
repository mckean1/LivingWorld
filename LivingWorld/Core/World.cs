using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Core;

public sealed class World
{
    private long _nextEventId;
    private EventPropagationCoordinator? _eventPropagationCoordinator;

    public WorldTime Time { get; }
    public WorldSimulationPhase SimulationPhase { get; private set; }
    public WorldStartupStage StartupStage { get; set; } = WorldStartupStage.SocietalSimulation;
    public PhaseAReadinessReport PhaseAReadinessReport { get; set; } = PhaseAReadinessReport.Empty;

    public List<Region> Regions { get; } = new();
    public List<Species> Species { get; } = new();
    public List<Polity> Polities { get; } = new();
    public List<LocalPopulationExtinctionRecord> LocalPopulationExtinctions { get; } = new();

    public List<WorldEvent> Events { get; } = new();

    public event Action<WorldEvent>? EventRecorded;

    public World(WorldTime time, WorldSimulationPhase simulationPhase = WorldSimulationPhase.Active)
    {
        Time = time;
        SimulationPhase = simulationPhase;
    }

    public bool IsBootstrapping => SimulationPhase == WorldSimulationPhase.Bootstrap;

    public void EnterBootstrapPhase()
    {
        SimulationPhase = WorldSimulationPhase.Bootstrap;
    }

    public void BeginActiveSimulation()
    {
        SimulationPhase = WorldSimulationPhase.Active;
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
        WorldSimulationPhase resolvedPhase = worldEvent.SimulationPhase == WorldSimulationPhase.Bootstrap || IsBootstrapping
            ? WorldSimulationPhase.Bootstrap
            : WorldSimulationPhase.Active;
        WorldEventOrigin resolvedOrigin = worldEvent.Origin != WorldEventOrigin.LiveTransition
            ? worldEvent.Origin
            : resolvedPhase == WorldSimulationPhase.Bootstrap
                ? WorldEventOrigin.BootstrapBaseline
                : WorldEventOrigin.LiveTransition;
        Dictionary<string, string> metadata = CopyMap(worldEvent.Metadata);
        metadata["simulationPhase"] = resolvedPhase.ToString();
        metadata["eventOrigin"] = resolvedOrigin.ToString();

        WorldEvent enriched = new()
        {
            EventId = ++_nextEventId,
            Year = Time.Year,
            Month = Time.Month,
            Season = Time.Season,
            SimulationPhase = resolvedPhase,
            Origin = resolvedOrigin,
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
            RelatedPolitySpeciesId = worldEvent.RelatedPolitySpeciesId,
            RelatedPolitySpeciesName = worldEvent.RelatedPolitySpeciesName,
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
            Metadata = metadata
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
        int? relatedPolitySpeciesId = null,
        string? relatedPolitySpeciesName = null,
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
            RelatedPolitySpeciesId = relatedPolitySpeciesId,
            RelatedPolitySpeciesName = relatedPolitySpeciesName,
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
