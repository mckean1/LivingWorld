using LivingWorld.Core;
using LivingWorld.Generation;
using Xunit;

namespace LivingWorld.Tests;

public sealed class PrehistoryRuntimeOrchestratorTests
{
    [Fact]
    public void RecordCheckpointOutcomeTransitionsPhases()
    {
        World world = new(new WorldTime());
        PrehistoryRuntimeOrchestrator orchestrator = new();
        orchestrator.Initialize(world, StartupWorldAgeConfiguration.ForPreset(StartupWorldAgePreset.StandardWorld));
        orchestrator.BeginPrehistoryRunning(world);

        orchestrator.RecordCheckpointOutcome(world, PrehistoryCheckpointOutcome.Continue("keep going"));
        Assert.Equal(PrehistoryRuntimePhase.PrehistoryRunning, world.PrehistoryRuntime.CurrentPhase);

        orchestrator.RecordCheckpointOutcome(world, PrehistoryCheckpointOutcome.EnterFocalSelection("ready"));
        Assert.Equal(PrehistoryRuntimePhase.FocalSelection, world.PrehistoryRuntime.CurrentPhase);

        orchestrator.RecordCheckpointOutcome(world, PrehistoryCheckpointOutcome.ForceEnterFocalSelection("fallback"));
        Assert.Equal(PrehistoryRuntimePhase.FocalSelection, world.PrehistoryRuntime.CurrentPhase);

        orchestrator.RecordCheckpointOutcome(world, PrehistoryCheckpointOutcome.Failure("failed"));
        Assert.Equal(PrehistoryRuntimePhase.GenerationFailure, world.PrehistoryRuntime.CurrentPhase);
    }

    [Fact]
    public void BeginReadinessCheckpointAndActivePlaySetPhasesAndFlags()
    {
        World world = new(new WorldTime());
        PrehistoryRuntimeOrchestrator orchestrator = new();
        orchestrator.Initialize(world, StartupWorldAgeConfiguration.ForPreset(StartupWorldAgePreset.StandardWorld));

        orchestrator.BeginReadinessCheckpoint(
            world,
            phaseLabel: "Checking readiness",
            subphaseLabel: "Checking stop conditions",
            activitySummary: "Inspecting world facts");
        Assert.Equal(PrehistoryRuntimePhase.ReadinessCheckpoint, world.PrehistoryRuntime.CurrentPhase);
        Assert.False(world.PrehistoryRuntime.IsPrehistoryAdvancing);

        orchestrator.BeginActivePlay(world);
        Assert.Equal(PrehistoryRuntimePhase.ActivePlay, world.PrehistoryRuntime.CurrentPhase);
        Assert.True(world.PrehistoryRuntime.AreReadinessChecksActive);
        Assert.False(world.PrehistoryRuntime.IsPrehistoryAdvancing);
    }
}
