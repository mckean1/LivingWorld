using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Presentation;

internal static class FocalSelectionPresentationBuilder
{
    public static IReadOnlyList<string> BuildWatchLines(World world, WatchUiState uiState, bool includeDiagnostics)
    {
        int candidateCount = world.PlayerEntryCandidates.Count;
        if (candidateCount == 0)
        {
            world.FocalSelectionPresentation.Update(0, 0, "No viable player-entry candidates were found.");
            return
            [
                "Focal Selection",
                string.Empty,
                BuildBannerLine(world),
                BuildBannerDetailLine(world),
                string.Empty,
                "No viable player-entry candidates were found."
            ];
        }

        int selectedIndex = Math.Clamp(uiState.GetSelectedIndex(WatchViewType.FocalSelection), 0, candidateCount - 1);
        uiState.SetSelectedIndex(WatchViewType.FocalSelection, selectedIndex);
        return BuildLines(world, selectedIndex, includeDiagnostics, includeCandidateList: true, "Focal Selection");
    }

    public static IReadOnlyList<string> BuildStartupLines(World world, bool includeDiagnostics)
    {
        int candidateCount = world.PlayerEntryCandidates.Count;
        int selectedIndex = candidateCount == 0 ? 0 : 0;
        return BuildLines(world, selectedIndex, includeDiagnostics, includeCandidateList: candidateCount > 1, "World Generation");
    }

    private static IReadOnlyList<string> BuildLines(
        World world,
        int selectedIndex,
        bool includeDiagnostics,
        bool includeCandidateList,
        string title)
    {
        List<string> lines =
        [
            title,
            string.Empty,
            BuildBannerLine(world),
            BuildBannerDetailLine(world)
        ];

        int candidateCount = world.PlayerEntryCandidates.Count;
        if (candidateCount == 0)
        {
            lines.Add(string.Empty);
            lines.Add("No viable player-entry candidates were found.");
            return lines;
        }

        selectedIndex = Math.Clamp(selectedIndex, 0, candidateCount - 1);
        PlayerEntryCandidateSummary selected = world.PlayerEntryCandidates[selectedIndex];
        world.FocalSelectionPresentation.Update(candidateCount, selectedIndex, BuildPresentationSummary(selected));

        lines.Add($"World age {world.Time.Year} | {world.StartupAgeConfiguration.Preset} | {BuildPoolSummary(world)}");
        lines.Add(string.Empty);

        if (includeCandidateList)
        {
            lines.Add("Candidates");
            for (int index = 0; index < candidateCount; index++)
            {
                PlayerEntryCandidateSummary candidate = world.PlayerEntryCandidates[index];
                string marker = index == selectedIndex ? ">" : " ";
                lines.Add($"{marker} {candidate.PolityName,-24} {candidate.MaturityBand.ToDisplayLabel(),-15} {candidate.HomeRegionName}");
            }

            lines.Add(string.Empty);
        }

        AppendCandidateContract(lines, selected, selectedIndex, candidateCount, includeDiagnostics);
        return lines;
    }

