namespace LivingWorld.Core;

public sealed class PrehistoryWorldState
{
    public StartupWorldAgeConfiguration AgeConfiguration { get; set; } = StartupWorldAgeConfiguration.ForPreset(StartupWorldAgePreset.StandardWorld);
    public PrehistoryRuntimeStatus Runtime { get; } = new();
    public PrehistoryEvaluationSnapshot Evaluation { get; } = new();
    public PrehistoryLegacyEvaluationArtifacts LegacyCompatibility => Evaluation.LegacyCompatibility;
    public PrehistoryCandidateSelectionState CandidateSelection => Evaluation.CandidateSelection;
    public ActivePlayHandoffState ActivePlayHandoff { get; } = new();
    public PrehistoryFocalSelectionPresentationState FocalSelectionPresentation { get; } = new();
}
