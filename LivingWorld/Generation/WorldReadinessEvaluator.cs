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
        Dictionary<string, bool> passes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["biology"] = biologicalScore >= (0.72 * world.StartupAgeConfiguration.ReadinessStrictness),
            ["social"] = socialScore >= (0.58 * world.StartupAgeConfiguration.ReadinessStrictness),
            ["civilization"] = civilizationalScore >= (0.58 * world.StartupAgeConfiguration.ReadinessStrictness),
            ["candidates"] = candidateScore >= 0.52,
            ["stability"] = stabilityScore >= 0.48
        };

        List<string> failures = [];
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
            passes);
    }

    private static double ScoreBiology(World world)
    {
        PhaseAReadinessReport phaseA = world.PhaseAReadinessReport;
        PhaseBReadinessReport phaseB = world.PhaseBReadinessReport;
        return Math.Clamp(
            (phaseA.ProducerCoverage * 0.20)
            + (phaseA.ConsumerCoverage * 0.18)
            + (phaseA.PredatorCoverage * 0.08)
            + (Math.Min(1.0, phaseB.MatureLineageCount / 5.0) * 0.22)
            + (Math.Min(1.0, phaseB.SpeciationCount / 4.0) * 0.18)
            + (Math.Min(1.0, phaseB.SentienceCapableLineageCount / 2.0) * 0.14),
            0.0,
            1.0);
    }

    private static double ScoreSocial(World world, WorldGenerationSettings settings)
        => Math.Clamp(
            (Math.Min(1.0, world.PhaseCReadinessReport.SentientGroupCount / (double)Math.Max(1, settings.MinimumPhaseCSentientGroupCount)) * 0.28)
            + (Math.Min(1.0, world.PhaseCReadinessReport.PersistentSocietyCount / (double)Math.Max(1, settings.MinimumPhaseCPersistentSocietyCount)) * 0.34)
            + (Math.Min(1.0, world.PhaseCReadinessReport.SettlementCount / (double)Math.Max(1, settings.MinimumPhaseCSettlementCount)) * 0.20)
            + (Math.Min(1.0, world.PhaseCReadinessReport.HistoricalEventDensity / Math.Max(0.01, settings.MinimumPhaseCHistoricalEventDensity)) * 0.18),
            0.0,
            1.0);

    private static double ScoreCivilization(World world, WorldGenerationSettings settings)
        => Math.Clamp(
            (Math.Min(1.0, world.PhaseCReadinessReport.PolityCount / (double)Math.Max(1, settings.MinimumPhaseCPolityCount)) * 0.34)
            + (Math.Min(1.0, world.PhaseCReadinessReport.ViableSettlementCount / (double)Math.Max(1, settings.MinimumPhaseCViableSettlementCount)) * 0.18)
            + (Math.Min(1.0, world.PhaseCReadinessReport.AveragePolityAge / Math.Max(1.0, settings.MinimumPhaseCAveragePolityAge)) * 0.24)
            + (Math.Min(1.0, world.CivilizationalHistory.Count / 18.0) * 0.24),
            0.0,
            1.0);

    private static double ScoreCandidates(World world, WorldGenerationSettings settings)
        => Math.Clamp(
            (Math.Min(1.0, world.PlayerEntryCandidates.Count / (double)Math.Max(1, settings.MinimumViablePlayerEntryCandidates)) * 0.50)
            + (world.PlayerEntryCandidates.DefaultIfEmpty().Average(candidate => candidate?.RankScore ?? 0.0) * 0.50),
            0.0,
            1.0);

    private static double ScoreStability(World world)
    {
        if (world.PlayerEntryCandidates.Count == 0)
        {
            return world.PhaseCReadinessReport.ViableSettlementCount > 0 ? 0.28 : 0.0;
        }

        return Math.Clamp(world.PlayerEntryCandidates.Average(candidate => candidate.StabilityBand switch
        {
            Societies.StabilityBand.Strong => 1.0,
            Societies.StabilityBand.Stable => 0.80,
            Societies.StabilityBand.Strained => 0.55,
            _ => 0.24
        }), 0.0, 1.0);
    }
}
