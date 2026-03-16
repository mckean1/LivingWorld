namespace LivingWorld.Core;

public sealed class PrehistoryRuntimeStatus
{
    public StartupWorldAgePreset StartupPreset { get; set; } = StartupWorldAgePreset.StandardWorld;
    public PrehistoryRuntimePhase CurrentPhase { get; set; } = PrehistoryRuntimePhase.WorldSeeding;
    public PrehistoryRuntimePhase LastAdvancingPhase { get; set; } = PrehistoryRuntimePhase.WorldSeeding;
    public PrehistoryRuntimeDetailView DetailView { get; set; } = PrehistoryRuntimeDetailView.WorldFrame;
    public int WorldAgeYears { get; set; }
    public bool AreReadinessChecksActive { get; set; }
    public bool IsPrehistoryAdvancing { get; set; } = true;
    public string PhaseLabel { get; set; } = "Establishing the initial world frame";
    public string SubphaseLabel { get; set; } = "Shaping land, climate, and primitive conditions";
    public string ActivitySummary { get; set; } = "Preparing the world for its first long prehistory pass.";
    public string? TransitionSummary { get; set; }
    public PrehistoryCheckpointOutcome? LastCheckpointOutcome { get; set; }

    public string GetStateKey()
        => string.Concat(CurrentPhase.ToString(), "|", DetailView.ToString(), "|", SubphaseLabel, "|", WorldAgeYears.ToString());
}
