using LivingWorld.Core;

namespace LivingWorld.Presentation;

public sealed class ChronicleEventFormatter
{
    private readonly ChroniclePresentationPolicy _presentationPolicy = new();
    private readonly Dictionary<string, ChroniclePresentationRecord> _presentedEvents = new(StringComparer.Ordinal);

    public bool TryFormat(WorldEvent worldEvent, ChronicleFocus focus, out string chronicleLine)
    {
        chronicleLine = string.Empty;

        if (!_presentationPolicy.ShouldPresent(worldEvent, focus, _presentedEvents, out string? presentationKey))
        {
            return false;
        }

        chronicleLine = WorldEvent.FormatHistoricalEvent(worldEvent.Year, worldEvent.Narrative);

        if (!string.IsNullOrWhiteSpace(presentationKey))
        {
            _presentedEvents[presentationKey] = new ChroniclePresentationRecord(
                worldEvent.Year,
                worldEvent.Severity,
                ResolvePresentationStateKey(worldEvent));
        }

        return true;
    }

    private static string? ResolvePresentationStateKey(WorldEvent worldEvent)
    {
        return worldEvent.Type switch
        {
            WorldEventType.FoodStress or WorldEventType.FoodStabilized =>
                $"{worldEvent.Reason ?? string.Empty}:{TryGetValue(worldEvent.After, "hardshipTier") ?? TryGetValue(worldEvent.Metadata, "hardshipTier") ?? string.Empty}",
            WorldEventType.Migration =>
                $"{worldEvent.Reason ?? string.Empty}:{TryGetValue(worldEvent.Before, "regionId") ?? string.Empty}->{worldEvent.RegionId?.ToString() ?? TryGetValue(worldEvent.After, "regionId") ?? string.Empty}",
            WorldEventType.SettlementConsolidated or WorldEventType.SettlementStabilized =>
                $"{worldEvent.Reason ?? string.Empty}:{worldEvent.SettlementId?.ToString() ?? worldEvent.RegionId?.ToString() ?? string.Empty}",
            WorldEventType.StageChanged =>
                $"{worldEvent.Reason ?? string.Empty}:{TryGetValue(worldEvent.After, "stage") ?? string.Empty}",
            WorldEventType.SpeciesPopulationAdaptedToRegion =>
                string.Join(":", new[]
                {
                    $"species:{worldEvent.SpeciesId?.ToString() ?? string.Empty}",
                    $"region:{worldEvent.RegionId?.ToString() ?? string.Empty}",
                    $"reason:{worldEvent.Reason ?? string.Empty}",
                    $"milestone:{TryGetValue(worldEvent.Metadata, "adaptationMilestone") ?? string.Empty}",
                    $"stage:{TryGetValue(worldEvent.Metadata, "adaptationStage") ?? string.Empty}",
                    $"signal:{TryGetValue(worldEvent.Metadata, "adaptationSignal") ?? string.Empty}"
                }),
            WorldEventType.SpeciesPopulationMajorMutation or
            WorldEventType.SpeciesPopulationEvolutionaryTurningPoint or
            WorldEventType.NewSpeciesAppeared =>
                $"{worldEvent.Reason ?? string.Empty}:{worldEvent.RegionId?.ToString() ?? string.Empty}:{TryGetValue(worldEvent.Metadata, "milestone") ?? TryGetValue(worldEvent.Metadata, "divergenceMilestone") ?? TryGetValue(worldEvent.Metadata, "adaptationMilestone") ?? string.Empty}",
            _ => null
        };
    }

    private static string? TryGetValue(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
}
