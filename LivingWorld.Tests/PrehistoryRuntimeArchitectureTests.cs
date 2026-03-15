using LivingWorld.Core;
using LivingWorld.Generation;
using LivingWorld.Presentation;
using Xunit;

namespace LivingWorld.Tests;

public sealed class PrehistoryRuntimeArchitectureTests
{
    [Fact]
    public void LegacyCheckpointCompatibilityAdapter_ReturnsEvaluationArtifactsWithoutMutatingWorldState()
    {
        World world = new WorldGenerator(seed: 43).Generate();
        world.Prehistory.CandidateSelection.Clear();
        world.Prehistory.LegacyCompatibility.ReplaceStartupDiagnostics([]);
        world.WorldReadinessReport = WorldReadinessReport.Empty;
        world.StartupOutcomeDiagnostics = StartupOutcomeDiagnostics.Empty;

        LegacyCheckpointCompatibilityAdapter adapter = new(new WorldGenerationSettings());
        PrehistoryCheckpointEvaluationResult result = adapter.Evaluate(world, allowEmergencyFallback: false, regenerationReasons: null);

        Assert.Empty(world.PlayerEntryCandidates);
        Assert.Empty(world.StartupDiagnostics);
        Assert.Equal(WorldReadinessReport.Empty, world.WorldReadinessReport);
        Assert.NotEmpty(result.PlayerEntryCandidates);
        Assert.NotNull(result.CandidatePoolSnapshot);
    }

    [Fact]
    public void WorldGenerator_ResolvesRegenerationFromCheckpointOutcomeFlow()
    {
        World world = new WorldGenerator(seed: 19, CreateImpossibleEntrySettings(maxAttempts: 2)).Generate();

        Assert.Equal(PrehistoryRuntimePhase.GenerationFailure, world.PrehistoryRuntime.CurrentPhase);
        Assert.Equal(PrehistoryCheckpointOutcomeKind.GenerationFailure, world.PrehistoryRuntime.LastCheckpointOutcome?.Kind);
        Assert.Contains(world.StartupDiagnostics, line => line.StartsWith("regeneration_reason:attempt_0:", StringComparison.Ordinal));
    }

    [Fact]
    public void Simulation_EntersActivePlayOnlyThroughExplicitHandoff()
    {
        World world = new WorldGenerator(seed: 43).Generate();

        using Simulation simulation = new(world, new SimulationOptions { OutputMode = OutputMode.Debug, WriteStructuredHistory = false });

        Assert.Equal(PrehistoryRuntimePhase.FocalSelection, world.PrehistoryRuntime.CurrentPhase);
        Assert.Null(world.SelectedFocalPolityId);
        Assert.Null(world.LiveChronicleStartYear);

        simulation.RunMonths(1);

        Assert.Equal(PrehistoryRuntimePhase.ActivePlay, world.PrehistoryRuntime.CurrentPhase);
        Assert.NotNull(world.SelectedFocalPolityId);
        Assert.NotNull(world.LiveChronicleStartYear);
    }

    [Fact]
    public void GenerationFailureRemainsFrozenWithoutStartingLiveChronicle()
    {
        World world = new WorldGenerator(seed: 19, CreateImpossibleEntrySettings()).Generate();

        Assert.Equal(PrehistoryRuntimePhase.GenerationFailure, world.PrehistoryRuntime.CurrentPhase);
        Assert.Empty(world.PlayerEntryCandidates);

        int year = world.Time.Year;
        int month = world.Time.Month;
        using Simulation simulation = new(world, new SimulationOptions { OutputMode = OutputMode.Debug, WriteStructuredHistory = false });

        Assert.Null(world.LiveChronicleStartYear);
        simulation.RunMonths(12);

        Assert.Equal(year, world.Time.Year);
        Assert.Equal(month, world.Time.Month);
        Assert.Null(world.LiveChronicleStartYear);
        Assert.Null(world.SelectedFocalPolityId);
    }

    [Fact]
    public void StartupProgressRenderer_UsesRuntimeDetailViewWithoutNeedingLegacyStartupStage()
    {
        World world = new(new WorldTime(180, 1));
        world.StartupStage = WorldStartupStage.PrimitiveEcologyFoundation;
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.PrehistoryRunning;
        world.PrehistoryRuntime.DetailView = PrehistoryRuntimeDetailView.EvolutionaryExpansion;
        world.PrehistoryRuntime.WorldAgeYears = 180;
        world.PrehistoryRuntime.PhaseLabel = "Running evolutionary history";
        world.PrehistoryRuntime.SubphaseLabel = "Diverging regional lineages";
        world.PrehistoryRuntime.ActivitySummary = "Diverging isolated lineages into new branches and adaptation paths.";
        world.PhaseBDiagnostics = new PhaseBDiagnostics(2.4, 6, 3, 4, 5, 2, 1, 2, 2, 2, []);
        world.PhaseBReadinessReport = new PhaseBReadinessReport(true, 5, 4, 2, 5, 2, 2, 7, []);

        IReadOnlyList<string> lines = StartupProgressRenderer.BuildDisplayLines(world, includeDiagnostics: false);

        Assert.Contains(lines, line => line.Contains("Evolution:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("producer coverage", StringComparison.OrdinalIgnoreCase));
    }

    private static WorldGenerationSettings CreateImpossibleEntrySettings(int maxAttempts = 1)
        => new()
        {
            StartupWorldAgePreset = StartupWorldAgePreset.YoungWorld,
            RegionCount = 16,
            ContinentWidth = 4,
            ContinentHeight = 4,
            PhaseAMaximumBootstrapMonths = 24,
            PhaseBMinimumBootstrapYears = 80,
            PhaseBMaximumBootstrapYears = 160,
            PhaseCMinimumBootstrapYears = 60,
            PhaseCMaximumBootstrapYears = 140,
            ReadinessEvaluationIntervalYears = 10,
            MaxStartupRegenerationAttempts = maxAttempts,
            CandidateMinimumPopulation = 5000,
            CandidateMinimumPolityAgeYears = 200,
            MinimumViablePlayerEntryCandidates = 4,
            MinimumHealthyCandidateCount = 4
        };
}
