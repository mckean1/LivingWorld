using LivingWorld.Core;

namespace LivingWorld.Generation;

public static class WorldReadinessEvaluator
{
    public static WorldReadinessReport Evaluate(World world, WorldGenerationSettings settings)
    {
        double biologicalScore = ScoreBiology(world);
        double socialScore = ScoreSocial(world, settings);
        double civilizationalScore = ScoreCivilization(world, settings);
        double candidateScore = ScoreCandidates(world, settings);
        double stabilityScore = ScoreStability(world);
        bool biologyFloorPass = biologicalScore >= settings.MinimumBiologicalReadinessFloor;
        double biologyReadinessThreshold = Math.Max(
            settings.MinimumBiologicalReadinessFloor,
            0.55 * world.StartupAgeConfiguration.ReadinessStrictness);
        int organicCandidateCount = world.PlayerEntryCandidates.Count(candidate => !candidate.IsFallbackCandidate);
        int fallbackCandidateCount = world.PlayerEntryCandidates.Count - organicCandidateCount;
        int organicHealthyCandidateCount = world.PlayerEntryCandidates.Count(candidate =>
            !candidate.IsFallbackCandidate
            && candidate.RankScore >= settings.MinimumHealthyCandidateScore);
        Dictionary<string, bool> passes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["biology"] = biologyFloorPass && biologicalScore >= biologyReadinessThreshold,
            ["social"] = socialScore >= (0.60 * world.StartupAgeConfiguration.ReadinessStrictness),
            ["civilization"] = civilizationalScore >= (0.62 * world.StartupAgeConfiguration.ReadinessStrictness),
            ["candidates"] = candidateScore >= 0.52
                && organicCandidateCount >= settings.MinimumViablePlayerEntryCandidates
                && organicHealthyCandidateCount >= settings.MinimumHealthyCandidateCount,
            ["stability"] = stabilityScore >= 0.50
        };

        List<string> failures = [];
        if (!biologyFloorPass)
        {
            failures.Add("biology_floor_below_minimum");
        }

        if (organicCandidateCount < settings.MinimumViablePlayerEntryCandidates)
        {
            failures.Add($"organic_candidate_count_below_target:{organicCandidateCount}/{settings.MinimumViablePlayerEntryCandidates}");
        }

        if (organicHealthyCandidateCount < settings.MinimumHealthyCandidateCount)
        {
            failures.Add($"healthy_candidate_count_below_target:{organicHealthyCandidateCount}/{settings.MinimumHealthyCandidateCount}");
        }

        if (world.PhaseCReadinessReport.OrganicPolityCount < settings.MinimumPhaseCPolityCount)
        {
            failures.Add($"organic_polity_count_below_target:{world.PhaseCReadinessReport.OrganicPolityCount}/{settings.MinimumPhaseCPolityCount}");
        }

        if (fallbackCandidateCount > 0 && organicHealthyCandidateCount < settings.MinimumHealthyCandidateCount)
        {
            failures.Add("fallback_candidates_without_organic_depth");
        }

        foreach ((string category, bool passed) in passes)
        {
            if (!passed)
            {
                failures.Add($"{category}_not_ready");
            }
        }

