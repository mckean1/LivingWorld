using System.Collections.Generic;
using LivingWorld.Core;

namespace LivingWorld.Generation;

public sealed class PrehistoryCheckpointCoordinator
{
    private readonly PrehistoryRuntimeOrchestrator _runtimeOrchestrator;
    private readonly ICheckpointEvaluationAdapter _evaluationAdapter;
    private readonly ICandidateOutcomeEvaluator _outcomeEvaluator;
    private readonly WorldGenerationSettings _settings;

    public PrehistoryCheckpointCoordinator(
        PrehistoryRuntimeOrchestrator runtimeOrchestrator,
        ICheckpointEvaluationAdapter evaluationAdapter,
        ICandidateOutcomeEvaluator outcomeEvaluator,
        WorldGenerationSettings settings)
    {
        _runtimeOrchestrator = runtimeOrchestrator;
        _evaluationAdapter = evaluationAdapter;
        _outcomeEvaluator = outcomeEvaluator;
        _settings = settings;
    }

    public PrehistoryCheckpointOutcome Evaluate(
        World world,
        string phaseLabel,
        string subphaseLabel,
        string activitySummary,
        string completionSummary,
        bool allowEmergencyFallback,
        IReadOnlyList<string>? regenerationReasons = null)
    {
        _runtimeOrchestrator.BeginReadinessCheckpoint(world, phaseLabel, subphaseLabel, activitySummary);
        PrehistoryCheckpointEvaluationResult evaluation = _evaluationAdapter.Evaluate(world, allowEmergencyFallback, regenerationReasons);
        world.PrehistoryEvaluation.ApplyCheckpointEvaluation(evaluation);
        PrehistoryCheckpointOutcome outcome = DetermineCheckpointOutcome(world, allowEmergencyFallback, completionSummary);
        _runtimeOrchestrator.RecordCheckpointOutcome(world, outcome, transitionSummary: outcome.Summary);
        return outcome;
    }

    private PrehistoryCheckpointOutcome DetermineCheckpointOutcome(World world, bool allowEmergencyFallback, string completionSummary)
    {
        bool accepted = _outcomeEvaluator.ShouldSurfaceFocalSelection(world, _settings, allowEmergencyFallback, out List<string> rejectionReasons);
        string? details = FormatCheckpointDetails(rejectionReasons);

        if (accepted)
        {
            PrehistoryCheckpointOutcomeKind kind = allowEmergencyFallback
                ? PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection
                : PrehistoryCheckpointOutcomeKind.EnterFocalSelection;
            string summary = allowEmergencyFallback ? "candidate_fallback_after_max_age" : completionSummary;
            return new PrehistoryCheckpointOutcome(kind, summary, details);
        }

        if (allowEmergencyFallback && world.PlayerEntryCandidates.Count == 0)
        {
            return PrehistoryCheckpointOutcome.Failure("generation_failed_no_candidates", details);
        }

        return PrehistoryCheckpointOutcome.Continue("prehistory_continues", details);
    }

    private static string? FormatCheckpointDetails(IReadOnlyList<string> rejectionReasons)
        => rejectionReasons.Count == 0 ? null : string.Join(", ", rejectionReasons);
}
