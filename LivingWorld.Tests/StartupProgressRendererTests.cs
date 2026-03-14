using LivingWorld.Core;
using LivingWorld.Presentation;
using LivingWorld.Societies;
using Xunit;

namespace LivingWorld.Tests;

public sealed class StartupProgressRendererTests
{
    [Fact]
    public void BuildDisplayLines_ShowsPhaseSubphaseAndAgeContext()
    {
        World world = new(new WorldTime(180, 1))
        {
            StartupGenerationAttempt = 1
        };
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.PrehistoryRunning;
        world.StartupStage = WorldStartupStage.EvolutionaryExpansion;
        world.PrehistoryRuntime.WorldAgeYears = 180;
        world.PrehistoryRuntime.AreReadinessChecksActive = true;
        world.PrehistoryRuntime.PhaseLabel = "Running evolutionary history";
        world.PrehistoryRuntime.SubphaseLabel = "Diverging regional lineages";
        world.PrehistoryRuntime.ActivitySummary = "Diverging isolated lineages into new branches and adaptation paths.";
        world.PrehistoryRuntime.TransitionSummary = "Phase A complete: ecosystems stabilized.";
        world.PhaseBDiagnostics = new PhaseBDiagnostics(2.4, 6, 3, 4, 5, 2, 1, 2, 2, 2, []);
        world.PhaseBReadinessReport = new PhaseBReadinessReport(true, 5, 4, 2, 5, 2, 2, 7, []);

        IReadOnlyList<string> lines = StartupProgressRenderer.BuildDisplayLines(world, includeDiagnostics: false);

        Assert.Contains(" Runtime phase: PrehistoryRunning | Running evolutionary history", lines);
        Assert.Contains(" Subphase: Diverging regional lineages", lines);
        Assert.Contains(" Activity: Diverging isolated lineages into new branches and adaptation paths.", lines);
        Assert.Contains(lines, line => line.Contains("World Age: 180 years", StringComparison.Ordinal));
        Assert.Contains(" Transition: Phase A complete: ecosystems stabilized.", lines);
    }

    [Fact]
    public void BuildDisplayLines_ShowsCandidateMetricsWithoutDuplicateLines()
    {
        World world = new(new WorldTime(920, 1));
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.ReadinessCheckpoint;
        world.StartupStage = WorldStartupStage.PlayerEntryEvaluation;
        world.PrehistoryRuntime.WorldAgeYears = 920;
        world.PrehistoryRuntime.AreReadinessChecksActive = true;
        world.PrehistoryRuntime.PhaseLabel = "Evaluating world readiness";
        world.PrehistoryRuntime.SubphaseLabel = "Building focal candidates";
        world.PrehistoryRuntime.ActivitySummary = "Evaluating whether the world is mature enough to surface healthy starting candidates.";
        world.WorldReadinessReport = new WorldReadinessReport(true, 920, 0.9, 0.86, 0.88, 0.82, 0.8, 3, [], new Dictionary<string, bool>(), 3, 0, 2);
        world.StartupOutcomeDiagnostics = new StartupOutcomeDiagnostics(4, 0, 3, 0, 5, 0, 2, 0, 3, 0, 3, 0, 0, new Dictionary<string, int>(), ["none"], []);
        world.PlayerEntryCandidates.Add(new PlayerEntryCandidateSummary(1, "River Hearth", 10, "Humans", 10, 0, "Green Basin", 12, 920, 2, "mid-sized", "Mixed hunter-forager", "Stable", "paired hearths", "river valley", "deep branch", "Fire", "Fire", "expanded river camps", "good water", 0.92, StabilityBand.Stable, false));

        IReadOnlyList<string> lines = StartupProgressRenderer.BuildDisplayLines(world, includeDiagnostics: false);
        List<string> meaningfulLines = lines
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("=", StringComparison.Ordinal))
            .ToList();

