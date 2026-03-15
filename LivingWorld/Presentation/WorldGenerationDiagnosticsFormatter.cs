using LivingWorld.Core;

namespace LivingWorld.Presentation;

internal static class WorldGenerationDiagnosticsFormatter
{
    public static IReadOnlyList<string> BuildCheckpointDiagnostics(StartupOutcomeDiagnostics diagnostics, WorldReadinessReport report)
    {
        List<string> lines =
        [
            $"Summary: {diagnostics.ReasonSummary}",
            $"Primary diagnosis: {DescribePrimaryFailure(diagnostics.PrimaryFailureKind)}",
            $"Candidates: viable {report.CandidatePoolSummary.TotalViableCandidatesDiscovered} | surfaced {report.CandidatePoolSummary.TotalSurfacedCandidates} | normal-ready {report.CandidatePoolSummary.NormalReadyCandidateCount}"
        ];

        string bottlenecks = FormatReasonCounts(diagnostics.Bottlenecks, 3);
        if (!string.IsNullOrWhiteSpace(bottlenecks))
        {
            lines.Add($"Bottlenecks: {bottlenecks}");
        }
        else
        {
            string rejections = FormatReasonCounts(diagnostics.CandidateRejections, 3);
            if (!string.IsNullOrWhiteSpace(rejections))
            {
                lines.Add($"Rejections: {rejections}");
            }
        }

        return lines;
    }

    public static IReadOnlyList<string> BuildAttemptSummaryLines(GenerationAttemptDiagnosticsSummary summary, bool willRegenerate)
    {
        List<string> lines =
        [
            $"Generation attempt {summary.AttemptNumber}",
            $"Outcome: {summary.Outcome} | age {summary.WorldAgeYears} | phase {summary.FinalPhase} / {summary.FinalSubphase}",
            $"Reason: {summary.ReasonSummary}",
            $"Candidates: viable {summary.TotalViableCandidatesDiscovered} | surfaced {summary.SurfacedCandidateCount} | normal-ready {summary.NormalReadyCandidateCount}",
            $"Organic world: societies {summary.Population.OrganicSocietyCount} | settlements {summary.Population.OrganicSettlementCount} | polities {summary.Population.OrganicPolityCount} | focal candidates {summary.Population.OrganicFocalCandidateCount}"
        ];

        string bottlenecks = FormatReasonCounts(summary.RankedBottlenecks, 3);
        if (!string.IsNullOrWhiteSpace(bottlenecks))
        {
            lines.Add($"Top bottlenecks: {bottlenecks}");
        }

        string rejections = FormatReasonCounts(summary.RankedCandidateRejections, 3);
        if (!string.IsNullOrWhiteSpace(rejections))
        {
            lines.Add($"Top rejections: {rejections}");
        }

        if (willRegenerate)
        {
            lines.Add("Decision: regeneration triggered for another honest attempt.");
        }

        return lines;
    }

    public static IReadOnlyList<string> BuildFinalFailureLines(GenerationFailurePostmortem postmortem)
    {
        GenerationAttemptDiagnosticsSummary finalAttempt = postmortem.FinalAttempt;
        List<string> lines =
        [
            "Generation Failure Postmortem",
            postmortem.HonestFailureStatement,
            $"Summary: {postmortem.ShortSummary}",
            $"Primary diagnosis: {DescribePrimaryFailure(postmortem.PrimaryFailureKind)}",
            $"Final attempt: age {finalAttempt.WorldAgeYears} | viable {finalAttempt.TotalViableCandidatesDiscovered} | surfaced {finalAttempt.SurfacedCandidateCount} | normal-ready {finalAttempt.NormalReadyCandidateCount}",
            $"Organic counts: societies {finalAttempt.Population.OrganicSocietyCount} | settlements {finalAttempt.Population.OrganicSettlementCount} | polities {finalAttempt.Population.OrganicPolityCount} | focal candidates {finalAttempt.Population.OrganicFocalCandidateCount} | pool size {finalAttempt.TotalViableCandidatesDiscovered}",
            $"Zero viable starts because: {DescribeZeroViableCause(postmortem.ZeroViableCause)}"
        ];

        string finalBottlenecks = FormatReasonCounts(finalAttempt.RankedBottlenecks, 4);
        if (!string.IsNullOrWhiteSpace(finalBottlenecks))
        {
            lines.Add($"Final bottlenecks: {finalBottlenecks}");
        }

        string finalRejections = FormatReasonCounts(finalAttempt.RankedCandidateRejections, 4);
        if (!string.IsNullOrWhiteSpace(finalRejections))
        {
            lines.Add($"Final rejections: {finalRejections}");
        }

        lines.Add($"Across attempts: {postmortem.FailurePatternSummary}");

        string repeatedBottlenecks = FormatAggregateReasons(postmortem.RepeatedBottlenecks, 4);
        if (!string.IsNullOrWhiteSpace(repeatedBottlenecks))
        {
            lines.Add($"Repeated bottlenecks: {repeatedBottlenecks}");
        }

        string repeatedRejections = FormatAggregateReasons(postmortem.RepeatedCandidateRejections, 4);
        if (!string.IsNullOrWhiteSpace(repeatedRejections))
        {
            lines.Add($"Repeated rejections: {repeatedRejections}");
        }

        return lines;
    }

