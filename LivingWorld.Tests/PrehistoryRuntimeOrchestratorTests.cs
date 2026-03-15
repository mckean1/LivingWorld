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
        Assert.Equal(PrehistoryRuntimeDetailView.FocalSelection, world.PrehistoryRuntime.DetailView);
        Assert.False(world.PrehistoryRuntime.IsPrehistoryAdvancing);

        orchestrator.RecordCheckpointOutcome(world, PrehistoryCheckpointOutcome.ForceEnterFocalSelection("fallback"));
        Assert.Equal(PrehistoryRuntimePhase.FocalSelection, world.PrehistoryRuntime.CurrentPhase);
        Assert.False(world.PrehistoryRuntime.IsPrehistoryAdvancing);

        orchestrator.RecordCheckpointOutcome(world, PrehistoryCheckpointOutcome.Failure("failed"));
        Assert.Equal(PrehistoryRuntimePhase.GenerationFailure, world.PrehistoryRuntime.CurrentPhase);
        Assert.Equal(PrehistoryRuntimeDetailView.GenerationFailure, world.PrehistoryRuntime.DetailView);
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
        Assert.Equal(PrehistoryRuntimeDetailView.ActivePlay, world.PrehistoryRuntime.DetailView);
        Assert.True(world.PrehistoryRuntime.AreReadinessChecksActive);
        Assert.False(world.PrehistoryRuntime.IsPrehistoryAdvancing);
    }

    [Fact]
    public void RecordCheckpointOutcomeContinueKeepsPrehistoryRunning()
    {
        World world = new(new WorldTime());
        PrehistoryRuntimeOrchestrator orchestrator = new();
        orchestrator.Initialize(world, StartupWorldAgeConfiguration.ForPreset(StartupWorldAgePreset.StandardWorld));
        orchestrator.BeginPrehistoryRunning(world);

        orchestrator.RecordCheckpointOutcome(world, PrehistoryCheckpointOutcome.Continue("still running"));

        Assert.Equal(PrehistoryRuntimePhase.PrehistoryRunning, world.PrehistoryRuntime.CurrentPhase);
        Assert.True(world.PrehistoryRuntime.IsPrehistoryAdvancing);
    }

    [Fact]
    public void RecordGenerationFailureDefinesTerminalPhase()
    {
        World world = new(new WorldTime());
        PrehistoryRuntimeOrchestrator orchestrator = new();
        orchestrator.Initialize(world, StartupWorldAgeConfiguration.ForPreset(StartupWorldAgePreset.StandardWorld));

        orchestrator.RecordGenerationFailure(world, "no candidates");

        Assert.Equal(PrehistoryRuntimePhase.GenerationFailure, world.PrehistoryRuntime.CurrentPhase);
        Assert.Equal(PrehistoryRuntimeDetailView.GenerationFailure, world.PrehistoryRuntime.DetailView);
        Assert.False(world.PrehistoryRuntime.IsPrehistoryAdvancing);
        Assert.Equal(PrehistoryCheckpointOutcomeKind.GenerationFailure, world.PrehistoryRuntime.LastCheckpointOutcome?.Kind);
        Assert.Equal("no candidates", world.PrehistoryRuntime.LastCheckpointOutcome?.Summary);
    }
}
