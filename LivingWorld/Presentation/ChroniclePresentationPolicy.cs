using LivingWorld.Core;

namespace LivingWorld.Presentation;

public sealed class ChroniclePresentationPolicy
{
    private readonly IReadOnlyDictionary<string, ChronicleCooldownRule> _cooldownRules;

    public ChroniclePresentationPolicy()
    {
        _cooldownRules = new Dictionary<string, ChronicleCooldownRule>(StringComparer.Ordinal)
        {
            [WorldEventType.Migration] = new ChronicleCooldownRule(20, BuildChronicleScopeKey),
            [WorldEventType.SettlementConsolidated] = new ChronicleCooldownRule(25, BuildChronicleScopeKey),
            [WorldEventType.FoodStress] = new ChronicleCooldownRule(15, BuildChronicleScopeKey),
            [WorldEventType.SpeciesPopulationAdaptedToRegion] = new ChronicleCooldownRule(30, BuildAdaptationScopeKey),
            [WorldEventType.SpeciesPopulationMajorMutation] = new ChronicleCooldownRule(24, BuildChronicleScopeKey),
            [WorldEventType.SpeciesPopulationEvolutionaryTurningPoint] = new ChronicleCooldownRule(30, BuildChronicleScopeKey)
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

        if (!focus.IsEventInFocusedLine(worldEvent))
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

    private static bool IsPlayerFacingChronicleEvent(WorldEvent worldEvent)
    {
        return worldEvent.Type is
            WorldEventType.Migration or
            WorldEventType.SettlementFounded or
            WorldEventType.SettlementConsolidated or
            WorldEventType.SettlementStabilized or
            WorldEventType.KnowledgeDiscovered or
            WorldEventType.LearnedAdvancement or
            WorldEventType.FoodStress or
            WorldEventType.FoodStabilized or
            WorldEventType.Fragmentation or
            WorldEventType.PolityFounded or
            WorldEventType.StageChanged or
            WorldEventType.PolityCollapsed or
            WorldEventType.SpeciesPopulationMajorMutation or
            WorldEventType.SpeciesPopulationEvolutionaryTurningPoint or
            WorldEventType.SpeciesPopulationAdaptedToRegion or
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

    private static string? BuildAdaptationScopeKey(WorldEvent worldEvent)
    {
        if (!worldEvent.SpeciesId.HasValue || !worldEvent.RegionId.HasValue || string.IsNullOrWhiteSpace(worldEvent.Reason))
        {
            return null;
        }

        List<string> parts =
        [
            $"species:{worldEvent.SpeciesId.Value}",
            $"region:{worldEvent.RegionId.Value}",
            $"reason:{worldEvent.Reason}"
        ];

        if (worldEvent.Metadata.TryGetValue("adaptationMilestone", out string? milestone) && !string.IsNullOrWhiteSpace(milestone))
        {
            parts.Add($"milestone:{milestone}");
        }

        if (worldEvent.Metadata.TryGetValue("adaptationStage", out string? stage) && !string.IsNullOrWhiteSpace(stage))
        {
            parts.Add($"stage:{stage}");
        }

        if (worldEvent.Metadata.TryGetValue("adaptationSignal", out string? signal) && !string.IsNullOrWhiteSpace(signal))
        {
            parts.Add($"signal:{signal}");
        }

        return string.Join(":", parts);
    }

    // Scope keys are presentation-only throttling keys. Prefer the primary actor
    // whose visible story beat is being suppressed so unrelated actors do not
    // accidentally mute one another and future species-facing events can plug in cleanly.
    private static string? BuildChronicleScopeKey(WorldEvent worldEvent)
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
