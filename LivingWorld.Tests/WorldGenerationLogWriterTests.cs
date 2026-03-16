using System.IO;
using LivingWorld.Core;
using LivingWorld.Presentation;
using Xunit;

namespace LivingWorld.Tests;

public sealed class WorldGenerationLogWriterTests
{
    [Fact]
    public void BuildDefaultFilePath_UsesWorldgenTimestampPattern()
    {
        string path = WorldGenerationLogWriter.BuildDefaultFilePath(new DateTime(2024, 2, 3, 4, 5, 6));

        Assert.Equal(Path.Combine("logs", "worldgen-20240203-040506.txt"), path);
    }

    [Fact]
    public void RecordWorldState_WritesPhaseCheckpointBatchAndPostmortemDetails()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LivingWorld.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "worldgen-test.txt");
        GenerationAttemptDiagnosticsSummary summary = new(
            AttemptNumber: 1,
            WorldAgeYears: 200,
            FinalPhase: PrehistoryRuntimePhase.GenerationFailure,
            FinalSubphase: "Final candidate pass",
            Outcome: PrehistoryCheckpointOutcomeKind.GenerationFailure,
            TotalViableCandidatesDiscovered: 0,
            SurfacedCandidateCount: 0,
            NormalReadyCandidateCount: 0,
            Population: new GenerationAttemptPopulationSnapshot(2, 0, 1, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0),
            PrimaryFailureKind: GenerationFailurePrimaryKind.FinalCandidateViabilityCollapse,
            ZeroViableCause: GenerationZeroViableCause.CandidateTruthFloorCollapse,
            ReasonSummary: "No viable truthful starts survived the final review.",
            RankedBottlenecks: [new StartupDiagnosticReasonCount(StartupDiagnosticReasonKind.CandidateReadiness, "no_viable_candidates", 2)],
            RankedCandidateRejections: [new StartupDiagnosticReasonCount(StartupDiagnosticReasonKind.CandidateReadiness, "candidate_pool_not_yet_ready", 1)],
            RegenerationReasons: []);

        World world = new(new WorldTime(200, 1));
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.WorldReadinessReview;
        world.PrehistoryRuntime.DetailView = PrehistoryRuntimeDetailView.CandidateEvaluation;
        world.PrehistoryRuntime.WorldAgeYears = 200;
        world.PrehistoryRuntime.PhaseLabel = "Reviewing whether the world can stop truthfully";
        world.PrehistoryRuntime.SubphaseLabel = "Checking stop conditions";
        world.PrehistoryRuntime.ActivitySummary = "Evaluating whether the world is ready for player entry or needs more historical time.";
        world.PrehistoryRuntime.LastCheckpointOutcome = PrehistoryCheckpointOutcome.Continue("World not ready yet; prehistory continues.", "warning:thin_world");
        world.WorldReadinessReport = new WorldReadinessReport(
            new WorldAgeGateReport(200, 100, 200, 400, PrehistoryAgeGateStatus.TargetAgeReached),
            PrehistoryCheckpointOutcomeKind.ContinuePrehistory,
            WorldReadinessReport.Empty.CategoryResults,
            new CandidatePoolReadinessSummary(1, 1, 0, 1, 0, 1, 1, 1, 1, true, "1 viable start"),
            Array.Empty<string>(),
            ["thin_world"],
            true,
            true,
            new WorldReadinessSummaryData("World not ready yet; prehistory continues.", "1 viable start", "Readiness remains weak or incomplete.", 1, 1, 0));
        world.StartupOutcomeDiagnostics = StartupOutcomeDiagnostics.Empty with
        {
            ReasonSummary = "candidate pool not ready",
            Bottlenecks = [new StartupDiagnosticReasonCount(StartupDiagnosticReasonKind.CandidateReadiness, "candidate_pool_not_yet_ready", 2)],
            CandidateRejections = [new StartupDiagnosticReasonCount(StartupDiagnosticReasonKind.CandidateReadiness, "no_viable_candidates", 1)]
        };
        world.PhaseAReadinessReport = new PhaseAReadinessReport(10, 8, 0.82, 7, 0.74, 5, 0.51, 2, 0.28, 6, 4, 1, true, []);
        world.PhaseBReadinessReport = new PhaseBReadinessReport(false, 4, 1, 0, 3, 1, 1, 5, []);
        world.PhaseBDiagnostics = new PhaseBDiagnostics(1.5, 4, 2, 2, 3, 1, 0, 2, 1, 1, []);
        world.PhaseCReadinessReport = new PhaseCReadinessReport(false, 2, 2, 0, 1, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 8, 0.0, []);

        using (WorldGenerationLogWriter writer = new(path, new DateTime(2024, 2, 3, 4, 5, 6)))
        {
            writer.RecordSessionStart(12345);
            writer.RecordWorldState(world);
            writer.RecordAttemptSummary(world, summary, willRegenerate: false);
            writer.RecordFinalFailure(new GenerationFailurePostmortem(
                "No viable truthful start was produced.",
                "The generator stopped honestly after exhausting all attempts.",
                GenerationFailurePrimaryKind.FinalCandidateViabilityCollapse,
                GenerationZeroViableCause.CandidateTruthFloorCollapse,
                summary,
                [new GenerationAggregateReasonCount(StartupDiagnosticReasonKind.CandidateReadiness, "no_viable_candidates", 2, 1)],
                [new GenerationAggregateReasonCount(StartupDiagnosticReasonKind.CandidateReadiness, "candidate_pool_not_yet_ready", 1, 1)],
                GenerationFailurePatternKind.SingleAttempt,
                "One attempt failed with the same candidate-truth bottleneck throughout."));
        }

        string contents = File.ReadAllText(path);

        Assert.Contains("WORLD GENERATION LOG", contents);
        Assert.Contains("Phase change: World Readiness Review", contents);
        Assert.Contains("Readiness review", contents);
        Assert.Contains("Year 200 summary", contents);
        Assert.Contains("Outcome: Continue Prehistory", contents);
        Assert.Contains("Attempt 1 result", contents);
        Assert.Contains("Generation attempt 1", contents);
        Assert.Contains("Final failure postmortem", contents);
    }

    [Fact]
    public void RecordSessionStart_CreatesFileImmediatelyWithHeader()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LivingWorld.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "worldgen-start.txt");

        using (WorldGenerationLogWriter writer = new(path, new DateTime(2024, 2, 3, 4, 5, 6)))
        {
            writer.RecordSessionStart(12345);

            Assert.True(File.Exists(path));
        }

        string contents = File.ReadAllText(path);
        Assert.Contains("WORLD GENERATION LOG", contents);
        Assert.Contains("Seed: 12345", contents);
    }

    [Fact]
    public void Dispose_WithoutCompletion_WritesInterruptedFooter()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LivingWorld.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "worldgen-interrupted.txt");

        using (WorldGenerationLogWriter writer = new(path, new DateTime(2024, 2, 3, 4, 5, 6)))
        {
            writer.RecordSessionStart(12345);
        }

        string contents = File.ReadAllText(path);

        Assert.Contains("Run interrupted", contents);
        Assert.Contains("Run ended before a normal completion footer was written.", contents);
    }

    [Fact]
    public void RecordCompleted_WritesNormalCompletionFooterWithoutInterruptedFooter()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LivingWorld.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "worldgen-complete.txt");
        World world = new(new WorldTime(200, 1));
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.FocalSelection;
        world.PrehistoryRuntime.WorldAgeYears = 200;

        using (WorldGenerationLogWriter writer = new(path, new DateTime(2024, 2, 3, 4, 5, 6)))
        {
            writer.RecordSessionStart(12345);
            writer.RecordCompleted(world);
        }

        string contents = File.ReadAllText(path);

        Assert.Contains("Run completed", contents);
        Assert.Contains("Final phase: Focal Selection", contents);
        Assert.DoesNotContain("Run interrupted", contents);
    }
}
