namespace LivingWorld.Presentation;

public sealed class SimulationOptions
{
    public static SimulationOptions ChronicleWatch(
        int playbackDelayMilliseconds = 150,
        int visibleEntryLimit = 8)
        => new()
        {
            OutputMode = OutputMode.Watch,
            ChroniclePlaybackDelayMilliseconds = Math.Max(0, playbackDelayMilliseconds),
            ChronicleVisibleEntryLimit = Math.Max(1, visibleEntryLimit),
            PauseBeforeStart = false,
            PauseAfterEachYear = false,
            WriteStructuredHistory = true,
            HistoryFilePath = BuildDefaultHistoryFilePath()
        };

    public OutputMode OutputMode { get; init; } = OutputMode.Watch;

    public int ChroniclePlaybackDelayMilliseconds { get; init; }

    public int ChronicleVisibleEntryLimit { get; init; } = 8;

    public bool PauseBeforeStart { get; init; }

    public bool PauseAfterEachYear { get; init; }

    public bool FocusedChronicleEnabled { get; init; } = true;

    public int? FocusedPolityId { get; init; }

    public bool WriteStructuredHistory { get; init; } = true;

    public string HistoryFilePath { get; init; } = BuildDefaultHistoryFilePath();

    public bool EnablePerformanceInstrumentation { get; init; }

    private static string BuildDefaultHistoryFilePath()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        return Path.Combine("logs", $"history-{timestamp}-{Guid.NewGuid():N}.jsonl");
    }
}
