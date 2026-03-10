using LivingWorld.Core;

namespace LivingWorld.Presentation;

public sealed class ChronicleEventFormatter
{
    public bool TryFormat(WorldEvent worldEvent, ChronicleFocus focus, out string chronicleLine)
    {
        chronicleLine = string.Empty;

        if (worldEvent.Severity < WorldEventSeverity.Notable)
        {
            return false;
        }

        if (!focus.FocusedPolityId.HasValue)
        {
            return false;
        }

        int focusedPolityId = focus.FocusedPolityId.Value;
        if (!IsFocusedEvent(worldEvent, focusedPolityId))
        {
            return false;
        }

        if (!IsPlayerFacingChronicleEvent(worldEvent))
        {
            return false;
        }

        chronicleLine = WorldEvent.FormatHistoricalEvent(worldEvent.Year, worldEvent.Narrative);
        return true;
    }

    private static bool IsFocusedEvent(WorldEvent worldEvent, int focusedPolityId)
        => worldEvent.PolityId == focusedPolityId || worldEvent.RelatedPolityId == focusedPolityId;

    private static bool IsPlayerFacingChronicleEvent(WorldEvent worldEvent)
    {
        return worldEvent.Type is
            WorldEventType.Migration or
            WorldEventType.SettlementFounded or
            WorldEventType.SettlementConsolidated or
            WorldEventType.KnowledgeDiscovered or
            WorldEventType.FoodStress or
            WorldEventType.PopulationChanged or
            WorldEventType.Fragmentation or
            WorldEventType.StageChanged or
            WorldEventType.PolityCollapsed or
            WorldEventType.FocusHandoffFragmentation or
            WorldEventType.FocusHandoffCollapse or
            WorldEventType.FocusLineageContinued or
            WorldEventType.FocusLineageExtinctFallback;
    }
}
