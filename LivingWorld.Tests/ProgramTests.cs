using LivingWorld.Presentation;
using Xunit;

namespace LivingWorld.Tests;

public sealed class ProgramTests
{
    [Fact]
    public void PauseBeforeStart_DoesNotUseConsolePrompt_InWatchMode()
    {
        SimulationOptions options = new()
        {
            OutputMode = OutputMode.Watch,
            PauseBeforeStart = true
        };

        Assert.False(Program.ShouldPromptBeforeStart(options));
    }

    [Fact]
    public void PauseBeforeStart_StillUsesConsolePrompt_OutsideWatchMode()
    {
        SimulationOptions options = new()
        {
            OutputMode = OutputMode.Debug,
            PauseBeforeStart = true
        };

        Assert.True(Program.ShouldPromptBeforeStart(options));
    }
}