    private static void AppendCandidateContract(
        List<string> lines,
        PlayerEntryCandidateSummary candidate,
        int selectedIndex,
        int candidateCount,
        bool includeDiagnostics)
    {
        lines.Add($"Start {selectedIndex + 1} of {candidateCount}");
        lines.Add($"{candidate.PolityName} | {candidate.SpeciesName} | {candidate.HomeRegionName}");
        lines.Add($"{candidate.MaturityBand.ToDisplayLabel()} | {Normalize(candidate.StabilityMode)} | {Normalize(candidate.ArchetypeSummary)}");
        lines.Add($"{Normalize(candidate.PopulationBand)} population | {FormatSettlementCount(candidate.SettlementCount)} | {Normalize(candidate.SubsistenceStyle)} | {Normalize(candidate.CurrentCondition)}");
        lines.Add(string.Empty);
        lines.Add("Qualification");
        lines.Add($"Why it qualified: {Normalize(candidate.QualificationReason)}");
        lines.Add($"Evidence: {Normalize(candidate.EvidenceSentence)}");
        lines.Add(string.Empty);
        lines.Add("Pressure and Opportunity");
        lines.Add($"Defining pressure or opportunity: {Normalize(candidate.DefiningPressureOrOpportunity)}");
        if (!string.IsNullOrWhiteSpace(candidate.RecentHistoricalNote))
        {
            lines.Add($"Recent note: {candidate.RecentHistoricalNote}");
        }

        AppendTagLine(lines, "Strengths", candidate.SafeStrengths);
        AppendTagLine(lines, "Warnings", candidate.SafeWarnings);
        AppendTagLine(lines, "Risks", candidate.SafeRisks);

        lines.Add(string.Empty);
        lines.Add("Identity and Form");
        lines.Add($"Population: {Normalize(candidate.PopulationBand)}");
        lines.Add($"Form: {FormatSettlementCount(candidate.SettlementCount)} | {Normalize(candidate.SettlementProfile)}");
        lines.Add($"Maturity / Stability: {candidate.MaturityBand.ToDisplayLabel()} | {Normalize(candidate.StabilityMode)}");
        lines.Add($"Archetype: {Normalize(candidate.ArchetypeSummary)}");
        lines.Add($"Score tier: {DescribeScoreTier(candidate.ScoreBreakdown?.Tier)}");

        lines.Add(string.Empty);
        lines.Add("Homeland and Movement");
        lines.Add($"Home region: {candidate.HomeRegionName}");
        lines.Add($"Regional pattern: {Normalize(candidate.RegionalProfile)}");
        lines.Add($"Subsistence: {Normalize(candidate.SubsistenceStyle)}");
        lines.Add($"Current condition: {Normalize(candidate.CurrentCondition)}");

        lines.Add(string.Empty);
        lines.Add("Neighbors and Pressure");
        lines.Add($"Pressure / opportunity: {Normalize(candidate.DefiningPressureOrOpportunity)}");
        lines.Add($"Historical context: {Normalize(candidate.RecentHistoricalNote)}");
        lines.Add($"Lineage context: {Normalize(candidate.LineageProfile)}");

        lines.Add(string.Empty);
        lines.Add("Opportunity and Risk");
        lines.Add($"Discoveries: {Normalize(candidate.DiscoverySummary)}");
        lines.Add($"Learned: {Normalize(candidate.LearnedSummary)}");
        lines.Add($"Strengths in play: {JoinOrFallback(candidate.SafeStrengths, "No clear structural edge surfaced.")}");
        lines.Add($"Warnings in play: {JoinOrFallback(candidate.SafeWarnings, "No special warning surfaced.")}");
        lines.Add($"Risks in play: {JoinOrFallback(candidate.SafeRisks, "No major risk surfaced.")}");

        lines.Add(string.Empty);
        lines.Add("Why This Start Qualified");
        lines.Add($"Qualification reason: {Normalize(candidate.QualificationReason)}");
        lines.Add($"Supporting evidence: {Normalize(candidate.EvidenceSentence)}");
        lines.Add($"Viability: {DescribeViability(candidate)}");
        lines.Add($"Outlook: {DescribeOutlook(candidate)}");

        if (includeDiagnostics && candidate.ScoreBreakdown is not null)
        {
            lines.Add(string.Empty);
            lines.Add("Diagnostics");
            lines.Add($"Score total: {candidate.ScoreBreakdown.Total:F2}");
            lines.Add($"Score explanation: {candidate.ScoreBreakdown.Explanation}");
            lines.Add($"Candidate origin: {Normalize(candidate.CandidateOriginReason)}");
        }
    }

    private static void AppendTagLine(List<string> lines, string label, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        lines.Add($"{label}: {string.Join(", ", values)}");
    }

