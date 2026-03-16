using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Generation;

public static class StartupOutcomeDiagnosticsEvaluator
{
    public static StartupOutcomeDiagnostics Evaluate(
        World world,
        IReadOnlyList<PlayerEntryCandidateSummary>? candidates = null,
        IReadOnlyDictionary<int, string>? candidateRejectionReasons = null,
        WorldReadinessReport? worldReadinessReport = null,
        IReadOnlyList<string>? regenerationReasons = null)
    {
        IReadOnlyList<PlayerEntryCandidateSummary> effectiveCandidates = candidates ?? world.PlayerEntryCandidates;
        IReadOnlyDictionary<int, string> effectiveRejectionReasons = candidateRejectionReasons ?? world.CandidateRejectionReasons;
        WorldReadinessReport effectiveReadinessReport = worldReadinessReport ?? world.WorldReadinessReport;
        Dictionary<int, Polity> politiesById = world.Polities.ToDictionary(polity => polity.Id);

        int organicFocalCandidateCount = world.PhaseCReadinessReport.OrganicViableFocalCandidateCount;
        int fallbackFocalCandidateCount = world.PhaseCReadinessReport.FallbackViableFocalCandidateCount;
        IReadOnlyList<StartupDiagnosticReasonCount> candidateRejectionCounts = effectiveRejectionReasons.Values
            .GroupBy(reason => reason, StringComparer.OrdinalIgnoreCase)
            .Select(group => new StartupDiagnosticReasonCount(
                StartupDiagnosticReasonKind.CandidateReadiness,
                group.Key,
                group.Count()))
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Code, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<StartupDiagnosticReasonCount> bottlenecks = [];
        bottlenecks.AddRange(world.PhaseBReadinessReport.FailureReasons.Select(reason => new StartupDiagnosticReasonCount(
            StartupDiagnosticReasonKind.PhaseBReadiness,
            reason,
            1)));
        bottlenecks.AddRange(world.PhaseBDiagnostics.WeaknessReasons.Select(reason => new StartupDiagnosticReasonCount(
            StartupDiagnosticReasonKind.PhaseBDiagnostics,
            reason,
            1)));
        bottlenecks.AddRange(world.PhaseCReadinessReport.FailureReasons.Select(reason => new StartupDiagnosticReasonCount(
            StartupDiagnosticReasonKind.PhaseCReadiness,
            reason,
            1)));
        bottlenecks.AddRange(effectiveReadinessReport.FailureReasons.Select(reason => new StartupDiagnosticReasonCount(
            StartupDiagnosticReasonKind.CandidateReadiness,
            reason,
            1)));

        if (world.Polities.All(polity => polity.IsFallbackCreated || polity.Population <= 0))
        {
            bottlenecks.Add(new StartupDiagnosticReasonCount(StartupDiagnosticReasonKind.Inferred, "no_organic_polities", 1));
        }

        if (effectiveCandidates.Count > 0 && effectiveCandidates.All(candidate => candidate.IsFallbackCandidate))
        {
            bottlenecks.Add(new StartupDiagnosticReasonCount(StartupDiagnosticReasonKind.Inferred, "no_organic_player_entry_candidates", 1));
        }

        if (effectiveReadinessReport.CandidatePoolSummary.TotalViableCandidatesDiscovered < 2)
        {
            bottlenecks.Add(new StartupDiagnosticReasonCount(
                StartupDiagnosticReasonKind.Inferred,
                $"candidate_pool_size:{effectiveReadinessReport.CandidatePoolSummary.TotalViableCandidatesDiscovered}",
                1));
        }

        IReadOnlyList<StartupDiagnosticReasonCount> rankedBottlenecks = bottlenecks
            .GroupBy(reason => (reason.Kind, reason.Code))
            .Select(group => new StartupDiagnosticReasonCount(group.Key.Kind, group.Key.Code, group.Sum(reason => reason.Count)))
            .OrderByDescending(reason => reason.Count)
            .ThenBy(reason => Priority(reason.Kind))
            .ThenBy(reason => reason.Code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        IReadOnlyList<StartupDiagnosticReason> typedRegenerationReasons = (regenerationReasons ?? Array.Empty<string>())
            .Select(reason => new StartupDiagnosticReason(StartupDiagnosticReasonKind.Regeneration, reason))
            .ToArray();
        GenerationFailurePrimaryKind primaryFailureKind = DeterminePrimaryFailure(world, effectiveReadinessReport, effectiveRejectionReasons);
        GenerationZeroViableCause zeroViableCause = DetermineZeroViableCause(world, effectiveReadinessReport, effectiveRejectionReasons);
        string reasonSummary = BuildReasonSummary(primaryFailureKind, effectiveReadinessReport, zeroViableCause);

        return new StartupOutcomeDiagnostics(
            world.SentientGroups.Count(group => !group.IsCollapsed && !group.IsFallbackCreated),
            world.SentientGroups.Count(group => !group.IsCollapsed && group.IsFallbackCreated),
            world.Societies.Count(society => HasActiveSocietalSubstrate(world, society) && !society.IsFallbackCreated),
            world.Societies.Count(society => HasActiveSocietalSubstrate(world, society) && society.IsFallbackCreated),
            world.SocialSettlements.Count(settlement => !settlement.IsAbandoned && !settlement.IsFallbackCreated),
            world.SocialSettlements.Count(settlement => !settlement.IsAbandoned && settlement.IsFallbackCreated),
            world.Polities.Count(polity => polity.Population > 0 && !polity.IsFallbackCreated),
            world.Polities.Count(polity => polity.Population > 0 && polity.IsFallbackCreated),
            organicFocalCandidateCount,
            fallbackFocalCandidateCount,
            effectiveCandidates.Count(candidate => !candidate.IsFallbackCandidate),
            effectiveCandidates.Count(candidate => candidate.IsFallbackCandidate),
            effectiveCandidates.Count(candidate => candidate.IsEmergencyAdmitted),
            candidateRejectionCounts,
            rankedBottlenecks,
            typedRegenerationReasons,
            primaryFailureKind,
            zeroViableCause,
            reasonSummary);
    }

    private static int Priority(StartupDiagnosticReasonKind kind)
        => kind switch
        {
            StartupDiagnosticReasonKind.PhaseBReadiness => 0,
            StartupDiagnosticReasonKind.PhaseBDiagnostics => 1,
            StartupDiagnosticReasonKind.PhaseCReadiness => 2,
            StartupDiagnosticReasonKind.CandidateReadiness => 3,
            StartupDiagnosticReasonKind.Inferred => 4,
            _ => 5
        };

    private static GenerationFailurePrimaryKind DeterminePrimaryFailure(
        World world,
        WorldReadinessReport report,
        IReadOnlyDictionary<int, string> candidateRejectionReasons)
    {
        if (report.IsReady)
        {
            return GenerationFailurePrimaryKind.None;
        }

        int phaseBScore = world.PhaseBReadinessReport.FailureReasons.Count + world.PhaseBDiagnostics.WeaknessReasons.Count;
        int phaseCScore = world.PhaseCReadinessReport.FailureReasons.Count;
        int candidateScore = report.GetCategory(WorldReadinessCategoryKind.CandidateReadiness).Blockers.Count
            + candidateRejectionReasons.Count;

        if (candidateScore > 0
            && world.PhaseCReadinessReport.OrganicPolityCount > 0
            && candidateScore >= phaseBScore
            && candidateScore >= phaseCScore)
        {
            return GenerationFailurePrimaryKind.FinalCandidateViabilityCollapse;
        }

        if (phaseCScore > 0
            && phaseCScore >= phaseBScore
            && (world.PhaseCReadinessReport.OrganicPersistentSocietyCount == 0 || world.PhaseCReadinessReport.OrganicPolityCount == 0))
        {
            return GenerationFailurePrimaryKind.PhaseCSocialEmergenceBottleneck;
        }

        if (phaseBScore > 0
            && phaseBScore > phaseCScore
            && world.PhaseBReadinessReport.SentienceCapableLineageCount == 0)
        {
            return GenerationFailurePrimaryKind.PhaseBBiologicalReadinessStall;
        }

        if (phaseBScore > 0 && phaseBScore > phaseCScore && phaseBScore > candidateScore)
        {
            return GenerationFailurePrimaryKind.PhaseBBiologicalReadinessStall;
        }

        if (phaseCScore > 0 && phaseCScore > candidateScore)
        {
            return GenerationFailurePrimaryKind.PhaseCSocialEmergenceBottleneck;
        }

        if (candidateScore > 0)
        {
            return GenerationFailurePrimaryKind.FinalCandidateViabilityCollapse;
        }

        return GenerationFailurePrimaryKind.MixedOrInconclusive;
    }

    private static GenerationZeroViableCause DetermineZeroViableCause(
        World world,
        WorldReadinessReport report,
        IReadOnlyDictionary<int, string> candidateRejectionReasons)
    {
        if (report.CandidatePoolSummary.TotalViableCandidatesDiscovered > 0)
        {
            return GenerationZeroViableCause.None;
        }

        GenerationZeroViableCause cause = GenerationZeroViableCause.None;

        if (world.PhaseCReadinessReport.OrganicPersistentSocietyCount == 0
            || world.PhaseCReadinessReport.OrganicSettlementCount == 0
            || world.PhaseCReadinessReport.OrganicPolityCount == 0)
        {
            cause |= GenerationZeroViableCause.NoDurableSocialWorld;
        }

        if (candidateRejectionReasons.Count > 0
            && (world.Polities.Any(polity => polity.Population > 0) || world.PhaseCReadinessReport.ViableFocalCandidateCount > 0))
        {
            cause |= GenerationZeroViableCause.CandidateTruthFloorCollapse;
        }

        if (!world.PhaseBReadinessReport.IsReady
            || report.GetCategory(WorldReadinessCategoryKind.BiologicalReadiness).IsBlocker
            || report.GetCategory(WorldReadinessCategoryKind.SocialEmergenceReadiness).IsBlocker)
        {
            cause |= GenerationZeroViableCause.BiologyOrSocialReadinessNeverMaturedEnough;
        }

        return cause == GenerationZeroViableCause.None
            ? GenerationZeroViableCause.CandidateTruthFloorCollapse
            : cause;
    }

    private static string BuildReasonSummary(
        GenerationFailurePrimaryKind primaryFailureKind,
        WorldReadinessReport report,
        GenerationZeroViableCause zeroViableCause)
    {
        if (report.FinalCheckpointResolution is PrehistoryCheckpointOutcomeKind.EnterFocalSelection or PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection)
        {
            return report.FinalCheckpointResolution == PrehistoryCheckpointOutcomeKind.EnterFocalSelection
                ? "Readiness gates passed and truthful starts surfaced."
                : "Maximum age forced a thin but still truthful candidate handoff.";
        }

        return primaryFailureKind switch
        {
            GenerationFailurePrimaryKind.PhaseBBiologicalReadinessStall => "Phase B biological readiness stalled before truthful starts could emerge.",
            GenerationFailurePrimaryKind.PhaseCSocialEmergenceBottleneck => "Phase C social emergence never produced enough durable organic societies or polities.",
            GenerationFailurePrimaryKind.FinalCandidateViabilityCollapse => "Late-world candidates appeared, but final truth floors collapsed the pool to zero.",
            _ when zeroViableCause != GenerationZeroViableCause.None => "Mixed readiness bottlenecks left the world with zero truthful viable starts.",
            _ => "The world remained below truthful entry readiness."
        };
    }

    private static bool HasActiveSocietalSubstrate(World world, EmergingSociety society)
        => !society.IsCollapsed
            || (society.FounderPolityId.HasValue && world.Polities.Any(polity => polity.Id == society.FounderPolityId.Value && polity.Population > 0));
}
