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
        StubCheckpointAdapter adapter = new(PrehistoryCheckpointOutcomeKind.ContinuePrehistory, CreateCandidate(1));
        PrehistoryCheckpointCoordinator coordinator = CreateCoordinator(adapter);

        PrehistoryCheckpointOutcome outcome = coordinator.Evaluate(
            world,
            phaseLabel: "Testing",
            subphaseLabel: "Subphase",
            activitySummary: "Activity",
            completionSummary: "completion",
            allowEmergencyFallback: false);

        Assert.Equal(PrehistoryRuntimePhase.SocialEmergence, world.PrehistoryRuntime.CurrentPhase);
        Assert.True(world.PrehistoryRuntime.IsPrehistoryAdvancing);
        Assert.Equal(PrehistoryCheckpointOutcomeKind.ContinuePrehistory, outcome.Kind);
        Assert.Equal(PrehistoryCheckpointOutcomeKind.ContinuePrehistory, world.PrehistoryRuntime.LastCheckpointOutcome?.Kind);
    }

    [Fact]
    public void EnterFocalSelectionPausesAdvancement()
    {
        World world = CreatePrehistoryWorld();
        StubCheckpointAdapter adapter = new(PrehistoryCheckpointOutcomeKind.EnterFocalSelection, CreateCandidate(2));
        PrehistoryCheckpointCoordinator coordinator = CreateCoordinator(adapter);

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
        StubCheckpointAdapter adapter = new(PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection, CreateCandidate(3));
        PrehistoryCheckpointCoordinator coordinator = CreateCoordinator(adapter);

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
        StubCheckpointAdapter adapter = new(PrehistoryCheckpointOutcomeKind.GenerationFailure);
        PrehistoryCheckpointCoordinator coordinator = CreateCoordinator(adapter);

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
        StubCheckpointAdapter adapter = new(PrehistoryCheckpointOutcomeKind.ContinuePrehistory, candidate);
        PrehistoryCheckpointCoordinator coordinator = CreateCoordinator(adapter);

        coordinator.Evaluate(
            world,
            phaseLabel: "Testing",
            subphaseLabel: "Candidates",
            activitySummary: "Activity",
            completionSummary: "completion",
            allowEmergencyFallback: false);

        Assert.Contains(candidate, world.PlayerEntryCandidates);
    }

    private static PrehistoryCheckpointCoordinator CreateCoordinator(ICheckpointEvaluationAdapter adapter)
        => new(
            new PrehistoryRuntimeOrchestrator(),
            adapter);

    private static World CreatePrehistoryWorld()
    {
        World world = new(new WorldTime())
        {
            StartupStage = WorldStartupStage.PlayerEntryEvaluation
        };
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.SocialEmergence;
        world.PrehistoryRuntime.LastAdvancingPhase = PrehistoryRuntimePhase.SocialEmergence;
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
        private readonly PrehistoryCheckpointOutcomeKind _resolution;
        private readonly IReadOnlyList<PlayerEntryCandidateSummary> _candidates;
        private readonly IReadOnlyDictionary<int, string> _rejectionReasons;

        public StubCheckpointAdapter(PrehistoryCheckpointOutcomeKind resolution, params PlayerEntryCandidateSummary[] candidates)
            : this(resolution, candidates, new Dictionary<int, string>())
        {
        }

        public StubCheckpointAdapter(PrehistoryCheckpointOutcomeKind resolution, IReadOnlyList<PlayerEntryCandidateSummary> candidates, IReadOnlyDictionary<int, string> rejectionReasons)
        {
            _resolution = resolution;
            _candidates = candidates;
            _rejectionReasons = rejectionReasons;
        }

        public PrehistoryCheckpointEvaluationResult Evaluate(World world, bool allowEmergencyFallback, IReadOnlyList<string>? regenerationReasons)
        {
            WorldReadinessReport report = new(
                new WorldAgeGateReport(900, 700, 1000, 1400, PrehistoryAgeGateStatus.MinimumAgeReached),
                _resolution,
                WorldReadinessReport.Empty.CategoryResults,
                new CandidatePoolReadinessSummary(_candidates.Count, _candidates.Count, _candidates.Count, _candidates.Count, 0, _candidates.Select(candidate => candidate.SpeciesId).Distinct().Count(), _candidates.Select(candidate => candidate.LineageId).Distinct().Count(), _candidates.Select(candidate => candidate.HomeRegionId).Distinct().Count(), _candidates.Select(candidate => candidate.SubsistenceStyle).Distinct(StringComparer.OrdinalIgnoreCase).Count(), _candidates.Count < 2, $"{_candidates.Count} candidates"),
                _resolution == PrehistoryCheckpointOutcomeKind.GenerationFailure ? ["no_viable_candidates"] : Array.Empty<string>(),
                Array.Empty<string>(),
                false,
                _candidates.Count < 2,
                new WorldReadinessSummaryData($"Stub {_resolution}", $"{_candidates.Count} viable starts", "Stub condition", 0, 0, _resolution == PrehistoryCheckpointOutcomeKind.GenerationFailure ? 1 : 0));
            return new PrehistoryCheckpointEvaluationResult
            {
                WorldReadinessReport = report,
                StartupOutcomeDiagnostics = StartupOutcomeDiagnostics.Empty,
                StartupDiagnostics = Array.Empty<string>(),
                PlayerEntryCandidates = _candidates,
                CandidateRejectionReasons = _rejectionReasons
            };
        }
    }
}
