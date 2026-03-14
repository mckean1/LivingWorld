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
        world.PrehistoryRuntime.PhaseLabel = "Generating world frame";
        world.PrehistoryRuntime.SubphaseLabel = "Preparing the first regions";
        world.PrehistoryRuntime.ActivitySummary = "Laying out the continent, climate, and primitive starting conditions.";
        world.PrehistoryRuntime.TransitionSummary = null;
    }

    public void Transition(World world, PrehistoryRuntimeState state)
    {
        world.PrehistoryRuntime.CurrentState = state;
        world.PrehistoryRuntime.WorldAgeYears = world.Time.Year;
        world.PrehistoryRuntime.TransitionSummary = null;
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
        world.PrehistoryRuntime.PhaseLabel = "World generation complete";
        world.PrehistoryRuntime.SubphaseLabel = "Building focal starts";
        world.PrehistoryRuntime.ActivitySummary = "Preparing the final candidate starts for selection.";
        world.PrehistoryRuntime.TransitionSummary = "World generation complete: viable starts are ready.";
    }

    public void BeginActivePlay(World world)
    {
        world.PrehistoryRuntime.CurrentState = PrehistoryRuntimeState.ActivePlay;
        world.PrehistoryRuntime.IsPrehistoryAdvancing = false;
        world.PrehistoryRuntime.AreReadinessChecksActive = true;
        world.PrehistoryRuntime.PhaseLabel = "Active play";
        world.PrehistoryRuntime.SubphaseLabel = "Chronicle active";
        world.PrehistoryRuntime.ActivitySummary = "The live chronicle has begun.";
        world.PrehistoryRuntime.TransitionSummary = null;
    }

    public void Describe(World world, string phaseLabel, string subphaseLabel, string activitySummary, string? transitionSummary = null)
    {
        world.PrehistoryRuntime.PhaseLabel = phaseLabel;
        world.PrehistoryRuntime.SubphaseLabel = subphaseLabel;
        world.PrehistoryRuntime.ActivitySummary = activitySummary;
        world.PrehistoryRuntime.TransitionSummary = transitionSummary;
    }
}
