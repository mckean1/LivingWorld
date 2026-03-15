using LivingWorld.Core;

namespace LivingWorld.Generation;

public static class WorldReadinessEvaluator
{
    public static WorldReadinessReport Evaluate(World world, WorldGenerationSettings settings)
    {
        WorldAgeGateReport ageGate = new(
            world.Time.Year,
            world.StartupAgeConfiguration.MinPrehistoryYears,
            world.StartupAgeConfiguration.TargetPrehistoryYears,
            world.StartupAgeConfiguration.MaxPrehistoryYears,
            world.Time.Year >= world.StartupAgeConfiguration.MaxPrehistoryYears
                ? PrehistoryAgeGateStatus.MaximumAgeReached
                : world.Time.Year >= world.StartupAgeConfiguration.TargetPrehistoryYears
                    ? PrehistoryAgeGateStatus.TargetAgeReached
                    : world.Time.Year >= world.StartupAgeConfiguration.MinPrehistoryYears
                        ? PrehistoryAgeGateStatus.MinimumAgeReached
                        : PrehistoryAgeGateStatus.BeforeMinimumAge);

        IReadOnlyList<WorldReadinessCategoryReport> categories =
        [
            new(WorldReadinessCategoryKind.BiologicalReadiness, world.PhaseAReadinessReport.IsReady && world.PhaseBReadinessReport.IsReady ? ReadinessAssessmentStatus.Pass : ReadinessAssessmentStatus.Warning, ReadinessCategoryStrictness.Medium, "Legacy biological readiness.", world.PhaseBReadinessReport.SentienceCapableLineageCount == 0 ? ["legacy_no_sentience_capable_lineages"] : Array.Empty<string>(), Array.Empty<string>()),
            new(WorldReadinessCategoryKind.SocialEmergenceReadiness, world.PhaseCReadinessReport.IsReady ? ReadinessAssessmentStatus.Pass : ReadinessAssessmentStatus.Warning, ReadinessCategoryStrictness.Strict, "Legacy social readiness.", Array.Empty<string>(), Array.Empty<string>()),
            new(WorldReadinessCategoryKind.WorldStructureReadiness, ReadinessAssessmentStatus.Warning, ReadinessCategoryStrictness.Strict, "Legacy world-structure readiness.", Array.Empty<string>(), ["legacy_world_structure_not_evaluated"]),
            new(WorldReadinessCategoryKind.CandidateReadiness, world.PlayerEntryCandidates.Count > 0 ? ReadinessAssessmentStatus.Pass : ReadinessAssessmentStatus.Blocker, ReadinessCategoryStrictness.Strict, "Legacy candidate readiness.", world.PlayerEntryCandidates.Count > 0 ? Array.Empty<string>() : ["legacy_no_candidates"], Array.Empty<string>()),
            new(WorldReadinessCategoryKind.VarietyReadiness, ReadinessAssessmentStatus.Warning, ReadinessCategoryStrictness.Soft, "Legacy variety readiness.", Array.Empty<string>(), ["legacy_variety_not_evaluated"]),
            new(WorldReadinessCategoryKind.AgencyReadiness, ReadinessAssessmentStatus.Warning, ReadinessCategoryStrictness.Soft, "Legacy agency readiness.", Array.Empty<string>(), ["legacy_agency_not_evaluated"])
        ];

        int organic = world.PlayerEntryCandidates.Count(candidate => !candidate.IsFallbackCandidate);
        int fallback = world.PlayerEntryCandidates.Count - organic;
        CandidatePoolReadinessSummary pool = new(
            world.PlayerEntryCandidates.Count,
            world.PlayerEntryCandidates.Count,
            world.PlayerEntryCandidates.Count,
            organic,
            fallback,
            world.PlayerEntryCandidates.Select(candidate => candidate.SpeciesId).Distinct().Count(),
            world.PlayerEntryCandidates.Select(candidate => candidate.LineageId).Distinct().Count(),
            world.PlayerEntryCandidates.Select(candidate => candidate.HomeRegionId).Distinct().Count(),
            world.PlayerEntryCandidates.Select(candidate => candidate.SubsistenceStyle).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            world.PlayerEntryCandidates.Count < settings.MinimumViablePlayerEntryCandidates,
            world.PlayerEntryCandidates.Count == 0 ? "Legacy readiness found no candidates." : $"Legacy readiness found {world.PlayerEntryCandidates.Count} candidates.");

        IReadOnlyList<string> blockers = categories.SelectMany(category => category.Blockers).ToArray();
        IReadOnlyList<string> warnings = categories.SelectMany(category => category.Warnings).ToArray();
        PrehistoryCheckpointOutcomeKind resolution = blockers.Count == 0 && ageGate.MinimumAgeReached
            ? PrehistoryCheckpointOutcomeKind.EnterFocalSelection
            : ageGate.MaximumAgeReached && pool.ViableCandidateCount == 0
                ? PrehistoryCheckpointOutcomeKind.GenerationFailure
                : PrehistoryCheckpointOutcomeKind.ContinuePrehistory;

        return new WorldReadinessReport(
            ageGate,
            resolution,
            categories,
            pool,
            blockers,
            warnings,
            warnings.Count > 0,
            pool.IsThinWorld,
            new WorldReadinessSummaryData(
                "Legacy readiness evaluation.",
                pool.ViableCandidateCount == 1 ? "1 viable start" : $"{pool.ViableCandidateCount} viable starts",
                "Legacy compatibility report.",
                categories.Count(category => category.Status == ReadinessAssessmentStatus.Pass),
                categories.Count(category => category.Status == ReadinessAssessmentStatus.Warning),
                categories.Count(category => category.Status == ReadinessAssessmentStatus.Blocker)));
    }
}
