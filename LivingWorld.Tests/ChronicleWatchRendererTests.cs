using System;
using System.Collections.Generic;
using LivingWorld.Core;
using LivingWorld.Presentation;
using LivingWorld.Societies;
using Xunit;

namespace LivingWorld.Tests;

public sealed class ChronicleWatchRendererTests
{
    [Fact]
    public void BuildStatusLines_ShowsFrozenFocalSelectionState()
    {
        World world = new(new WorldTime(90, 1));
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.FocalSelection;
        world.PrehistoryRuntime.ActivitySummary = "Refreshing the focal list";
        world.PrehistoryRuntime.LastCheckpointOutcome = PrehistoryCheckpointOutcome.EnterFocalSelection("ready", "almost there");
        world.PlayerEntryCandidates.Add(CreateCandidate(101));

        IReadOnlyList<string> lines = ChronicleWatchRenderer.BuildStatusLines(
            world,
            polity: null,
            new WatchUiState(),
            width: 80,
            stageNameFormatter: stage => stage.ToString());

        Assert.Contains(" Chronicle Watch - Focal selection (time paused)", lines);
        Assert.Contains(" Candidate pool: 1 viable start(s) available", lines);
        Assert.Contains(" Handoff: awaiting player selection", lines);
        Assert.Contains(lines, line => line.Contains("Checkpoint: EnterFocalSelection", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildStatusLines_ShowsActivePlayHandoffSummary()
    {
        World world = new(new WorldTime(200, 1));
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.ActivePlay;
        world.PrehistoryRuntime.ActivitySummary = "Chronicle running";
        world.PrehistoryRuntime.PhaseLabel = "Active play";
        world.ActivePlayHandoff.RecordHandoff(23, world.Time.Year, 6, "Chosen start summary");
        world.BeginActiveSimulation();

        Polity polity = new(23, "Focus Polity", 5, 3, 120, stage: PolityStage.Civilization);
        world.Polities.Add(polity);

        IReadOnlyList<string> lines = ChronicleWatchRenderer.BuildStatusLines(
            world,
            polity,
            new WatchUiState(),
            width: 80,
            stageNameFormatter: stage => stage.ToString());

        Assert.Contains($" Chronicle boundary: {world.LiveChronicleStartYear}", lines);
        Assert.Contains(" Handoff summary: Chosen start summary", lines);
    }

    [Fact]
    public void BuildStatusLines_GenerationFailureIsHonest()
    {
        World world = new(new WorldTime(70, 4));
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.GenerationFailure;
        world.PrehistoryRuntime.ActivitySummary = "Unable to surface viable starts.";
        world.PrehistoryRuntime.LastCheckpointOutcome = PrehistoryCheckpointOutcome.Failure("world_failed");

        IReadOnlyList<string> lines = ChronicleWatchRenderer.BuildStatusLines(
            world,
            polity: null,
            new WatchUiState(),
            width: 80,
            stageNameFormatter: stage => stage.ToString());

        Assert.Contains(" World Generation Failure", lines);
        Assert.Contains(" Details: world_failed", lines);
        Assert.Contains(lines, line => line.Contains("honest failure", StringComparison.Ordinal));
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
            6,
            30,
            2,
            "medium",
            "Mixed",
            "Condition",
            "Settlement",
            "Regional",
            "Lineage",
            "Discovery",
            "Learned",
            "Note",
            "Opportunity",
            0.5,
            StabilityBand.Stable,
            false);
}
