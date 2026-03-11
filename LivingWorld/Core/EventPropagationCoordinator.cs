namespace LivingWorld.Core;

public sealed class EventPropagationCoordinator
{
    private readonly IReadOnlyList<IWorldEventHandler> _handlers;
    private readonly int _maxDepth;
    private readonly int _maxEventsPerStep;
    private readonly HashSet<string> _stepDeduplicationKeys = new(StringComparer.Ordinal);
    private readonly Queue<PendingEvent> _pendingEvents = new();
    private bool _isProcessing;

    public EventPropagationCoordinator(
        IEnumerable<IWorldEventHandler> handlers,
        int maxDepth = 4,
        int maxEventsPerStep = 64)
    {
        _handlers = handlers.ToList();
        _maxDepth = Math.Max(0, maxDepth);
        _maxEventsPerStep = Math.Max(1, maxEventsPerStep);
    }

    public void Process(World world, WorldEvent initialEvent, Func<WorldEvent, WorldEvent> recordEvent)
    {
        _pendingEvents.Enqueue(new PendingEvent(initialEvent, null));
        if (_isProcessing)
        {
            return;
        }

        _isProcessing = true;
        _stepDeduplicationKeys.Clear();
        int processedCount = 0;

        try
        {
            while (_pendingEvents.Count > 0 && processedCount < _maxEventsPerStep)
            {
                PendingEvent pendingEvent = _pendingEvents.Dequeue();
                WorldEvent preparedEvent = PrepareEvent(pendingEvent.Event, pendingEvent.ParentEvent);

                string dedupeKey = BuildDedupeKey(preparedEvent);
                if (!_stepDeduplicationKeys.Add(dedupeKey))
                {
                    continue;
                }

                WorldEvent recordedEvent = recordEvent(preparedEvent);
                processedCount++;

                if (recordedEvent.PropagationDepth >= _maxDepth)
                {
                    continue;
                }

                foreach (IWorldEventHandler handler in _handlers)
                {
                    if (!handler.CanHandle(recordedEvent))
                    {
                        continue;
                    }

                    foreach (WorldEvent followUpEvent in handler.Handle(world, recordedEvent))
                    {
                        _pendingEvents.Enqueue(new PendingEvent(followUpEvent, recordedEvent));
                    }
                }
            }
        }
        finally
        {
            _pendingEvents.Clear();
            _stepDeduplicationKeys.Clear();
            _isProcessing = false;
        }
    }

    private static WorldEvent PrepareEvent(WorldEvent worldEvent, WorldEvent? parentEvent)
    {
        if (parentEvent is null)
        {
            return worldEvent with
            {
                PropagationDepth = Math.Max(0, worldEvent.PropagationDepth),
                ParentEventIds = NormalizeIds(worldEvent.ParentEventIds)
            };
        }

        List<long> parentIds = NormalizeIds(worldEvent.ParentEventIds);
        if (!parentIds.Contains(parentEvent.EventId))
        {
            parentIds.Add(parentEvent.EventId);
        }

        return worldEvent with
        {
            Scope = worldEvent.Scope,
            ParentEventIds = parentIds,
            RootEventId = worldEvent.RootEventId ?? parentEvent.RootEventId ?? parentEvent.EventId,
            PropagationDepth = Math.Max(worldEvent.PropagationDepth, parentEvent.PropagationDepth + 1)
        };
    }

    private static List<long> NormalizeIds(IEnumerable<long>? ids)
        => ids is null
            ? []
            : ids.Where(id => id > 0).Distinct().OrderBy(id => id).ToList();

    private static string BuildDedupeKey(WorldEvent worldEvent)
    {
        string parentIds = worldEvent.ParentEventIds.Count == 0
            ? "-"
            : string.Join(",", worldEvent.ParentEventIds);

        return string.Join(
            "|",
            worldEvent.Type,
            worldEvent.Scope,
            worldEvent.Reason ?? string.Empty,
            worldEvent.PolityId?.ToString() ?? "-",
            worldEvent.RelatedPolityId?.ToString() ?? "-",
            worldEvent.RegionId?.ToString() ?? "-",
            worldEvent.SettlementId?.ToString() ?? "-",
            worldEvent.Narrative,
            parentIds);
    }

    private sealed record PendingEvent(WorldEvent Event, WorldEvent? ParentEvent);
}
