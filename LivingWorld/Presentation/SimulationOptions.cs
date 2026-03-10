namespace LivingWorld.Presentation;

public sealed class SimulationOptions
{
    public static SimulationOptions NarrativeChronicle(int tickDelayMilliseconds = 1000)
        => new()
        {
            OutputMode = OutputMode.Narrative,
            StreamTickChronicle = false,
            TickDelayMilliseconds = tickDelayMilliseconds,
            PauseBeforeStart = false,
            PauseAfterEachYear = false,
            FocusedChronicleEnabled = true,
            WriteStructuredHistory = true,
            HistoryFilePath = BuildDefaultHistoryFilePath()
        };

    public OutputMode OutputMode { get; init; } = OutputMode.Narrative;

    public bool StreamTickChronicle { get; init; }

    public int TickDelayMilliseconds { get; init; }

    public bool PauseBeforeStart { get; init; }

    public bool PauseAfterEachYear { get; init; }

    public bool FocusedChronicleEnabled { get; init; } = true;

    public int? FocusedPolityId { get; init; }

    public bool WriteStructuredHistory { get; init; } = true;

    public string HistoryFilePath { get; init; } = BuildDefaultHistoryFilePath();

    private static string BuildDefaultHistoryFilePath()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        return Path.Combine("logs", $"history-{timestamp}.jsonl");
    }
}
