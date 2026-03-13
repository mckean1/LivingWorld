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
            [WorldEventType.FoodAidSent] = new ChronicleEventProfile(
                BasePriority: 4,
                SameStateCooldownYears: 6,
                ChangedStateCooldownYears: 2,
                BuildSettlementAidScopeKey,
                BuildSettlementAidStateKey),
            [WorldEventType.FamineRelief] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 8,
                ChangedStateCooldownYears: 0,
                BuildSettlementAidScopeKey,
                BuildSettlementAidStateKey),
            [WorldEventType.AidFailed] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 10,
                ChangedStateCooldownYears: 0,
                BuildSettlementAidScopeKey,
                BuildSettlementAidStateKey),
            [WorldEventType.SpeciesDomesticationCandidateIdentified] = new ChronicleEventProfile(
                BasePriority: 4,
                SameStateCooldownYears: 16,
                ChangedStateCooldownYears: 6,
                BuildManagedFoodScopeKey,
                BuildManagedFoodStateKey),
            [WorldEventType.PlantCultivationDiscovered] = new ChronicleEventProfile(
                BasePriority: 4,
                SameStateCooldownYears: 16,
                ChangedStateCooldownYears: 6,
                BuildManagedFoodScopeKey,
                BuildManagedFoodStateKey),
            [WorldEventType.AnimalDomesticated] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 18,
                ChangedStateCooldownYears: 0,
                BuildManagedFoodScopeKey,
                BuildManagedFoodStateKey),
            [WorldEventType.CropEstablished] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 18,
                ChangedStateCooldownYears: 0,
                BuildManagedFoodScopeKey,
                BuildManagedFoodStateKey),
            [WorldEventType.DomesticationSpread] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 12,
                ChangedStateCooldownYears: 4,
                BuildManagedFoodScopeKey,
                BuildManagedFoodStateKey),
            [WorldEventType.AgricultureStabilizedFoodSupply] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 10,
                ChangedStateCooldownYears: 0,
                BuildPrimaryPolityScopeKey,
                BuildManagedFoodStateKey),
            [WorldEventType.MaterialDiscovered] = new ChronicleEventProfile(
                BasePriority: 4,
                SameStateCooldownYears: 20,
                ChangedStateCooldownYears: 6,
                BuildMaterialScopeKey,
                BuildMaterialStateKey),
            [WorldEventType.MaterialShortageStarted] = new ChronicleEventProfile(
                BasePriority: 4,
                SameStateCooldownYears: 12,
                ChangedStateCooldownYears: 0,
                BuildMaterialScopeKey,
                BuildMaterialStateKey),
            [WorldEventType.MaterialShortageWorsened] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 10,
                ChangedStateCooldownYears: 0,
                BuildMaterialScopeKey,
                BuildMaterialStateKey),
            [WorldEventType.MaterialShortageResolved] = new ChronicleEventProfile(
                BasePriority: 4,
                SameStateCooldownYears: 10,
                ChangedStateCooldownYears: 0,
                BuildMaterialScopeKey,
                BuildMaterialStateKey),
            [WorldEventType.MaterialCrisisStarted] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 12,
                ChangedStateCooldownYears: 0,
                BuildMaterialCrisisScopeKey,
                BuildMaterialCrisisStateKey),
            [WorldEventType.MaterialCrisisWorsened] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 10,
                ChangedStateCooldownYears: 0,
                BuildMaterialCrisisScopeKey,
                BuildMaterialCrisisStateKey),
            [WorldEventType.MaterialCrisisResolved] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 10,
                ChangedStateCooldownYears: 0,
                BuildMaterialCrisisScopeKey,
                BuildMaterialCrisisStateKey),
            [WorldEventType.MaterialConvoySent] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 8,
                ChangedStateCooldownYears: 2,
                BuildMaterialScopeKey,
                BuildMaterialStateKey),
            [WorldEventType.MaterialConvoyFailed] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 10,
                ChangedStateCooldownYears: 0,
                BuildMaterialScopeKey,
                BuildMaterialStateKey),
            [WorldEventType.ProductionMilestone] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 18,
                ChangedStateCooldownYears: 0,
                BuildMaterialScopeKey,
                BuildMaterialStateKey),
            [WorldEventType.SettlementSpecialized] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 24,
                ChangedStateCooldownYears: 0,
                BuildMaterialScopeKey,
                BuildMaterialStateKey),
            [WorldEventType.PreservationEstablished] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 20,
                ChangedStateCooldownYears: 0,
                BuildMaterialScopeKey,
                BuildMaterialStateKey),
            [WorldEventType.ToolmakingEstablished] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 20,
                ChangedStateCooldownYears: 0,
                BuildMaterialScopeKey,
                BuildMaterialStateKey),
            [WorldEventType.MaterialHighlyValued] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 12,
                ChangedStateCooldownYears: 4,
                BuildMaterialScopeKey,
                BuildMaterialStateKey),
            [WorldEventType.ProductionFocusShifted] = new ChronicleEventProfile(
                BasePriority: 4,
                SameStateCooldownYears: 8,
                ChangedStateCooldownYears: 2,
                BuildMaterialScopeKey,
                BuildMaterialStateKey),
            [WorldEventType.ProductionBottleneckHit] = new ChronicleEventProfile(
                BasePriority: 4,
                SameStateCooldownYears: 10,
                ChangedStateCooldownYears: 2,
                BuildMaterialScopeKey,
                BuildMaterialStateKey),
            [WorldEventType.TradeGoodEstablished] = new ChronicleEventProfile(
                BasePriority: 5,
                SameStateCooldownYears: 20,
                ChangedStateCooldownYears: 0,
                BuildMaterialScopeKey,
                BuildMaterialStateKey),
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

    public string? ResolveStateKey(WorldEvent worldEvent)
    {
        ChronicleEventProfile profile = ResolveProfile(worldEvent);
        return profile.StateKeyFactory?.Invoke(worldEvent) ?? BuildNarrativeStateKey(worldEvent);
    }

    public string? BuildPlayerFacingDedupKey(WorldEvent worldEvent)
    {
        if (worldEvent.IsBootstrapEvent || worldEvent.Severity < MinimumChronicleSeverity || !IsPlayerFacingChronicleEvent(worldEvent))
        {
            return null;
        }

        ChronicleEventProfile profile = ResolveProfile(worldEvent);
        string? actorScope = profile.ScopeKeyFactory(worldEvent) ?? BuildChronicleScopeKey(worldEvent);
        string stateKey = ResolveStateKey(worldEvent) ?? BuildNarrativeStateKey(worldEvent) ?? string.Empty;
        return string.Join(":", new[]
        {
            worldEvent.Year.ToString(),
            worldEvent.Type,
            actorScope ?? "global",
            stateKey
        });
    }

    public bool ShouldPresent(
        WorldEvent worldEvent,
        ChronicleFocus focus,
        IReadOnlyDictionary<string, ChroniclePresentationRecord> previouslyPresented,
        out string? presentationKey)
    {
        presentationKey = null;

        if (worldEvent.IsBootstrapEvent)
        {
            return false;
        }

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
        string? stateKey = ResolveStateKey(worldEvent);

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
            WorldEventType.FoodAidSent or
            WorldEventType.FamineRelief or
            WorldEventType.AidFailed or
            WorldEventType.SpeciesDomesticationCandidateIdentified or
            WorldEventType.PlantCultivationDiscovered or
            WorldEventType.AnimalDomesticated or
            WorldEventType.CropEstablished or
            WorldEventType.DomesticationSpread or
            WorldEventType.AgricultureStabilizedFoodSupply or
            WorldEventType.MaterialDiscovered or
            WorldEventType.MaterialShortageStarted or
            WorldEventType.MaterialShortageWorsened or
            WorldEventType.MaterialShortageResolved or
            WorldEventType.MaterialCrisisStarted or
            WorldEventType.MaterialCrisisWorsened or
            WorldEventType.MaterialCrisisResolved or
            WorldEventType.MaterialConvoySent or
            WorldEventType.MaterialConvoyFailed or
            WorldEventType.ProductionMilestone or
            WorldEventType.SettlementSpecialized or
            WorldEventType.PreservationEstablished or
            WorldEventType.ToolmakingEstablished or
            WorldEventType.MaterialHighlyValued or
            WorldEventType.ProductionFocusShifted or
            WorldEventType.ProductionBottleneckHit or
            WorldEventType.TradeGoodEstablished or
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
                SameStateCooldownYears: 18,
                ChangedStateCooldownYears: 0,
                BuildChronicleScopeKey,
                BuildNarrativeStateKey);

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

    private static string? BuildSettlementAidStateKey(WorldEvent worldEvent)
        => string.Join(":", new[]
        {
            worldEvent.Reason ?? TryGetValue(worldEvent.Metadata, "cause") ?? string.Empty,
            worldEvent.SettlementId?.ToString() ?? string.Empty,
            TryGetValue(worldEvent.Metadata, "senderSettlementId") ?? string.Empty,
            TryGetValue(worldEvent.After, "foodState") ?? string.Empty,
            TryGetValue(worldEvent.After, "starvationStage") ?? TryGetValue(worldEvent.Metadata, "starvationStage") ?? string.Empty
        });

    private static string? BuildSettlementAidScopeKey(WorldEvent worldEvent)
    {
        string receiverKey = worldEvent.SettlementId?.ToString()
            ?? worldEvent.RegionId?.ToString()
            ?? string.Empty;
        string senderKey = TryGetValue(worldEvent.Metadata, "senderSettlementId") ?? string.Empty;
        return $"receiver:{receiverKey}:sender:{senderKey}";
    }

    private static string? BuildManagedFoodStateKey(WorldEvent worldEvent)
        => string.Join(":", new[]
        {
            worldEvent.Reason ?? string.Empty,
            TryGetValue(worldEvent.Metadata, "targetSpeciesId") ?? string.Empty,
            TryGetValue(worldEvent.Metadata, "managedKind") ?? string.Empty,
            TryGetValue(worldEvent.Metadata, "cropName") ?? TryGetValue(worldEvent.Metadata, "variantName") ?? string.Empty
        });

    private static string? BuildManagedFoodScopeKey(WorldEvent worldEvent)
    {
        string settlementKey = worldEvent.SettlementId?.ToString()
            ?? worldEvent.RegionId?.ToString()
            ?? string.Empty;
        string targetSpeciesKey = TryGetValue(worldEvent.Metadata, "targetSpeciesId") ?? string.Empty;
        return $"settlement:{settlementKey}:species:{targetSpeciesKey}";
    }

    private static string? BuildMaterialStateKey(WorldEvent worldEvent)
        => string.Join(":", new[]
        {
            worldEvent.Reason ?? string.Empty,
            worldEvent.SettlementId?.ToString() ?? string.Empty,
            TryGetValue(worldEvent.Metadata, "materialType")
                ?? TryGetValue(worldEvent.After, "materialType")
                ?? string.Empty,
            TryGetValue(worldEvent.Metadata, "specializationTag")
                ?? TryGetValue(worldEvent.Metadata, "shortageBand")
                ?? TryGetValue(worldEvent.Metadata, "toolTier")
                ?? string.Empty
        });

    private static string? BuildMaterialScopeKey(WorldEvent worldEvent)
    {
        string settlementKey = worldEvent.SettlementId?.ToString()
            ?? worldEvent.RegionId?.ToString()
            ?? string.Empty;
        string materialKey = TryGetValue(worldEvent.Metadata, "materialType")
            ?? TryGetValue(worldEvent.After, "materialType")
            ?? string.Empty;
        return $"settlement:{settlementKey}:material:{materialKey}";
    }

    private static string? BuildMaterialCrisisStateKey(WorldEvent worldEvent)
        => string.Join(":", new[]
        {
            worldEvent.Reason ?? string.Empty,
            TryGetValue(worldEvent.Metadata, "groupedMaterials") ?? string.Empty,
            TryGetValue(worldEvent.Metadata, "groupedCount") ?? string.Empty
        });

    private static string? BuildMaterialCrisisScopeKey(WorldEvent worldEvent)
    {
        string settlementKey = worldEvent.SettlementId?.ToString()
            ?? worldEvent.RegionId?.ToString()
            ?? string.Empty;
        return $"settlement:{settlementKey}:material-crisis";
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

    private static string? BuildNarrativeStateKey(WorldEvent worldEvent)
        => $"{worldEvent.Reason ?? string.Empty}:{WorldEvent.NormalizeNarrative(worldEvent.Narrative)}";

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
