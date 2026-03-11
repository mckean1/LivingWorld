using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Presentation;

public sealed class ChronicleFocus
{
    public int? FocusedPolityId { get; private set; }
    public int? FocusedLineageId { get; private set; }

    public bool IsFocusedPolity(int polityId)
        => FocusedPolityId.HasValue && FocusedPolityId.Value == polityId;

    // Ordinary chronicle events follow the currently focused polity. Focus handoff
    // events are the explicit bridge that keeps the chronicle attached to the same
    // historical line as subjects change across fragmentation or collapse.
    public bool IsEventInFocusedLine(WorldEvent worldEvent)
    {
        if (!FocusedPolityId.HasValue)
        {
            return false;
        }

        return IsFocusTransitionEvent(worldEvent)
            ? IsRelevantFocusTransitionEvent(worldEvent)
            : IsEventForFocusedPolity(worldEvent);
    }

    public void SetFocus(int? polityId, int? lineageId)
    {
        FocusedPolityId = polityId;
        FocusedLineageId = lineageId;
    }

    public void SetFocus(Polity? polity)
        => SetFocus(polity?.Id, polity?.LineageId);

    public Polity? ResolvePolity(World world)
    {
        if (!FocusedPolityId.HasValue)
        {
            return null;
        }

        return world.Polities.FirstOrDefault(polity => polity.Id == FocusedPolityId.Value);
    }

    private bool IsRelevantFocusTransitionEvent(WorldEvent worldEvent)
    {
        int focusedPolityId = FocusedPolityId!.Value;
        if (worldEvent.PolityId == focusedPolityId || worldEvent.RelatedPolityId == focusedPolityId)
        {
            return true;
        }

        if (!FocusedLineageId.HasValue)
        {
            return false;
        }

        int focusedLineageId = FocusedLineageId.Value;
        return TryGetInt(worldEvent.Metadata, "previousLineageId", out int previousLineageId) && previousLineageId == focusedLineageId
            || TryGetInt(worldEvent.Metadata, "newLineageId", out int newLineageId) && newLineageId == focusedLineageId
            || TryGetInt(worldEvent.Before, "focusedLineageId", out int beforeFocusedLineageId) && beforeFocusedLineageId == focusedLineageId
            || TryGetInt(worldEvent.After, "focusedLineageId", out int afterFocusedLineageId) && afterFocusedLineageId == focusedLineageId;
    }

    private bool IsEventForFocusedPolity(WorldEvent worldEvent)
    {
        int focusedPolityId = FocusedPolityId!.Value;
        return worldEvent.PolityId == focusedPolityId || worldEvent.RelatedPolityId == focusedPolityId;
    }

    private static bool IsFocusTransitionEvent(WorldEvent worldEvent)
    {
        return worldEvent.Type is
            WorldEventType.FocusHandoffFragmentation or
            WorldEventType.FocusHandoffCollapse or
            WorldEventType.FocusLineageContinued or
            WorldEventType.FocusLineageExtinctFallback;
    }

    private static bool TryGetInt(IReadOnlyDictionary<string, string> values, string key, out int parsedValue)
    {
        parsedValue = default;
        return values.TryGetValue(key, out string? rawValue) && int.TryParse(rawValue, out parsedValue);
    }
}
