using LivingWorld.Core;
using LivingWorld.Presentation;
using LivingWorld.Societies;
using Xunit;

namespace LivingWorld.Tests;

public sealed class StartupProgressRendererTests
{
    [Fact]
    public void BuildDisplayLines_ShowsPlayerFacingWorldGenerationStructure()
    {
        World world = new(new WorldTime(180, 1))
        {
            StartupGenerationAttempt = 1
        };
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.BiologicalDivergence;
        world.PrehistoryRuntime.DetailView = PrehistoryRuntimeDetailView.EvolutionaryExpansion;
        world.StartupStage = WorldStartupStage.EvolutionaryExpansion;
        world.PrehistoryRuntime.WorldAgeYears = 180;
        world.PrehistoryRuntime.AreReadinessChecksActive = true;
        world.PrehistoryRuntime.PhaseLabel = "Letting lineages branch, adapt, die out, and recolonize";
        world.PrehistoryRuntime.SubphaseLabel = "Diverging regional lineages";
        world.PrehistoryRuntime.ActivitySummary = "Diverging isolated lineages into new branches and adaptation paths.";
        world.PrehistoryRuntime.TransitionSummary = "Phase A complete: ecosystems stabilized.";
        world.PhaseBDiagnostics = new PhaseBDiagnostics(2.4, 6, 3, 4, 5, 2, 1, 2, 2, 2, []);
        world.PhaseBReadinessReport = new PhaseBReadinessReport(true, 5, 4, 2, 5, 2, 2, 7, []);

        IReadOnlyList<string> lines = StartupProgressRenderer.BuildDisplayLines(world, includeDiagnostics: false);

        Assert.Contains(" WORLD GENERATION", lines);
        Assert.Contains(" Era / Stage: Age of Divergence", lines);
        Assert.Contains(lines, line => line.Contains("World Age: 180 years", StringComparison.Ordinal));
        Assert.Contains(" Current Outlook", lines);
        Assert.Contains(lines, line => line.StartsWith(" Status: ", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("Preset:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("Window:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("Attempt:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("Checkpoint:", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildDisplayLines_KeepsCandidateReviewPlayerFacing()
    {
        World world = new(new WorldTime(920, 1));
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.SocialEmergence;
        world.PrehistoryRuntime.DetailView = PrehistoryRuntimeDetailView.CandidateEvaluation;
        world.StartupStage = WorldStartupStage.PlayerEntryEvaluation;
        world.PrehistoryRuntime.WorldAgeYears = 920;
        world.PrehistoryRuntime.AreReadinessChecksActive = true;
        world.PrehistoryRuntime.PhaseLabel = "Evaluating viable starts";
        world.PrehistoryRuntime.SubphaseLabel = "Building focal candidates";
        world.PrehistoryRuntime.ActivitySummary = "Evaluating whether the world is mature enough to surface healthy starting candidates.";
        world.WorldReadinessReport = new WorldReadinessReport(
            new WorldAgeGateReport(920, 700, 1000, 1400, PrehistoryAgeGateStatus.MinimumAgeReached),
            PrehistoryCheckpointOutcomeKind.ContinuePrehistory,
            [
                new(WorldReadinessCategoryKind.BiologicalReadiness, ReadinessAssessmentStatus.Pass, ReadinessCategoryStrictness.Medium, "bio", Array.Empty<string>(), Array.Empty<string>()),
                new(WorldReadinessCategoryKind.SocialEmergenceReadiness, ReadinessAssessmentStatus.Pass, ReadinessCategoryStrictness.Strict, "social", Array.Empty<string>(), Array.Empty<string>()),
                new(WorldReadinessCategoryKind.WorldStructureReadiness, ReadinessAssessmentStatus.Warning, ReadinessCategoryStrictness.Strict, "structure", Array.Empty<string>(), ["thin"]),
                new(WorldReadinessCategoryKind.CandidateReadiness, ReadinessAssessmentStatus.Warning, ReadinessCategoryStrictness.Strict, "candidate", Array.Empty<string>(), ["thin"]),
                new(WorldReadinessCategoryKind.VarietyReadiness, ReadinessAssessmentStatus.Warning, ReadinessCategoryStrictness.Soft, "variety", Array.Empty<string>(), ["thin"]),
                new(WorldReadinessCategoryKind.AgencyReadiness, ReadinessAssessmentStatus.Pass, ReadinessCategoryStrictness.Soft, "agency", Array.Empty<string>(), Array.Empty<string>())
            ],
            new CandidatePoolReadinessSummary(3, 3, 2, 3, 0, 2, 2, 3, 2, false, "3 viable starts"),
            Array.Empty<string>(),
            ["thin_world"],
            true,
            false,
            new WorldReadinessSummaryData("World not ready yet; prehistory continues.", "3 viable starts", "Readiness remains weak or incomplete.", 3, 3, 0));
        world.StartupOutcomeDiagnostics = StartupOutcomeDiagnostics.Empty with
        {
            OrganicSentientGroupCount = 4,
            OrganicSocietyCount = 3,
            OrganicSettlementCount = 5,
            OrganicPolityCount = 2,
            OrganicFocalCandidateCount = 3,
            OrganicPlayerEntryCandidateCount = 3,
            Bottlenecks = [new StartupDiagnosticReasonCount(StartupDiagnosticReasonKind.Inferred, "none", 1)],
            ReasonSummary = "none"
        };
        world.PlayerEntryCandidates.Add(new PlayerEntryCandidateSummary(1, "River Hearth", 10, "Humans", 10, 0, "Green Basin", 12, 920, 2, "mid-sized", "Mixed hunter-forager", "Stable", "paired hearths", "river valley", "deep branch", "Fire", "Fire", "expanded river camps", "good water", 0.92, StabilityBand.Stable, false));

        IReadOnlyList<string> lines = StartupProgressRenderer.BuildDisplayLines(world, includeDiagnostics: false);

        Assert.Contains(" Era / Stage: Approaching First Starts", lines);
        Assert.Contains(lines, line => line.Contains("truthful starts are being reviewed", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Potential starts exist, but they still need to pass the final review.", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Promising starts exist, but the world still needs review.", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("Candidates:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("Readiness:", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_DoesNotPolluteChronicleEntries()
    {
        World world = new(new WorldTime(240, 1));
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.SocialEmergence;
        world.PrehistoryRuntime.DetailView = PrehistoryRuntimeDetailView.SocietalEmergence;
        world.StartupStage = WorldStartupStage.SentienceActivation;
        world.PrehistoryRuntime.WorldAgeYears = 240;
        world.PrehistoryRuntime.PhaseLabel = "Persistent peoples, settlements, and early polities forming";
        world.PrehistoryRuntime.SubphaseLabel = "Growing groups, settlements, and polities";
        world.PrehistoryRuntime.ActivitySummary = "Growing early societies, settlements, and the first plausible polity starts.";
        world.PhaseCReadinessReport = new PhaseCReadinessReport(false, 3, 3, 0, 2, 2, 0, 2, 2, 0, 2, 1, 1, 0, 0, 0, 0, 24, 0.4, []);
        world.StartupOutcomeDiagnostics = StartupOutcomeDiagnostics.Empty with
        {
            OrganicSentientGroupCount = 3,
            OrganicSocietyCount = 2,
            OrganicSettlementCount = 2,
            OrganicPolityCount = 1
        };

        StartupProgressRenderer startupRenderer = new(new SimulationOptions { OutputMode = OutputMode.Watch });
        ChronicleWatchRenderer chronicleRenderer = new(
            new SimulationOptions { OutputMode = OutputMode.Watch },
            new ChronicleColorWriter(),
            new ChronicleEventFormatter());

        startupRenderer.Render(world);

        Assert.Empty(chronicleRenderer.SnapshotChronicleEntries());
    }

    [Fact]
    public void Render_BatchesLongRunningPrehistoryToHundredYearCadence()
    {
        World world = new(new WorldTime(100, 1));
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.BiologicalDivergence;
        world.PrehistoryRuntime.DetailView = PrehistoryRuntimeDetailView.EvolutionaryExpansion;
        world.PrehistoryRuntime.WorldAgeYears = 100;
        world.PrehistoryRuntime.PhaseLabel = "Letting lineages branch, adapt, die out, and recolonize";
        world.PrehistoryRuntime.SubphaseLabel = "Diverging regional lineages";
        world.PrehistoryRuntime.ActivitySummary = "Diverging isolated lineages into new branches and adaptation paths.";
        world.PhaseBReadinessReport = new PhaseBReadinessReport(false, 3, 1, 0, 2, 0, 1, 6, []);

        StartupProgressRenderer renderer = new(new SimulationOptions { OutputMode = OutputMode.Watch });

        renderer.Render(world);
        Assert.Contains(renderer.SnapshotLastRenderedLines(), line => line.Contains("World Age: 100 years", StringComparison.Ordinal));

        world.PrehistoryRuntime.WorldAgeYears = 150;
        renderer.Render(world);
        Assert.Contains(renderer.SnapshotLastRenderedLines(), line => line.Contains("World Age: 100 years", StringComparison.Ordinal));

        world.PrehistoryRuntime.WorldAgeYears = 200;
        renderer.Render(world);
        Assert.Contains(renderer.SnapshotLastRenderedLines(), line => line.Contains("World Age: 200 years", StringComparison.Ordinal));
    }

    [Fact]
    public void ClearForHandoff_ResetsStartupFrameState()
    {
        World world = new(new WorldTime(400, 1))
        {
            StartupStage = WorldStartupStage.FocalSelection
        };
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.FocalSelection;
        world.PrehistoryRuntime.DetailView = PrehistoryRuntimeDetailView.FocalSelection;
        world.PrehistoryRuntime.WorldAgeYears = 400;
        world.PrehistoryRuntime.PhaseLabel = "Reviewing surfaced candidate starts";
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
        world.PrehistoryRuntime.DetailView = PrehistoryRuntimeDetailView.GenerationFailure;
        world.PrehistoryRuntime.WorldAgeYears = 35;
        world.PrehistoryRuntime.PhaseLabel = "No viable truthful start was produced";
        world.PrehistoryRuntime.SubphaseLabel = "Candidate pool collapsed before handoff";
        world.PrehistoryRuntime.ActivitySummary = "The simulation could not produce a viable truthful start.";
        world.PrehistoryRuntime.LastCheckpointOutcome = PrehistoryCheckpointOutcome.Failure("generation_failed_no_candidates", "no_cand");

        IReadOnlyList<string> lines = StartupProgressRenderer.BuildDisplayLines(world, includeDiagnostics: false);
        Assert.Contains(" Era / Stage: Generation Failure", lines);
        Assert.Contains(lines, line => line.Contains("This world could not produce a truthful player start.", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("Metrics:", StringComparison.Ordinal));
    }
}
