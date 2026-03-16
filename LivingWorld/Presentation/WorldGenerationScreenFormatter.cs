using LivingWorld.Core;
using LivingWorld.Life;

namespace LivingWorld.Presentation;

internal static class WorldGenerationScreenFormatter
{
    public static List<string> BuildLines(World world, bool includeDiagnostics)
    {
        string border = new('=', 78);
        List<string> lines =
        [
            border,
            " WORLD GENERATION",
            $" Era / Stage: {ResolveStageLabel(world)}",
            $" World Age: {world.PrehistoryRuntime.WorldAgeYears:N0} years",
            string.Empty,
            $" {ResolveNarrative(world)}",
            string.Empty,
            " Current Outlook"
        ];

        foreach (string bullet in BuildOutlook(world).Distinct(StringComparer.Ordinal).Take(4))
        {
            lines.Add($"  - {bullet}");
        }

        lines.Add(string.Empty);
        lines.Add($" Status: {ResolveStatusLine(world)}");

        if (includeDiagnostics && world.StartupDiagnostics.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add(" Developer Notes");
            foreach (string diagnostic in world.StartupDiagnostics.Take(4))
            {
                lines.Add($"  - {diagnostic}");
            }
        }

        lines.Add(border);
        return lines;
    }

    private static string ResolveStageLabel(World world)
    {
        PrehistoryRuntimeStatus runtime = world.PrehistoryRuntime;
        return runtime.CurrentPhase switch
        {
            PrehistoryRuntimePhase.WorldSeeding when runtime.DetailView == PrehistoryRuntimeDetailView.WorldFrame => "Shaping the Young World",
            PrehistoryRuntimePhase.WorldSeeding => "Living Foundations",
            PrehistoryRuntimePhase.BiologicalDivergence => "Age of Divergence",
            PrehistoryRuntimePhase.SocialEmergence when runtime.DetailView == PrehistoryRuntimeDetailView.CandidateEvaluation => "Approaching First Starts",
            PrehistoryRuntimePhase.SocialEmergence => "Age of Peoples",
            PrehistoryRuntimePhase.WorldReadinessReview => "Readiness Review",
            PrehistoryRuntimePhase.GenerationFailure => "Generation Failure",
            PrehistoryRuntimePhase.SimulationEngineActivePlay => "SimulationEngine Active Play",
            _ => runtime.CurrentPhase.ToDisplayString()
        };
    }

    private static string ResolveNarrative(World world)
    {
        PrehistoryRuntimeStatus runtime = world.PrehistoryRuntime;
        return runtime.CurrentPhase switch
        {
            PrehistoryRuntimePhase.WorldSeeding when runtime.DetailView == PrehistoryRuntimeDetailView.WorldFrame
                => "Land, climate, and the first habitats are taking shape.",
            PrehistoryRuntimePhase.WorldSeeding
                => world.PhaseAReadinessReport.IsReady
                    ? "The world has a living foundation, and its earliest ecosystems are settling into place."
                    : "The world is still young, and its earliest ecosystems are struggling toward stability.",
            PrehistoryRuntimePhase.BiologicalDivergence
                => world.PhaseBReadinessReport.IsReady
                    ? "Life has spread and deepened into a richer biological history."
                    : "Life is spreading into new niches as lineages branch, adapt, and endure.",
            PrehistoryRuntimePhase.SocialEmergence when runtime.DetailView == PrehistoryRuntimeDetailView.CandidateEvaluation
                => world.PlayerEntryCandidates.Count > 0 || world.WorldReadinessReport.ViableCandidateCount > 0
                    ? "The world is approaching viable starting conditions, and truthful starts are being reviewed."
                    : "The world is being tested for a truthful player start, but none are secure yet.",
            PrehistoryRuntimePhase.SocialEmergence
                => world.PhaseCReadinessReport.PolityCount > 0
                    ? "Distinct peoples are beginning to take shape, and some have already formed early polities."
                    : world.PhaseCReadinessReport.SettlementCount > 0
                        ? "Distinct peoples are beginning to take shape, and some communities are learning to stay rooted."
                        : "Distinct peoples are beginning to take shape, but lasting societies are still fragile.",
            PrehistoryRuntimePhase.WorldReadinessReview
                => world.WorldReadinessReport.IsReady
                    ? "The world appears ready to stop honestly and offer truthful starts."
                    : "The world is being reviewed for a truthful player start.",
            PrehistoryRuntimePhase.GenerationFailure
                => "This world could not produce a truthful player start.",
            PrehistoryRuntimePhase.SimulationEngineActivePlay
                => "A truthful start has been chosen and handed to the SimulationEngine.",
            _ => runtime.ActivitySummary
        };
    }