        return new WorldReadinessReport(
            failures.Count == 0,
            world.Time.Year,
            biologicalScore,
            socialScore,
            civilizationalScore,
            candidateScore,
            stabilityScore,
            world.PlayerEntryCandidates.Count,
            failures,
            passes,
            organicCandidateCount,
            fallbackCandidateCount,
            organicHealthyCandidateCount);
    }

    private static double ScoreBiology(World world)
    {
        PhaseAReadinessReport phaseA = world.PhaseAReadinessReport;
        PhaseBReadinessReport phaseB = world.PhaseBReadinessReport;
        PhaseBDiagnostics diagnostics = world.PhaseBDiagnostics;
        int currentSentienceCapableLineageCount = world.EvolutionaryLineages.Count(lineage =>
            !lineage.IsExtinct
            && lineage.SentienceCapability == Life.SentienceCapabilityState.Capable);
        int effectiveSentienceCapableLineageCount = Math.Max(phaseB.SentienceCapableLineageCount, currentSentienceCapableLineageCount);
        return Math.Clamp(
            (phaseA.ProducerCoverage * 0.20)
            + (phaseA.ConsumerCoverage * 0.14)
            + (phaseA.PredatorCoverage * 0.06)
            + (Math.Min(1.0, phaseB.MatureLineageCount / 5.0) * 0.16)
            + (Math.Min(1.0, phaseB.SpeciationCount / 4.0) * 0.14)
            + (Math.Min(1.0, diagnostics.AverageAncestryDepth / 2.0) * 0.08)
            + (Math.Min(1.0, diagnostics.MatureDivergenceLineageCount / 4.0) * 0.08)
            + (Math.Min(1.0, diagnostics.AdaptedBiomeSpan / 4.0) * 0.06)
            + (Math.Min(1.0, (diagnostics.LocalExtinctionEventCount + diagnostics.RecolonizationEventCount) / 4.0) * 0.08)
            + (Math.Min(1.0, diagnostics.SentienceCapableRootBranchCount / 2.0) * 0.08)
            + (Math.Min(1.0, effectiveSentienceCapableLineageCount / (double)Math.Max(1, 2)) * 0.12),
            0.0,
            1.0);
    }

    private static double ScoreSocial(World world, WorldGenerationSettings settings)
    {
        int organicSocialTrajectoryCount = world.PhaseCReadinessReport.OrganicSentientGroupCount
            + world.PhaseCReadinessReport.OrganicPersistentSocietyCount;

        return Math.Clamp(
            (Math.Min(1.0, organicSocialTrajectoryCount / (double)Math.Max(1, settings.MinimumPhaseCSentientGroupCount)) * 0.28)
            + (Math.Min(1.0, world.PhaseCReadinessReport.OrganicPersistentSocietyCount / (double)Math.Max(1, settings.MinimumPhaseCPersistentSocietyCount)) * 0.34)
            + (Math.Min(1.0, world.PhaseCReadinessReport.OrganicSettlementCount / (double)Math.Max(1, settings.MinimumPhaseCSettlementCount)) * 0.20)
            + (Math.Min(1.0, world.PhaseCReadinessReport.HistoricalEventDensity / Math.Max(0.01, settings.MinimumPhaseCHistoricalEventDensity)) * 0.18),
            0.0,
            1.0);
    }

    private static double ScoreCivilization(World world, WorldGenerationSettings settings)
    {
        int organicViableSettlementCount = world.SocialSettlements.Count(settlement =>
            !settlement.IsAbandoned
            && !settlement.IsFallbackCreated
            && settlement.SettlementViability >= 0.55);

        return Math.Clamp(
            (Math.Min(1.0, world.PhaseCReadinessReport.OrganicPolityCount / (double)Math.Max(1, settings.MinimumPhaseCPolityCount)) * 0.34)
            + (Math.Min(1.0, organicViableSettlementCount / (double)Math.Max(1, settings.MinimumPhaseCViableSettlementCount)) * 0.18)
            + (Math.Min(1.0, world.PhaseCReadinessReport.AveragePolityAge / Math.Max(1.0, settings.MinimumPhaseCAveragePolityAge)) * 0.24)
            + (Math.Min(1.0, world.CivilizationalHistory.Count / 18.0) * 0.24),
            0.0,
            1.0);
    }

    private static double ScoreCandidates(World world, WorldGenerationSettings settings)
    {
        List<Societies.PlayerEntryCandidateSummary> organicCandidates = world.PlayerEntryCandidates
            .Where(candidate => !candidate.IsFallbackCandidate)
            .ToList();
        if (organicCandidates.Count == 0)
        {
            return 0.0;
        }

        return Math.Clamp(
            (Math.Min(1.0, organicCandidates.Count / (double)Math.Max(1, settings.MinimumViablePlayerEntryCandidates)) * 0.55)
            + (organicCandidates.Average(candidate => candidate.RankScore) * 0.45),
            0.0,
            1.0);
    }

    private static double ScoreStability(World world)
    {
        List<Societies.PlayerEntryCandidateSummary> organicCandidates = world.PlayerEntryCandidates
            .Where(candidate => !candidate.IsFallbackCandidate)
            .ToList();
        if (organicCandidates.Count == 0)
        {
            return world.PhaseCReadinessReport.OrganicSettlementCount > 0 ? 0.22 : 0.0;
        }

        return Math.Clamp(organicCandidates.Average(candidate => candidate.StabilityBand switch
        {
            Societies.StabilityBand.Strong => 1.0,
            Societies.StabilityBand.Stable => 0.80,
            Societies.StabilityBand.Strained => 0.55,
            _ => 0.24
        }), 0.0, 1.0);
    }
}