    public static string BuildAttemptTransitionSummary(GenerationAttemptDiagnosticsSummary summary, bool willRegenerate)
        => willRegenerate
            ? $"Attempt {summary.AttemptNumber} ended: {summary.ReasonSummary}. Regenerating with a new seed."
            : $"Attempt {summary.AttemptNumber} ended: {summary.ReasonSummary}.";

    public static IEnumerable<WorldEvent> BuildStructuredHistoryEvents(World world)
    {
        foreach (GenerationAttemptDiagnosticsSummary summary in world.GenerationAttemptHistory)
        {
            yield return BuildAttemptHistoryEvent(summary);
        }

        if (world.GenerationFailurePostmortem is GenerationFailurePostmortem postmortem)
        {
            yield return BuildFailureHistoryEvent(postmortem);
        }
    }

    private static WorldEvent BuildAttemptHistoryEvent(GenerationAttemptDiagnosticsSummary summary)
    {
        return new WorldEvent
        {
            Year = summary.WorldAgeYears,
            Month = 12,
            Season = Season.Winter,
            SimulationPhase = WorldSimulationPhase.Bootstrap,
            Origin = WorldEventOrigin.BootstrapBaseline,
            Type = WorldEventType.WorldEvent,
            Severity = summary.Outcome == PrehistoryCheckpointOutcomeKind.GenerationFailure
                ? WorldEventSeverity.Major
                : WorldEventSeverity.Notable,
            Scope = WorldEventScope.World,
            Narrative = $"Generation attempt {summary.AttemptNumber} ended with {summary.Outcome}.",
            Details = string.Join(Environment.NewLine, BuildAttemptSummaryLines(summary, willRegenerate: false)),
            Reason = NormalizeKey(summary.PrimaryFailureKind.ToString()),
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["diagnosticKind"] = "world_generation_attempt_summary",
                ["attemptNumber"] = summary.AttemptNumber.ToString(),
                ["outcome"] = summary.Outcome.ToString(),
                ["primaryFailureKind"] = summary.PrimaryFailureKind.ToString(),
                ["worldAgeYears"] = summary.WorldAgeYears.ToString(),
                ["viableCandidates"] = summary.TotalViableCandidatesDiscovered.ToString(),
                ["surfacedCandidates"] = summary.SurfacedCandidateCount.ToString(),
                ["normalReadyCandidates"] = summary.NormalReadyCandidateCount.ToString(),
                ["organicSocieties"] = summary.Population.OrganicSocietyCount.ToString(),
                ["organicSettlements"] = summary.Population.OrganicSettlementCount.ToString(),
                ["organicPolities"] = summary.Population.OrganicPolityCount.ToString(),
                ["organicFocalCandidates"] = summary.Population.OrganicFocalCandidateCount.ToString(),
                ["candidateRejections"] = FormatReasonCounts(summary.RankedCandidateRejections, 5),
                ["bottlenecks"] = FormatReasonCounts(summary.RankedBottlenecks, 5)
            }
        };
    }

    private static WorldEvent BuildFailureHistoryEvent(GenerationFailurePostmortem postmortem)
    {
        return new WorldEvent
        {
            Year = postmortem.FinalAttempt.WorldAgeYears,
            Month = 12,
            Season = Season.Winter,
            SimulationPhase = WorldSimulationPhase.Bootstrap,
            Origin = WorldEventOrigin.BootstrapBaseline,
            Type = WorldEventType.WorldEvent,
            Severity = WorldEventSeverity.Legendary,
            Scope = WorldEventScope.World,
            Narrative = "World generation failed honestly after all regeneration attempts.",
            Details = string.Join(Environment.NewLine, BuildFinalFailureLines(postmortem)),
            Reason = NormalizeKey(postmortem.PrimaryFailureKind.ToString()),
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["diagnosticKind"] = "world_generation_failure_postmortem",
                ["failurePattern"] = postmortem.FailurePattern.ToString(),
                ["primaryFailureKind"] = postmortem.PrimaryFailureKind.ToString(),
                ["zeroViableCause"] = postmortem.ZeroViableCause.ToString(),
                ["repeatedBottlenecks"] = FormatAggregateReasons(postmortem.RepeatedBottlenecks, 5),
                ["repeatedRejections"] = FormatAggregateReasons(postmortem.RepeatedCandidateRejections, 5)
            }
        };
    }

    private static string FormatReasonCounts(IReadOnlyList<StartupDiagnosticReasonCount> reasons, int limit)
        => string.Join(", ", reasons
            .Take(limit)
            .Select(reason => reason.Count > 1
                ? $"{DescribeReason(reason.Kind, reason.Code)} x{reason.Count}"
                : DescribeReason(reason.Kind, reason.Code)));

    private static string FormatAggregateReasons(IReadOnlyList<GenerationAggregateReasonCount> reasons, int limit)
        => string.Join(", ", reasons
            .Take(limit)
            .Select(reason => $"{DescribeReason(reason.Kind, reason.Code)} ({reason.AttemptCount}/{reason.TotalCount})"));

    private static string DescribePrimaryFailure(GenerationFailurePrimaryKind primaryFailureKind)
        => primaryFailureKind switch
        {
            GenerationFailurePrimaryKind.PhaseBBiologicalReadinessStall => "Phase B biological readiness stall",
            GenerationFailurePrimaryKind.PhaseCSocialEmergenceBottleneck => "Phase C social emergence bottleneck",
            GenerationFailurePrimaryKind.FinalCandidateViabilityCollapse => "final candidate viability collapse",
            GenerationFailurePrimaryKind.MixedOrInconclusive => "mixed or inconclusive failure pattern",
            _ => "no dominant failure"
        };

    private static string DescribeZeroViableCause(GenerationZeroViableCause cause)
    {
        if (cause == GenerationZeroViableCause.None)
        {
            return "no zero-viable failure cause was recorded";
        }

        List<string> parts = [];
        if (cause.HasFlag(GenerationZeroViableCause.NoDurableSocialWorld))
        {
            parts.Add("no durable social world emerged");
        }

        if (cause.HasFlag(GenerationZeroViableCause.CandidateTruthFloorCollapse))
        {
            parts.Add("candidates existed but all failed truth floors");
        }

        if (cause.HasFlag(GenerationZeroViableCause.BiologyOrSocialReadinessNeverMaturedEnough))
        {
            parts.Add("biology or social readiness never matured enough");
        }

        return string.Join(" + ", parts);
    }

    private static string DescribeReason(StartupDiagnosticReasonKind kind, string code)
    {
        string category = kind switch
        {
            StartupDiagnosticReasonKind.PhaseBReadiness => "Phase B",
            StartupDiagnosticReasonKind.PhaseBDiagnostics => "Phase B diag",
            StartupDiagnosticReasonKind.PhaseCReadiness => "Phase C",
            StartupDiagnosticReasonKind.CandidateReadiness => "Candidate",
            StartupDiagnosticReasonKind.Regeneration => "Regen",
            _ => "World"
        };
        return $"{category} {HumanizeCode(code)}";
    }

    private static string HumanizeCode(string code)
    {
        Dictionary<string, string> overrides = new(StringComparer.OrdinalIgnoreCase)
        {
            ["no_viable_candidates"] = "no viable truthful candidates",
            ["candidate_pool_not_yet_ready"] = "candidate pool not ready",
            ["candidate_pool_viable_but_not_normal_ready"] = "candidate pool viable but not normal-entry ready",
            ["no_organic_persistent_societies"] = "no organic persistent societies",
            ["no_organic_polities"] = "no organic polities",
            ["settlement_depth_not_ready"] = "settlement depth not ready",
            ["biological_foundation_below_truth_floor"] = "biological foundation below truth floor",
            ["hard_gate:current_support"] = "current support truth floor failed",
            ["hard_gate:continuity_floor"] = "continuity truth floor failed",
            ["hard_gate:movement_or_rooting_floor"] = "movement or rootedness truth floor failed",
            ["hard_veto:severe_unsupported_current_month"] = "current month support crash",
            ["hard_veto:population_below_minimum_demographic_viability"] = "population below demographic truth floor",
            ["pool_fill:lower_ranked_viable_candidate"] = "lower-ranked viable candidate suppressed by pool fill"
        };

        if (overrides.TryGetValue(code, out string? overrideText))
        {
            return overrideText;
        }

        return code
            .Replace(':', ' ')
            .Replace('_', ' ')
            .Trim();
    }

    private static string NormalizeKey(string value)
        => string.Concat(value.Select(character => char.IsUpper(character)
            ? $"_{char.ToLowerInvariant(character)}"
            : char.ToLowerInvariant(character).ToString())).TrimStart('_');
}
