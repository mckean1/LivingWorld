using System;
using System.Collections.Generic;
using LivingWorld.Core;
using LivingWorld.Generation;
using LivingWorld.Societies;
using Xunit;

namespace LivingWorld.Tests;

public sealed class CheckpointCoordinatorTests
{
    [Fact]
    public void ContinuePrehistoryKeepsPhaseAdvancing()
    {
        World world = CreatePrehistoryWorld();
        StubCheckpointAdapter adapter = new(CreateCandidate(1));
        StubCandidateOutcomeEvaluator outcomeEvaluator = new(shouldSurface: false);
        PrehistoryCheckpointCoordinator coordinator = CreateCoordinator(adapter, outcomeEvaluator);

        PrehistoryCheckpointOutcome outcome = coordinator.Evaluate(
            world,
            phaseLabel: "Testing",
            subphaseLabel: "Subphase",
            activitySummary: "Activity",
            completionSummary: "completion",
            allowEmergencyFallback: false);

        Assert.Equal(PrehistoryRuntimePhase.PrehistoryRunning, world.PrehistoryRuntime.CurrentPhase);
        Assert.True(world.PrehistoryRuntime.IsPrehistoryAdvancing);
        Assert.Equal(PrehistoryCheckpointOutcomeKind.ContinuePrehistory, outcome.Kind);
        Assert.Equal(PrehistoryCheckpointOutcomeKind.ContinuePrehistory, world.PrehistoryRuntime.LastCheckpointOutcome?.Kind);
    }

    [Fact]
    public void EnterFocalSelectionPausesAdvancement()
    {
        World world = CreatePrehistoryWorld();
        StubCheckpointAdapter adapter = new(CreateCandidate(2));
        StubCandidateOutcomeEvaluator outcomeEvaluator = new(shouldSurface: true);
        PrehistoryCheckpointCoordinator coordinator = CreateCoordinator(adapter, outcomeEvaluator);

        PrehistoryCheckpointOutcome outcome = coordinator.Evaluate(
            world,
            phaseLabel: "Testing",
            subphaseLabel: "Selection",
            activitySummary: "Activity",
            completionSummary: "completion",
            allowEmergencyFallback: false);

        Assert.Equal(PrehistoryRuntimePhase.FocalSelection, world.PrehistoryRuntime.CurrentPhase);
        Assert.False(world.PrehistoryRuntime.IsPrehistoryAdvancing);
        Assert.Equal(PrehistoryCheckpointOutcomeKind.EnterFocalSelection, outcome.Kind);
    }

    [Fact]
    public void ForceEnterFocalSelectionAppliesForcedFlag()
    {
        World world = CreatePrehistoryWorld();
        StubCheckpointAdapter adapter = new(CreateCandidate(3));
        StubCandidateOutcomeEvaluator outcomeEvaluator = new(shouldSurface: true);
        PrehistoryCheckpointCoordinator coordinator = CreateCoordinator(adapter, outcomeEvaluator);

        PrehistoryCheckpointOutcome outcome = coordinator.Evaluate(
            world,
            phaseLabel: "Testing",
            subphaseLabel: "Forced",
            activitySummary: "Activity",
            completionSummary: "completion",
            allowEmergencyFallback: true);

        Assert.Equal(PrehistoryRuntimePhase.FocalSelection, world.PrehistoryRuntime.CurrentPhase);
        Assert.Equal(PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection, outcome.Kind);
    }

    [Fact]
    public void GenerationFailureIsRepresentedWhenNoCandidates()
    {
        World world = CreatePrehistoryWorld();
        StubCheckpointAdapter adapter = new();
        StubCandidateOutcomeEvaluator outcomeEvaluator = new(shouldSurface: false);
        PrehistoryCheckpointCoordinator coordinator = CreateCoordinator(adapter, outcomeEvaluator);

        PrehistoryCheckpointOutcome outcome = coordinator.Evaluate(
            world,
            phaseLabel: "Testing",
            subphaseLabel: "Failure",
            activitySummary: "Activity",
            completionSummary: "completion",
            allowEmergencyFallback: true);

        Assert.Equal(PrehistoryRuntimePhase.GenerationFailure, world.PrehistoryRuntime.CurrentPhase);
        Assert.Equal(PrehistoryCheckpointOutcomeKind.GenerationFailure, outcome.Kind);
    }

