using LivingWorld.Core;

namespace LivingWorld.Presentation;

public sealed class ChroniclePresentationPolicy
{
    private const int MinimumVisibilityScore = 5;
    private readonly IReadOnlyDictionary<string, ChronicleEventProfile> _profiles;

    public ChroniclePresentationPolicy()
    {
        _profiles = new Dictionary<string, ChronicleEventProfile>(StringComparer.Ordinal)
        {
            [WorldEventType.Migration] = new ChronicleEventProfile(
                BasePriority: 4,
                SameStateCooldownYears: 20,
                ChangedStateCooldownYears: 8,
                BuildPrimaryPolityScopeKey,
                BuildMigrationStateKey),
            [WorldEventType.SettlementConsolidated] = new ChronicleEventProfile(
                BasePriority: 4,
                SameStateCooldownYears: 25,
                ChangedStateCooldownYears: 12,
                BuildPrimaryPolityScopeKey,
                BuildSettlementStateKey),
            [WorldEventType.SettlementStabilized] = new ChronicleEventProfile(
                BasePriority: 2,
                SameStateCooldownYears: 18,
                ChangedStateCooldownYears: 8,
                BuildPrimaryPolityScopeKey,
                BuildSettlementStateKey),
            [WorldEventType.FoodStress] = new ChronicleEventProfile(
                BasePriority: 4,
                SameStateCooldownYears: 15,
                ChangedStateCooldownYears: 0,
                BuildPrimaryPolityScopeKey,
                BuildHardshipStateKey),
            [WorldEventType.FoodStabilized] = new ChronicleEventProfile(
                BasePriority: 3,
                SameStateCooldownYears: 12,
                ChangedStateCooldownYears: 3,
                BuildPrimaryPolityScopeKey,
                BuildHardshipStateKey),
            [WorldEventType.StageChanged] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 30,
                ChangedStateCooldownYears: 0,
                BuildPrimaryPolityScopeKey,
                BuildStageStateKey),
            [WorldEventType.SpeciesPopulationAdaptedToRegion] = new ChronicleEventProfile(
                BasePriority: 3,
                SameStateCooldownYears: 30,
                ChangedStateCooldownYears: 0,
                BuildSpeciesRegionScopeKey,
                BuildAdaptationStateKey),
            [WorldEventType.SpeciesPopulationMajorMutation] = new ChronicleEventProfile(
                BasePriority: 4,
                SameStateCooldownYears: 24,
                ChangedStateCooldownYears: 6,
                BuildSpeciesRegionScopeKey,
                BuildEcologyTransitionStateKey),
            [WorldEventType.SpeciesPopulationEvolutionaryTurningPoint] = new ChronicleEventProfile(
                BasePriority: 4,
                SameStateCooldownYears: 30,
                ChangedStateCooldownYears: 0,
                BuildSpeciesRegionScopeKey,
                BuildEcologyTransitionStateKey),
            [WorldEventType.NewSpeciesAppeared] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 40,
                ChangedStateCooldownYears: 0,
                BuildSpeciesRegionScopeKey,
                BuildEcologyTransitionStateKey)
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

        ChronicleEventProfile profile = ResolveProfile(worldEvent);
        if (!MeetsVisibilityBar(worldEvent, profile))
        {
            return false;
        }

        string? actorScope = profile.ScopeKeyFactory(worldEvent);
        if (string.IsNullOrWhiteSpace(actorScope))
        {
            return true;
        }

        presentationKey = $"{worldEvent.Type}:{actorScope}";
        string? stateKey = profile.StateKeyFactory?.Invoke(worldEvent);

        if (!previouslyPresented.TryGetValue(presentationKey, out ChroniclePresentationRecord? previousRecord))
        {
            return true;
        }

        if (ShouldBypassCooldown(worldEvent, previousRecord))
        {
            return true;
        }

