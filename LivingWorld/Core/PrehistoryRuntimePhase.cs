namespace LivingWorld.Core;

public enum PrehistoryRuntimePhase
{
    WorldSeeding,
    BiologicalDivergence,
    SocialEmergence,
    WorldReadinessReview,
    FocalSelection,
    SimulationEngineActivePlay,
    GenerationFailure
}

public static class PrehistoryRuntimePhaseDisplayExtensions
{
    public static string ToDisplayString(this PrehistoryRuntimePhase phase)
        => phase switch
        {
            PrehistoryRuntimePhase.WorldSeeding => "World Seeding",
            PrehistoryRuntimePhase.BiologicalDivergence => "Biological Divergence",
            PrehistoryRuntimePhase.SocialEmergence => "Social Emergence",
            PrehistoryRuntimePhase.WorldReadinessReview => "World Readiness Review",
            PrehistoryRuntimePhase.FocalSelection => "Focal Selection",
            PrehistoryRuntimePhase.SimulationEngineActivePlay => "SimulationEngine Active Play",
            PrehistoryRuntimePhase.GenerationFailure => "Generation Failure",
            _ => phase.ToString()
        };

    public static string ToDisplayString(this PrehistoryCheckpointOutcomeKind kind)
        => kind switch
        {
            PrehistoryCheckpointOutcomeKind.ContinuePrehistory => "Continue Prehistory",
            PrehistoryCheckpointOutcomeKind.EnterFocalSelection => "Enter Focal Selection",
            PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection => "Force Enter Focal Selection",
            PrehistoryCheckpointOutcomeKind.GenerationFailure => "Generation Failure",
            _ => kind.ToString()
        };
}
