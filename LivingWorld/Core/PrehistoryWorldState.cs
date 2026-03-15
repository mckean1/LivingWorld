namespace LivingWorld.Core;

public sealed class PrehistoryWorldState
{
    public StartupWorldAgeConfiguration AgeConfiguration { get; set; } = StartupWorldAgeConfiguration.ForPreset(StartupWorldAgePreset.StandardWorld);
    public PrehistoryRuntimeStatus Runtime { get; } = new();
    public PrehistoryEvaluationSnapshot Evaluation { get; } = new();
    public ActivePlayHandoffState ActivePlayHandoff { get; } = new();
    public PrehistoryFocalSelectionPresentationState FocalSelectionPresentation { get; } = new();
}
