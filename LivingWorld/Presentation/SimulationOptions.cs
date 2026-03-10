namespace LivingWorld.Presentation;

public sealed class SimulationOptions
{
    public static SimulationOptions NarrativeChronicle(int tickDelayMilliseconds = 250)
        => new()
        {
            OutputMode = OutputMode.Narrative,
            StreamTickChronicle = true,
            TickDelayMilliseconds = tickDelayMilliseconds
        };

    public OutputMode OutputMode { get; init; } = OutputMode.Narrative;

    public bool StreamTickChronicle { get; init; }

    public int TickDelayMilliseconds { get; init; }
}
