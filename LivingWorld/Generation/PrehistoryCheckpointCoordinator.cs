using System.Collections.Generic;
using LivingWorld.Core;

namespace LivingWorld.Generation;

public sealed class PrehistoryCheckpointCoordinator
{
    private readonly PrehistoryRuntimeOrchestrator _runtimeOrchestrator;
    private readonly ICheckpointEvaluationAdapter _evaluationAdapter;

    public PrehistoryCheckpointCoordinator(
        PrehistoryRuntimeOrchestrator runtimeOrchestrator,
        ICheckpointEvaluationAdapter evaluationAdapter)
    {
        _runtimeOrchestrator = runtimeOrchestrator;
        _evaluationAdapter = evaluationAdapter;
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
        PrehistoryCheckpointOutcome outcome = DetermineCheckpointOutcome(world);
        _runtimeOrchestrator.RecordCheckpointOutcome(world, outcome, transitionSummary: outcome.Summary);
        return outcome;
    }

    private PrehistoryCheckpointOutcome DetermineCheckpointOutcome(World world)
    {
        WorldReadinessReport report = world.WorldReadinessReport;
        string? details = FormatCheckpointDetails(report.GlobalBlockingReasons, report.GlobalWarningReasons);
        string summary = report.SummaryData.Headline;
        return report.FinalCheckpointResolution switch
        {
            PrehistoryCheckpointOutcomeKind.EnterFocalSelection => PrehistoryCheckpointOutcome.EnterFocalSelection(summary, details),
            PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection => PrehistoryCheckpointOutcome.ForceEnterFocalSelection(summary, details),
            PrehistoryCheckpointOutcomeKind.GenerationFailure => PrehistoryCheckpointOutcome.Failure(summary, details),
            _ => PrehistoryCheckpointOutcome.Continue(summary, details)
        };
    }

    private static string? FormatCheckpointDetails(IReadOnlyList<string> blockingReasons, IReadOnlyList<string> warningReasons)
    {
        List<string> details = [];
        details.AddRange(blockingReasons.Select(reason => $"blocker:{reason}"));
        details.AddRange(warningReasons.Take(4).Select(reason => $"warning:{reason}"));
        return details.Count == 0 ? null : string.Join(", ", details);
    }
}
