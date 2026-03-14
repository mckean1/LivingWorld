using LivingWorld.Core;

namespace LivingWorld.Generation;

public sealed class PrehistoryRuntimeController
{
    public void Initialize(World world, StartupWorldAgeConfiguration configuration)
    {
        world.StartupAgeConfiguration = configuration;
        world.PrehistoryRuntime.StartupPreset = configuration.Preset;
        world.PrehistoryRuntime.CurrentState = PrehistoryRuntimeState.BootstrapWorldFrame;
        world.PrehistoryRuntime.WorldAgeYears = world.Time.Year;
        world.PrehistoryRuntime.AreReadinessChecksActive = false;
        world.PrehistoryRuntime.IsPrehistoryAdvancing = true;
        world.PrehistoryRuntime.StopReason = null;
        world.PrehistoryRuntime.StopSummary = "prehistory_running";
    }

    public void Transition(World world, PrehistoryRuntimeState state)
    {
        world.PrehistoryRuntime.CurrentState = state;
        world.PrehistoryRuntime.WorldAgeYears = world.Time.Year;
    }

    public void RefreshAge(World world)
    {
        world.PrehistoryRuntime.WorldAgeYears = world.Time.Year;
        world.PrehistoryRuntime.AreReadinessChecksActive = world.Time.Year >= world.StartupAgeConfiguration.MinPrehistoryYears;
    }

    public void Stop(World world, PrehistoryStopReason stopReason, string stopSummary)
    {
        world.PrehistoryRuntime.IsPrehistoryAdvancing = false;
        world.PrehistoryRuntime.StopReason = stopReason;
        world.PrehistoryRuntime.StopSummary = stopSummary;
        world.PrehistoryStopReason = stopReason;
        world.PrehistoryStopSummary = stopSummary;
    }

    public void EnterFocalSelection(World world)
    {
        world.PrehistoryRuntime.CurrentState = PrehistoryRuntimeState.FocalSelection;
        world.PrehistoryRuntime.IsPrehistoryAdvancing = false;
    }

    public void BeginActivePlay(World world)
    {
        world.PrehistoryRuntime.CurrentState = PrehistoryRuntimeState.ActivePlay;
        world.PrehistoryRuntime.IsPrehistoryAdvancing = false;
        world.PrehistoryRuntime.AreReadinessChecksActive = true;
    }
}