    [Fact]
    public void EvaluationLayerProducesCandidatePoolState()
    {
        World world = CreatePrehistoryWorld();
        PlayerEntryCandidateSummary candidate = CreateCandidate(5);
        StubCheckpointAdapter adapter = new(candidate);
        StubCandidateOutcomeEvaluator outcomeEvaluator = new(shouldSurface: false);
        PrehistoryCheckpointCoordinator coordinator = CreateCoordinator(adapter, outcomeEvaluator);

        coordinator.Evaluate(
            world,
            phaseLabel: "Testing",
            subphaseLabel: "Candidates",
            activitySummary: "Activity",
            completionSummary: "completion",
            allowEmergencyFallback: false);

        Assert.Contains(candidate, world.PlayerEntryCandidates);
    }

    private static PrehistoryCheckpointCoordinator CreateCoordinator(ICheckpointEvaluationAdapter adapter, ICandidateOutcomeEvaluator outcomeEvaluator)
        => new(
            new PrehistoryRuntimeOrchestrator(),
            adapter,
            outcomeEvaluator,
            new WorldGenerationSettings());

    private static World CreatePrehistoryWorld()
    {
        World world = new(new WorldTime())
        {
            StartupStage = WorldStartupStage.PlayerEntryEvaluation
        };
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.PrehistoryRunning;
        world.PrehistoryRuntime.IsPrehistoryAdvancing = true;
        return world;
    }

    private static PlayerEntryCandidateSummary CreateCandidate(int polityId)
        => new(
            polityId,
            $"Polity {polityId}",
            polityId,
            $"Species {polityId}",
            polityId,
            polityId,
            $"Region {polityId}",
            10,
            25,
            2,
            "Medium",
            "Mixed",
            "Stable",
            "Steady",
            "Temperate",
            "Rooted",
            "Discovery",
            "Learned",
            "Note",
            "Opportunity",
            0.5,
            StabilityBand.Stable,
            false);

    private sealed class StubCheckpointAdapter : ICheckpointEvaluationAdapter
    {
        private readonly IReadOnlyList<PlayerEntryCandidateSummary> _candidates;
        private readonly IReadOnlyDictionary<int, string> _rejectionReasons;

        public StubCheckpointAdapter(params PlayerEntryCandidateSummary[] candidates)
            : this(candidates, new Dictionary<int, string>())
        {
        }

        public StubCheckpointAdapter(IReadOnlyList<PlayerEntryCandidateSummary> candidates, IReadOnlyDictionary<int, string> rejectionReasons)
        {
            _candidates = candidates;
            _rejectionReasons = rejectionReasons;
        }

        public PrehistoryCheckpointEvaluationResult Evaluate(World world, bool allowEmergencyFallback, IReadOnlyList<string>? regenerationReasons)
        {
            return new PrehistoryCheckpointEvaluationResult
            {
                WorldReadinessReport = WorldReadinessReport.Empty,
                StartupOutcomeDiagnostics = StartupOutcomeDiagnostics.Empty,
                StartupDiagnostics = Array.Empty<string>(),
                PlayerEntryCandidates = _candidates,
                CandidateRejectionReasons = _rejectionReasons
            };
        }
    }

    private sealed class StubCandidateOutcomeEvaluator : ICandidateOutcomeEvaluator
    {
        private readonly bool _shouldSurface;

        public StubCandidateOutcomeEvaluator(bool shouldSurface)
        {
            _shouldSurface = shouldSurface;
        }

        public bool ShouldSurfaceFocalSelection(World world, WorldGenerationSettings settings, bool allowEmergencyFallback, out List<string> rejectionReasons)
        {
            rejectionReasons = new();
            return _shouldSurface;
        }
    }
}
