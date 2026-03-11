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
            _presentedEvents[presentationKey] = new ChroniclePresentationRecord(worldEvent.Year, worldEvent.Severity);
        }

        return true;
    }
}
