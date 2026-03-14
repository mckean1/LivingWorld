namespace LivingWorld.Core;

public sealed class PrehistoryRuntimeStatus
{
    public StartupWorldAgePreset StartupPreset { get; set; } = StartupWorldAgePreset.StandardWorld;
    public PrehistoryRuntimePhase CurrentPhase { get; set; } = PrehistoryRuntimePhase.BootstrapWorldFrame;
    public int WorldAgeYears { get; set; }
    public bool AreReadinessChecksActive { get; set; }
    public bool IsPrehistoryAdvancing { get; set; } = true;
    public string PhaseLabel { get; set; } = "Preparing world generation";
    public string SubphaseLabel { get; set; } = "Bootstrapping startup";
    public string ActivitySummary { get; set; } = "Preparing the world for its first long prehistory pass.";
    public string? TransitionSummary { get; set; }
    public PrehistoryCheckpointOutcome? LastCheckpointOutcome { get; set; }

    public string GetStateKey()
        => string.Concat(CurrentPhase.ToString(), "|", SubphaseLabel, "|", WorldAgeYears.ToString());
}
