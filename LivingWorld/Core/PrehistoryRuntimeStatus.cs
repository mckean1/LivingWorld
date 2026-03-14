namespace LivingWorld.Core;

public sealed class PrehistoryRuntimeStatus
{
    public StartupWorldAgePreset StartupPreset { get; set; } = StartupWorldAgePreset.StandardWorld;
    public PrehistoryRuntimeState CurrentState { get; set; } = PrehistoryRuntimeState.BootstrapWorldFrame;
    public int WorldAgeYears { get; set; }
    public bool AreReadinessChecksActive { get; set; }
    public bool IsPrehistoryAdvancing { get; set; } = true;
    public PrehistoryStopReason? StopReason { get; set; }
    public string StopSummary { get; set; } = "prehistory_not_finished";
}
