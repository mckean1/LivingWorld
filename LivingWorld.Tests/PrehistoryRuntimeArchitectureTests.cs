using LivingWorld.Core;
using LivingWorld.Generation;
using LivingWorld.Presentation;
using Xunit;

namespace LivingWorld.Tests;

public sealed class PrehistoryRuntimeArchitectureTests : IClassFixture<PrehistoryRuntimeArchitectureTests.Fixture>
{
    private readonly Fixture fixture;

    public PrehistoryRuntimeArchitectureTests(Fixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void LegacyCheckpointCompatibilityAdapter_ReturnsEvaluationArtifactsWithoutMutatingWorldState()
    {
        World world = fixture.CreateGeneratedWorld(seed: 43);
        List<(int PolityId, int ObservationCount)> observerHistoryCounts = world.Polities
            .ConvertAll(polity => (polity.Id, world.PrehistoryObserver.GetPeopleHistory(polity.Id).Count));
        world.Prehistory.CandidateSelection.Clear();
        world.Prehistory.LegacyCompatibility.ReplaceStartupDiagnostics([]);
        world.WorldReadinessReport = WorldReadinessReport.Empty;
        world.StartupOutcomeDiagnostics = StartupOutcomeDiagnostics.Empty;

        LegacyCheckpointCompatibilityAdapter adapter = fixture.CreateLegacyCheckpointCompatibilityAdapter();
        PrehistoryCheckpointEvaluationResult result = adapter.Evaluate(world, allowEmergencyFallback: false, regenerationReasons: null);

        Assert.Empty(world.PlayerEntryCandidates);
        Assert.Empty(world.StartupDiagnostics);
        Assert.Equal(WorldReadinessReport.Empty, world.WorldReadinessReport);
        foreach ((int polityId, int observationCount) in observerHistoryCounts)
        {
            Assert.Equal(observationCount, world.PrehistoryObserver.GetPeopleHistory(polityId).Count);
        }
        Assert.NotEmpty(result.PlayerEntryCandidates);
        Assert.NotNull(result.CandidatePoolSnapshot);
    }

    [Fact]
    public void WorldGenerator_ResolvesRegenerationFromCheckpointOutcomeFlow()
    {
        World world = fixture.CreateImpossibleEntryWorld(seed: 19, maxAttempts: 2);

        Assert.Equal(PrehistoryRuntimePhase.GenerationFailure, world.PrehistoryRuntime.CurrentPhase);
        Assert.Equal(PrehistoryCheckpointOutcomeKind.GenerationFailure, world.PrehistoryRuntime.LastCheckpointOutcome?.Kind);
        Assert.Contains(world.StartupDiagnostics, line => line.StartsWith("regen:attempt_0:", StringComparison.Ordinal));
    }

    [Fact]
    public void Simulation_EntersActivePlayOnlyThroughExplicitHandoff()
    {
        World world = fixture.CreateGeneratedWorld(seed: 43);
        int handoffYear = world.Time.Year;
        int handoffMonth = world.Time.Month;
        int eventCountAtSelection = world.Events.Count;

        using Simulation simulation = new(world, fixture.CreateDebugSimulationOptions());

        Assert.Equal(PrehistoryRuntimePhase.FocalSelection, world.PrehistoryRuntime.CurrentPhase);
        Assert.Null(world.SelectedFocalPolityId);
        Assert.Null(world.LiveChronicleStartYear);

        simulation.RunMonths(1);

        Assert.Equal(PrehistoryRuntimePhase.SimulationEngineActivePlay, world.PrehistoryRuntime.CurrentPhase);
        Assert.NotNull(world.SelectedFocalPolityId);
        Assert.NotNull(world.LiveChronicleStartYear);
        Assert.Equal(handoffYear, world.Time.Year);
        Assert.Equal(handoffMonth, world.Time.Month);
        Assert.True(simulation.IsWatchPaused);
        Assert.True(world.ActivePlayHandoff.Package?.PlayerOwnership.StartsPaused);
        Assert.Equal(world.SelectedFocalPolityId, world.ActivePlayHandoff.Package?.PlayerOwnership.SelectedPeopleId);
        Assert.Equal(eventCountAtSelection, world.Events.Count);
    }

    [Fact]
    public void GenerationFailureRemainsFrozenWithoutStartingLiveChronicle()
    {
        World world = fixture.CreateImpossibleEntryWorld(seed: 19);

        Assert.Equal(PrehistoryRuntimePhase.GenerationFailure, world.PrehistoryRuntime.CurrentPhase);
        Assert.Empty(world.PlayerEntryCandidates);

        int year = world.Time.Year;
        int month = world.Time.Month;
        using Simulation simulation = new(world, fixture.CreateDebugSimulationOptions());

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
        World world = fixture.CreateRuntimeDetailViewWorld();
        world.StartupStage = WorldStartupStage.PrimitiveEcologyFoundation;
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.BiologicalDivergence;
        world.PrehistoryRuntime.DetailView = PrehistoryRuntimeDetailView.EvolutionaryExpansion;
        world.PrehistoryRuntime.WorldAgeYears = 180;
        world.PrehistoryRuntime.PhaseLabel = "Letting lineages branch, adapt, die out, and recolonize";
        world.PrehistoryRuntime.SubphaseLabel = "Diverging regional lineages";
        world.PrehistoryRuntime.ActivitySummary = "Diverging isolated lineages into new branches and adaptation paths.";
        world.PhaseBDiagnostics = new PhaseBDiagnostics(2.4, 6, 3, 4, 5, 2, 1, 2, 2, 2, []);
        world.PhaseBReadinessReport = new PhaseBReadinessReport(true, 5, 4, 2, 5, 2, 2, 7, []);

        IReadOnlyList<string> lines = StartupProgressRenderer.BuildDisplayLines(world, includeDiagnostics: false);

        Assert.Contains(lines, line => line.Contains("Evolution:", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("producer coverage", StringComparison.OrdinalIgnoreCase));
    }

    public sealed class Fixture
    {
        public World CreateGeneratedWorld(int seed)
            => new WorldGenerator(seed: seed).Generate();

        public World CreateImpossibleEntryWorld(int seed, int maxAttempts = 1)
            => new WorldGenerator(seed: seed, CreateImpossibleEntrySettings(maxAttempts)).Generate();

        public LegacyCheckpointCompatibilityAdapter CreateLegacyCheckpointCompatibilityAdapter()
            => new(new WorldGenerationSettings());

        public SimulationOptions CreateDebugSimulationOptions()
            => new()
            {
                OutputMode = OutputMode.Debug,
                WriteStructuredHistory = false
            };

        public World CreateRuntimeDetailViewWorld()
            => new(new WorldTime(180, 1));

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
}