    private static string BuildBannerLine(World world)
    {
        FocalSelectionBannerState bannerState = ResolveBannerState(world);
        return bannerState switch
        {
            FocalSelectionBannerState.Ready => "READY STARTS",
            FocalSelectionBannerState.Thin => "THIN STARTS",
            FocalSelectionBannerState.Forced => "FORCED ENTRY",
            FocalSelectionBannerState.WeakWorld => "WEAK WORLD",
            _ => "FOCAL SELECTION"
        };
    }

    private static string BuildBannerDetailLine(World world)
    {
        WorldReadinessReport report = world.WorldReadinessReport;
        return ResolveBannerState(world) switch
        {
            FocalSelectionBannerState.Ready => "The world produced real viable starts and reached a normal focal-selection stop.",
            FocalSelectionBannerState.Thin => $"The world is viable but thin. {BuildPoolSummary(world)}.",
            FocalSelectionBannerState.Forced => $"Maximum-age or forced stop. {BuildPoolSummary(world)}.",
            FocalSelectionBannerState.WeakWorld => $"The world is weak and that weakness is visible here. {BuildPoolSummary(world)}.",
            _ => report.SummaryData.Headline
        };
    }

    private static FocalSelectionBannerState ResolveBannerState(World world)
    {
        if (world.WorldReadinessReport.IsWeakWorld)
        {
            return FocalSelectionBannerState.WeakWorld;
        }

        if (world.PrehistoryRuntime.LastCheckpointOutcome?.Kind == PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection)
        {
            return FocalSelectionBannerState.Forced;
        }

        if (world.WorldReadinessReport.IsThinWorld || world.WorldReadinessReport.CandidatePoolSummary.IsThinWorld)
        {
            return FocalSelectionBannerState.Thin;
        }

        return FocalSelectionBannerState.Ready;
    }

    private static string BuildPoolSummary(World world)
    {
        int surfaced = world.PlayerEntryCandidates.Count;
        int viable = world.WorldReadinessReport.CandidatePoolSummary.TotalViableCandidatesDiscovered;
        return viable > surfaced
            ? $"{surfaced} surfaced of {viable} viable starts"
            : $"{surfaced} {(surfaced == 1 ? "viable start" : "viable starts")}";
    }

    private static string FormatSettlementCount(int settlementCount)
        => settlementCount == 1 ? "1 settlement" : $"{settlementCount} settlements";

    private static string DescribeScoreTier(CandidateScoreTier? tier)
        => tier switch
        {
            CandidateScoreTier.Exceptional => "Exceptional",
            CandidateScoreTier.Strong => "Strong",
            CandidateScoreTier.Promising => "Promising",
            CandidateScoreTier.Modest => "Modest",
            _ => "Unscored"
        };

    private static string DescribeViability(PlayerEntryCandidateSummary candidate)
    {
        CandidateViabilityResult? viability = candidate.Viability;
        if (viability is null)
        {
            return "No viability detail surfaced.";
        }

        if (!viability.IsViable)
        {
            return $"Not viable: {Normalize(viability.PrimaryFailureReason)}";
        }

        if (!viability.SupportsNormalEntry)
        {
            return "Viable but thin for a normal stop.";
        }

        return "Viable for a normal stop.";
    }

    private static string DescribeOutlook(PlayerEntryCandidateSummary candidate)
    {
        string tier = DescribeScoreTier(candidate.ScoreBreakdown?.Tier);
        return candidate.ScoreBreakdown is null
            ? tier
            : $"{tier} outlook. {candidate.ScoreBreakdown.Explanation}";
    }

    private static string JoinOrFallback(IReadOnlyList<string> values, string fallback)
        => values.Count == 0 ? fallback : string.Join(", ", values);

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Unknown" : value;

    private static string BuildPresentationSummary(PlayerEntryCandidateSummary candidate)
        => $"{candidate.PolityName} | {candidate.MaturityBand.ToDisplayLabel()} | {DescribeScoreTier(candidate.ScoreBreakdown?.Tier)}";

    private enum FocalSelectionBannerState
    {
        Ready,
        Thin,
        Forced,
        WeakWorld
    }
}
