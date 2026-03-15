using LivingWorld.Core;

namespace LivingWorld.Generation;

public static class WorldGenerationDiagnosticsEvaluator
{
    public static GenerationAttemptDiagnosticsSummary BuildAttemptSummary(World world)
    {
        StartupOutcomeDiagnostics diagnostics = world.StartupOutcomeDiagnostics;
        WorldReadinessReport report = world.WorldReadinessReport;

        return new GenerationAttemptDiagnosticsSummary(
            world.StartupGenerationAttempt + 1,
            world.Time.Year,
            world.PrehistoryRuntime.CurrentPhase,
            world.PrehistoryRuntime.SubphaseLabel,
            world.PrehistoryRuntime.LastCheckpointOutcome?.Kind ?? report.FinalCheckpointResolution,
            report.CandidatePoolSummary.TotalViableCandidatesDiscovered,
            report.CandidatePoolSummary.TotalSurfacedCandidates,
            report.CandidatePoolSummary.NormalReadyCandidateCount,
            new GenerationAttemptPopulationSnapshot(
                diagnostics.OrganicSentientGroupCount,
                diagnostics.FallbackSentientGroupCount,
                diagnostics.OrganicSocietyCount,
                diagnostics.FallbackSocietyCount,
                diagnostics.OrganicSettlementCount,
                diagnostics.FallbackSettlementCount,
                diagnostics.OrganicPolityCount,
                diagnostics.FallbackPolityCount,
                diagnostics.OrganicFocalCandidateCount,
                diagnostics.FallbackFocalCandidateCount,
                diagnostics.OrganicPlayerEntryCandidateCount,
                diagnostics.FallbackPlayerEntryCandidateCount,
                diagnostics.EmergencyAdmittedCandidateCount),
            diagnostics.PrimaryFailureKind,
            diagnostics.ZeroViableCause,
            diagnostics.ReasonSummary,
            diagnostics.Bottlenecks,
            diagnostics.CandidateRejections,
            diagnostics.RegenerationReasons);
    }

    public static IReadOnlyList<StartupDiagnosticReason> BuildRegenerationReasons(GenerationAttemptDiagnosticsSummary summary)
    {
        int attemptIndex = Math.Max(0, summary.AttemptNumber - 1);
        List<StartupDiagnosticReason> reasons = summary.RankedBottlenecks
            .Take(3)
            .Select(reason => new StartupDiagnosticReason(
                StartupDiagnosticReasonKind.Regeneration,
                $"attempt_{attemptIndex}:{reason.Code}"))
            .ToList();

        if (summary.TotalViableCandidatesDiscovered == 0)
        {
            reasons.AddRange(summary.RankedCandidateRejections
                .Take(2)
                .Select(reason => new StartupDiagnosticReason(
                    StartupDiagnosticReasonKind.Regeneration,
                    $"attempt_{attemptIndex}:rejection:{reason.Code}")));
        }

        return reasons
            .DistinctBy(reason => reason.Code, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
    }

    public static void ApplyAttemptHistory(
        World world,
        IReadOnlyList<GenerationAttemptDiagnosticsSummary> attemptHistory,
        GenerationFailurePostmortem? postmortem)
    {
        world.WorldGenerationDiagnostics.ReplaceAttemptHistory(attemptHistory);
        world.WorldGenerationDiagnostics.SetFinalFailurePostmortem(postmortem);
    }

    public static GenerationFailurePostmortem BuildFailurePostmortem(IReadOnlyList<GenerationAttemptDiagnosticsSummary> attemptHistory)
    {
        if (attemptHistory.Count == 0)
        {
            throw new ArgumentException("Attempt history is required.", nameof(attemptHistory));
        }

        GenerationAttemptDiagnosticsSummary finalAttempt = attemptHistory[^1];
        IReadOnlyList<GenerationAggregateReasonCount> repeatedBottlenecks = AggregateReasons(
            attemptHistory,
            summary => summary.RankedBottlenecks);
        IReadOnlyList<GenerationAggregateReasonCount> repeatedCandidateRejections = AggregateReasons(
            attemptHistory,
            summary => summary.RankedCandidateRejections);
        GenerationFailurePatternKind failurePattern = DetermineFailurePattern(attemptHistory, repeatedBottlenecks);

        return new GenerationFailurePostmortem(
            finalAttempt.ReasonSummary,
            "Generation failed honestly: maximum age was reached without any truthful viable starts.",
            finalAttempt.PrimaryFailureKind,
            finalAttempt.ZeroViableCause,
            finalAttempt,
            repeatedBottlenecks.Take(5).ToArray(),
            repeatedCandidateRejections.Take(5).ToArray(),
            failurePattern,
            failurePattern switch
            {
                GenerationFailurePatternKind.SingleAttempt => "Only one generation attempt completed, so there is no cross-attempt pattern yet.",
                GenerationFailurePatternKind.StablePattern => "Attempts repeated the same dominant bottlenecks across regeneration attempts.",
                _ => "Attempts failed for different mixes of bottlenecks across regeneration attempts."
            });
    }

    private static IReadOnlyList<GenerationAggregateReasonCount> AggregateReasons(
        IReadOnlyList<GenerationAttemptDiagnosticsSummary> attemptHistory,
        Func<GenerationAttemptDiagnosticsSummary, IReadOnlyList<StartupDiagnosticReasonCount>> selector)
    {
        return attemptHistory
            .SelectMany(summary => selector(summary).Select(reason => (summary.AttemptNumber, Reason: reason)))
            .GroupBy(entry => (entry.Reason.Kind, entry.Reason.Code))
            .Select(group => new GenerationAggregateReasonCount(
                group.Key.Kind,
                group.Key.Code,
                group.Sum(entry => entry.Reason.Count),
                group.Select(entry => entry.AttemptNumber).Distinct().Count()))
            .OrderByDescending(entry => entry.AttemptCount)
            .ThenByDescending(entry => entry.TotalCount)
            .ThenBy(entry => entry.Code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static GenerationFailurePatternKind DetermineFailurePattern(
        IReadOnlyList<GenerationAttemptDiagnosticsSummary> attemptHistory,
        IReadOnlyList<GenerationAggregateReasonCount> repeatedBottlenecks)
    {
        if (attemptHistory.Count <= 1)
        {
            return GenerationFailurePatternKind.SingleAttempt;
        }

        IReadOnlyList<GenerationFailurePrimaryKind> primaryKinds = attemptHistory
            .Select(summary => summary.PrimaryFailureKind)
            .Where(kind => kind != GenerationFailurePrimaryKind.None)
            .Distinct()
            .ToArray();
        bool stablePrimaryFailure = primaryKinds.Count == 1;
        bool stableTopBottleneck = repeatedBottlenecks.FirstOrDefault()?.AttemptCount == attemptHistory.Count;

        return stablePrimaryFailure || stableTopBottleneck
            ? GenerationFailurePatternKind.StablePattern
            : GenerationFailurePatternKind.VariedPattern;
    }
}