    private static IReadOnlyList<string> BuildOutlook(World world)
    {
        PrehistoryRuntimeStatus runtime = world.PrehistoryRuntime;
        return runtime.CurrentPhase switch
        {
            PrehistoryRuntimePhase.WorldSeeding when runtime.DetailView == PrehistoryRuntimeDetailView.WorldFrame =>
            [
                "Land and climate are still settling.",
                "Habitats for the first lineages are being prepared.",
                "The world is far from ready for peoples or starts."
            ],
            PrehistoryRuntimePhase.WorldSeeding => BuildEcologyOutlook(world),
            PrehistoryRuntimePhase.BiologicalDivergence => BuildBiologyOutlook(world),
            PrehistoryRuntimePhase.SocialEmergence when runtime.DetailView == PrehistoryRuntimeDetailView.CandidateEvaluation => BuildCandidateOutlook(world),
            PrehistoryRuntimePhase.SocialEmergence => BuildSocietalOutlook(world),
            PrehistoryRuntimePhase.WorldReadinessReview => BuildCandidateOutlook(world),
            PrehistoryRuntimePhase.GenerationFailure =>
            [
                "No viable truthful start survived the final review.",
                "The simulation stopped honestly rather than inventing a start.",
                "Detailed world-generation diagnostics were written to the worldgen log."
            ],
            PrehistoryRuntimePhase.SimulationEngineActivePlay =>
            [
                "A truthful start has been selected.",
                "The inherited world state is preserved exactly at handoff.",
                "The SimulationEngine is paused until play resumes."
            ],
            _ => ["The world continues to mature."]
        };
    }

    private static IReadOnlyList<string> BuildEcologyOutlook(World world)
    {
        PhaseAReadinessReport report = world.PhaseAReadinessReport;
        List<string> lines =
        [
            report.OccupiedRegionPercentage >= 0.65
                ? "Life now reaches much of the world."
                : "Life is still spreading into open habitats.",
            report.StableRegionCount >= report.CollapsingRegionCount
                ? "Food webs are stabilizing across more regions."
                : "Large parts of the biosphere remain fragile.",
            report.ConsumerCoverage >= 0.45
                ? "Complex food webs are beginning to hold together."
                : "Many regions still lack fuller food webs."
        ];

        lines.Add(report.IsReady
            ? "The living foundation looks strong enough for deeper history."
            : "The world still needs more ecological stability before deeper history can begin.");

        return lines;
    }

    private static IReadOnlyList<string> BuildBiologyOutlook(World world)
    {
        PhaseBReadinessReport report = world.PhaseBReadinessReport;
        List<string> lines =
        [
            report.MatureLineageCount > 0
                ? "New branches of life continue to appear."
                : "Life is still shallow and close to its earliest forms.",
            CountExtinctionEvents(world) > 0
                ? "Extinction and recovery are reshaping the biosphere."
                : "Older lineages are still holding most of the world.",
            report.SentienceCapableLineageCount > 0
                ? "Some lineages are approaching the complexity needed for sentient life."
                : "No sentient branch is secure yet."
        ];

        lines.Add(report.IsReady
            ? "The world is nearing biological maturity."
            : "The world still needs deeper biological history.");

        return lines;
    }

