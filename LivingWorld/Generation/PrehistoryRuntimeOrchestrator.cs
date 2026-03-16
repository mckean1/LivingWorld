using LivingWorld.Core;

namespace LivingWorld.Generation;

public sealed class PrehistoryRuntimeOrchestrator
{
    public void Initialize(World world, StartupWorldAgeConfiguration configuration)
    {
        world.StartupAgeConfiguration = configuration;
        world.PrehistoryRuntime.StartupPreset = configuration.Preset;
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.WorldSeeding;
        world.PrehistoryRuntime.LastAdvancingPhase = PrehistoryRuntimePhase.WorldSeeding;
        world.PrehistoryRuntime.DetailView = PrehistoryRuntimeDetailView.WorldFrame;
        world.PrehistoryRuntime.WorldAgeYears = world.Time.Year;
        world.PrehistoryRuntime.AreReadinessChecksActive = false;
        world.PrehistoryRuntime.IsPrehistoryAdvancing = true;
        world.PrehistoryRuntime.LastCheckpointOutcome = null;
        Describe(world, "Establishing the initial world frame", "Preparing the first regions", "Laying out the continent, climate, and primitive starting conditions.");
    }

    public void Describe(World world, string phaseLabel, string subphaseLabel, string activitySummary, string? transitionSummary = null)
    {
        world.PrehistoryRuntime.PhaseLabel = phaseLabel;
        world.PrehistoryRuntime.SubphaseLabel = subphaseLabel;
        world.PrehistoryRuntime.ActivitySummary = activitySummary;
        world.PrehistoryRuntime.TransitionSummary = transitionSummary;
    }

    public void BeginAdvancingPhase(World world, PrehistoryRuntimePhase phase)
    {
        var runtime = world.PrehistoryRuntime;
        runtime.CurrentPhase = phase;
        runtime.LastAdvancingPhase = phase;
        runtime.IsPrehistoryAdvancing = true;
        runtime.TransitionSummary = null;
    }

    public void BeginReadinessCheckpoint(World world, string phaseLabel, string subphaseLabel, string activitySummary)
    {
        var runtime = world.PrehistoryRuntime;
        runtime.CurrentPhase = PrehistoryRuntimePhase.WorldReadinessReview;
        runtime.IsPrehistoryAdvancing = false;
        Describe(world, phaseLabel, subphaseLabel, activitySummary);
    }

    public void RecordCheckpointOutcome(World world, PrehistoryCheckpointOutcome outcome, string? transitionSummary = null)
    {
        var runtime = world.PrehistoryRuntime;
        runtime.LastCheckpointOutcome = outcome;
        runtime.TransitionSummary = transitionSummary;
        switch (outcome.Kind)
        {
            case PrehistoryCheckpointOutcomeKind.ContinuePrehistory:
                runtime.CurrentPhase = runtime.LastAdvancingPhase;
                runtime.IsPrehistoryAdvancing = true;
                break;
            case PrehistoryCheckpointOutcomeKind.EnterFocalSelection:
            case PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection:
                runtime.CurrentPhase = PrehistoryRuntimePhase.FocalSelection;
                runtime.DetailView = PrehistoryRuntimeDetailView.FocalSelection;
                runtime.IsPrehistoryAdvancing = false;
                break;
            case PrehistoryCheckpointOutcomeKind.GenerationFailure:
                runtime.CurrentPhase = PrehistoryRuntimePhase.GenerationFailure;
                runtime.DetailView = PrehistoryRuntimeDetailView.GenerationFailure;
                runtime.IsPrehistoryAdvancing = false;
                break;
        }
    }

    public void RecordGenerationFailure(World world, string summary, string? details = null)
    {
        var runtime = world.PrehistoryRuntime;
        runtime.CurrentPhase = PrehistoryRuntimePhase.GenerationFailure;
        runtime.DetailView = PrehistoryRuntimeDetailView.GenerationFailure;
        runtime.IsPrehistoryAdvancing = false;
        runtime.LastCheckpointOutcome = PrehistoryCheckpointOutcome.Failure(summary, details);
        runtime.PhaseLabel = "No viable truthful start was produced";
        runtime.SubphaseLabel = "Candidate pool collapsed before handoff";
        runtime.ActivitySummary = "The simulation could not produce a viable truthful start.";
    }

    public void BeginActivePlay(World world)
    {
        var runtime = world.PrehistoryRuntime;
        runtime.CurrentPhase = PrehistoryRuntimePhase.SimulationEngineActivePlay;
        runtime.DetailView = PrehistoryRuntimeDetailView.ActivePlay;
        runtime.IsPrehistoryAdvancing = false;
        runtime.AreReadinessChecksActive = true;
        Describe(world, "Selected start now running inside the SimulationEngine", "Chronicle active", "The SimulationEngine is now driving the live monthly simulation.");
    }

    public void SetDetailView(World world, PrehistoryRuntimeDetailView detailView)
    {
        world.PrehistoryRuntime.DetailView = detailView;
    }

    public void RefreshAge(World world)
    {
        var runtime = world.PrehistoryRuntime;
        runtime.WorldAgeYears = world.Time.Year;
        runtime.AreReadinessChecksActive = world.Time.Year >= world.StartupAgeConfiguration.MinPrehistoryYears;
    }
}