        Assert.Contains(meaningfulLines, line => line.Contains("Candidates: viable 1 | organic 3 | fallback 0", StringComparison.Ordinal));
        Assert.Equal(meaningfulLines.Count, meaningfulLines.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Render_DoesNotPolluteChronicleEntries()
    {
        World world = new(new WorldTime(240, 1));
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.PrehistoryRunning;
        world.StartupStage = WorldStartupStage.SentienceActivation;
        world.PrehistoryRuntime.WorldAgeYears = 240;
        world.PrehistoryRuntime.PhaseLabel = "Developing sentient societies";
        world.PrehistoryRuntime.SubphaseLabel = "Growing groups, settlements, and polities";
        world.PrehistoryRuntime.ActivitySummary = "Growing early societies, settlements, and the first plausible polity starts.";
        world.PhaseCReadinessReport = new PhaseCReadinessReport(false, 3, 3, 0, 2, 2, 0, 2, 2, 0, 2, 1, 1, 0, 0, 0, 0, 24, 0.4, []);
        world.StartupOutcomeDiagnostics = new StartupOutcomeDiagnostics(3, 0, 2, 0, 2, 0, 1, 0, 0, 0, 0, 0, 0, new Dictionary<string, int>(), [], []);

        StartupProgressRenderer startupRenderer = new(new SimulationOptions { OutputMode = OutputMode.Watch });
        ChronicleWatchRenderer chronicleRenderer = new(
            new SimulationOptions { OutputMode = OutputMode.Watch },
            new ChronicleColorWriter(),
            new ChronicleEventFormatter());

        startupRenderer.Render(world);

        Assert.Empty(chronicleRenderer.SnapshotChronicleEntries());
    }

    [Fact]
    public void ClearForHandoff_ResetsStartupFrameState()
    {
        World world = new(new WorldTime(400, 1))
        {
            StartupStage = WorldStartupStage.FocalSelection
        };
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.FocalSelection;
        world.PrehistoryRuntime.WorldAgeYears = 400;
        world.PrehistoryRuntime.PhaseLabel = "World generation complete";
        world.PrehistoryRuntime.SubphaseLabel = "Building focal starts";
        world.PrehistoryRuntime.ActivitySummary = "Preparing the final candidate starts for selection.";
        world.PlayerEntryCandidates.Add(new PlayerEntryCandidateSummary(1, "Stonewater", 10, "Humans", 10, 0, "Stonewater", 5, 400, 2, "small", "Foraging-focused", "Stable", "paired camps", "temperate uplands", "shallow branch", "Fire", "Fire", "held together through migration", "scarcity pressure", 0.71, StabilityBand.Stable, false));

        StartupProgressRenderer renderer = new(new SimulationOptions { OutputMode = OutputMode.Watch });
        renderer.Render(world);

        Assert.True(renderer.HasActiveFrame);
        Assert.NotEmpty(renderer.SnapshotLastRenderedLines());

        renderer.ClearForHandoff();

        Assert.False(renderer.HasActiveFrame);
        Assert.Empty(renderer.SnapshotLastRenderedLines());
    }

    [Fact]
    public void BuildDisplayLines_ShowsGenerationFailureMessage()
    {
        World world = new(new WorldTime(35, 6))
        {
            StartupStage = WorldStartupStage.FocalSelection
        };
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.GenerationFailure;
        world.PrehistoryRuntime.WorldAgeYears = 35;
        world.PrehistoryRuntime.PhaseLabel = "World generation failure";
        world.PrehistoryRuntime.SubphaseLabel = "No viable starts";
        world.PrehistoryRuntime.ActivitySummary = "The simulation could not produce viable player starts.";
        world.PrehistoryRuntime.LastCheckpointOutcome = PrehistoryCheckpointOutcome.Failure("generation_failed_no_candidates", "no_cand");

        IReadOnlyList<string> lines = StartupProgressRenderer.BuildDisplayLines(world, includeDiagnostics: false);
        Assert.Contains(" Runtime phase: GenerationFailure | World generation failure", lines);
        Assert.Contains(" Metrics: world generation failed to surface viable starts", lines);
    }
}