        bool sameState = string.Equals(stateKey, previousRecord.StateKey, StringComparison.Ordinal);
        int requiredGap = sameState
            ? profile.SameStateCooldownYears
            : profile.ChangedStateCooldownYears;
        return worldEvent.Year - previousRecord.Year >= requiredGap;
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
            WorldEventType.NewSpeciesAppeared or
            WorldEventType.FocusHandoffFragmentation or
            WorldEventType.FocusHandoffCollapse or
            WorldEventType.FocusLineageContinued or
            WorldEventType.FocusLineageExtinctFallback;
    }

    private ChronicleEventProfile ResolveProfile(WorldEvent worldEvent)
        => _profiles.TryGetValue(worldEvent.Type, out ChronicleEventProfile? profile)
            ? profile
            : new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 0,
                ChangedStateCooldownYears: 0,
                BuildChronicleScopeKey,
                StateKeyFactory: null);

    private static bool MeetsVisibilityBar(WorldEvent worldEvent, ChronicleEventProfile profile)
        => profile.BasePriority + ResolveSeverityWeight(worldEvent.Severity) >= MinimumVisibilityScore;

    private static int ResolveSeverityWeight(WorldEventSeverity severity)
        => severity switch
        {
            WorldEventSeverity.Legendary => 3,
            WorldEventSeverity.Major => 2,
            _ => 0
        };

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

    private static string? BuildAdaptationStateKey(WorldEvent worldEvent)
        => BuildAdaptationScopeKey(worldEvent);

    private static string? BuildHardshipStateKey(WorldEvent worldEvent)
    {
        string reason = worldEvent.Reason ?? string.Empty;
        string hardshipTier = TryGetValue(worldEvent.After, "hardshipTier")
            ?? TryGetValue(worldEvent.Metadata, "hardshipTier")
            ?? string.Empty;
        string transitionKind = TryGetValue(worldEvent.Metadata, "transitionKind") ?? reason;
        return $"{transitionKind}:{hardshipTier}";
    }

    private static string? BuildMigrationStateKey(WorldEvent worldEvent)
    {
        string fromRegion = TryGetValue(worldEvent.Before, "regionId") ?? string.Empty;
        string toRegion = worldEvent.RegionId?.ToString() ?? TryGetValue(worldEvent.After, "regionId") ?? string.Empty;
        return $"{worldEvent.Reason ?? string.Empty}:{fromRegion}->{toRegion}";
    }

    private static string? BuildSettlementStateKey(WorldEvent worldEvent)
    {
        string location = worldEvent.SettlementId?.ToString()
            ?? worldEvent.RegionId?.ToString()
            ?? string.Empty;
        return $"{worldEvent.Reason ?? string.Empty}:{location}";
    }

    private static string? BuildStageStateKey(WorldEvent worldEvent)
        => $"{worldEvent.Reason ?? string.Empty}:{TryGetValue(worldEvent.After, "stage") ?? string.Empty}";

    private static string? BuildEcologyTransitionStateKey(WorldEvent worldEvent)
    {
        string milestone = TryGetValue(worldEvent.Metadata, "adaptationMilestone")
            ?? TryGetValue(worldEvent.Metadata, "milestone")
            ?? TryGetValue(worldEvent.Metadata, "divergenceMilestone")
            ?? string.Empty;
        return $"{worldEvent.Reason ?? string.Empty}:{worldEvent.RegionId?.ToString() ?? string.Empty}:{milestone}";
    }

    private static string? BuildPrimaryPolityScopeKey(WorldEvent worldEvent)
    {
        if (worldEvent.PolityId.HasValue)
        {
            return $"polity:{worldEvent.PolityId.Value}";
        }

        if (worldEvent.RelatedPolityId.HasValue)
        {
            return $"relatedPolity:{worldEvent.RelatedPolityId.Value}";
        }

        return BuildChronicleScopeKey(worldEvent);
    }

    private static string? BuildSpeciesRegionScopeKey(WorldEvent worldEvent)
    {
        if (worldEvent.SpeciesId.HasValue && worldEvent.RegionId.HasValue)
        {
            return $"species:{worldEvent.SpeciesId.Value}:region:{worldEvent.RegionId.Value}";
        }

        if (worldEvent.SpeciesId.HasValue)
        {
            return $"species:{worldEvent.SpeciesId.Value}";
        }

        return BuildChronicleScopeKey(worldEvent);
    }

    private static string? TryGetValue(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

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

    private sealed record ChronicleEventProfile(
        int BasePriority,
        int SameStateCooldownYears,
        int ChangedStateCooldownYears,
        Func<WorldEvent, string?> ScopeKeyFactory,
        Func<WorldEvent, string?>? StateKeyFactory);
}

public sealed record ChroniclePresentationRecord(int Year, WorldEventSeverity Severity, string? StateKey);
