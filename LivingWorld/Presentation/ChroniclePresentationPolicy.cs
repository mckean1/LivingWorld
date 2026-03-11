using LivingWorld.Core;

namespace LivingWorld.Presentation;

public sealed class ChroniclePresentationPolicy
{
    private readonly IReadOnlyDictionary<string, ChronicleCooldownRule> _cooldownRules;

    public ChroniclePresentationPolicy()
    {
        _cooldownRules = new Dictionary<string, ChronicleCooldownRule>(StringComparer.Ordinal)
        {
            [WorldEventType.Migration] = new ChronicleCooldownRule(20, BuildActorScopeKey),
            [WorldEventType.SettlementConsolidated] = new ChronicleCooldownRule(25, BuildActorScopeKey),
            [WorldEventType.FoodStress] = new ChronicleCooldownRule(15, BuildActorScopeKey)
        };
    }

    public WorldEventSeverity MinimumChronicleSeverity => WorldEventSeverity.Major;

    public bool ShouldPresent(
        WorldEvent worldEvent,
        ChronicleFocus focus,
        IReadOnlyDictionary<string, ChroniclePresentationRecord> previouslyPresented,
        out string? presentationKey)
    {
        presentationKey = null;

        if (worldEvent.Severity < MinimumChronicleSeverity)
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

        if (!_cooldownRules.TryGetValue(worldEvent.Type, out ChronicleCooldownRule? cooldownRule))
        {
            return true;
        }

        string? actorScope = cooldownRule.ScopeKeyFactory(worldEvent);
        if (string.IsNullOrWhiteSpace(actorScope))
        {
            return true;
        }

        presentationKey = $"{worldEvent.Type}:{actorScope}";
        if (!previouslyPresented.TryGetValue(presentationKey, out ChroniclePresentationRecord? previousRecord))
        {
            return true;
        }

        if (ShouldBypassCooldown(worldEvent, previousRecord))
        {
            return true;
        }

        return worldEvent.Year - previousRecord.Year >= cooldownRule.Years;
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

    private static bool ShouldBypassCooldown(WorldEvent currentEvent, ChroniclePresentationRecord previousRecord)
    {
        if (currentEvent.Severity > previousRecord.Severity)
        {
            return true;
        }

        return currentEvent.Type switch
        {
            WorldEventType.FoodStress => IsFoodStressTransition(currentEvent),
            _ => false
        };
    }

    private static bool IsFoodStressTransition(WorldEvent worldEvent)
    {
        return worldEvent.Reason is
            "hardship_entered" or
            "hardship_worsened" or
            "hardship_improved" or
            "hardship_recovered";
    }

    private static string? BuildActorScopeKey(WorldEvent worldEvent)
    {
        if (worldEvent.PolityId.HasValue)
        {
            return $"polity:{worldEvent.PolityId.Value}";
        }

        if (worldEvent.SpeciesId.HasValue)
        {
            return $"species:{worldEvent.SpeciesId.Value}";
        }

        if (worldEvent.SettlementId.HasValue)
        {
            return $"settlement:{worldEvent.SettlementId.Value}";
        }

        if (worldEvent.RegionId.HasValue)
        {
            return $"region:{worldEvent.RegionId.Value}";
        }

        if (worldEvent.RelatedPolityId.HasValue)
        {
            return $"relatedPolity:{worldEvent.RelatedPolityId.Value}";
        }

        return null;
    }

    private sealed record ChronicleCooldownRule(int Years, Func<WorldEvent, string?> ScopeKeyFactory);
}

public sealed record ChroniclePresentationRecord(int Year, WorldEventSeverity Severity);
