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

    [Fact]
    public void ResolveSeed_UsesRequestedSeed_WhenProvided()
    {
        Assert.Equal(12345, Program.ResolveSeed(12345));
    }

    [Fact]
    public void ResolveSeed_GeneratesPositiveSeed_WhenNotProvided()
    {
        int seed = Program.ResolveSeed(null);

        Assert.InRange(seed, 1, int.MaxValue - 1);
    }
}