    private static IReadOnlyList<string> BuildSocietalOutlook(World world)
    {
        PhaseCReadinessReport report = world.PhaseCReadinessReport;
        List<string> lines =
        [
            report.SentientGroupCount > 0
                ? "Distinct peoples now exist in the world."
                : "No lasting peoples have emerged yet.",
            report.SettlementCount > 0
                ? "Some communities are beginning to stay rooted."
                : "Most groups are still mobile and fragile.",
            report.PolityCount > 0
                ? "Early polities are starting to hold together."
                : "No durable polity has held together yet."
        ];

        lines.Add(report.ViableFocalCandidateCount > 0 || world.PlayerEntryCandidates.Count > 0
            ? "Viable starts are beginning to appear."
            : "The world still needs more social depth before a start can be chosen.");

        return lines;
    }

    private static IReadOnlyList<string> BuildCandidateOutlook(World world)
    {
        WorldReadinessReport report = world.WorldReadinessReport;
        List<string> lines =
        [
            report.ViableCandidateCount > 0
                ? "Potential starts exist, but they still need to pass the final review."
                : "No viable start has survived review yet.",
            report.IsThinWorld
                ? "The world remains narrow, with too little variety for a healthy opening."
                : "The world has enough breadth to keep the search honest.",
            report.IsWeakWorld
                ? "The world is still maturing and may need more time."
                : "The world is holding together well enough for a serious review."
        ];

        lines.Add(report.IsReady
            ? "The world is ready to stop honestly if you choose a start."
            : "The world is still approaching viable starting conditions.");

        return lines;
    }

    private static string ResolveStatusLine(World world)
    {
        PrehistoryRuntimeStatus runtime = world.PrehistoryRuntime;
        return runtime.CurrentPhase switch
        {
            PrehistoryRuntimePhase.WorldSeeding when world.PhaseAReadinessReport.IsReady
                => "Living foundations are taking hold.",
            PrehistoryRuntimePhase.WorldSeeding
                => "The world is still young.",
            PrehistoryRuntimePhase.BiologicalDivergence when world.PhaseBReadinessReport.IsReady
                => "The living world is maturing into deeper history.",
            PrehistoryRuntimePhase.BiologicalDivergence
                => "Life is diversifying and the world is still maturing.",
            PrehistoryRuntimePhase.SocialEmergence when runtime.DetailView == PrehistoryRuntimeDetailView.CandidateEvaluation && world.WorldReadinessReport.IsReady
                => "The world is ready for a truthful player start.",
            PrehistoryRuntimePhase.SocialEmergence when runtime.DetailView == PrehistoryRuntimeDetailView.CandidateEvaluation
                => "Promising starts exist, but the world still needs review.",
            PrehistoryRuntimePhase.SocialEmergence when world.PhaseCReadinessReport.ViableFocalCandidateCount > 0
                => "Viable starts are beginning to emerge.",
            PrehistoryRuntimePhase.SocialEmergence
                => "Early peoples are still finding stable footing.",
            PrehistoryRuntimePhase.WorldReadinessReview when world.WorldReadinessReport.IsReady
                => "The world is ready to stop honestly.",
            PrehistoryRuntimePhase.WorldReadinessReview
                => "The world is being reviewed for a truthful start.",
            PrehistoryRuntimePhase.GenerationFailure
                => "This world could not produce a truthful player start.",
            PrehistoryRuntimePhase.SimulationEngineActivePlay
                => "The SimulationEngine is ready to continue from the selected start.",
            _ => runtime.ActivitySummary
        };
    }

    private static int CountExtinctionEvents(World world)
        => world.EvolutionaryHistory.Count(entry => entry.Type is EvolutionaryHistoryEventType.LocalExtinction or EvolutionaryHistoryEventType.GlobalExtinction);
}
