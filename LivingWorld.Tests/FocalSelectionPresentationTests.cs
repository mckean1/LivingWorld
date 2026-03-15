using LivingWorld.Core;
using LivingWorld.Presentation;
using LivingWorld.Societies;
using Xunit;

namespace LivingWorld.Tests;

public sealed class FocalSelectionPresentationTests
{
    [Fact]
    public void WatchBodyLines_ShowPlayerFacingPresentationContract()
    {
        World world = CreateWorld(PrehistoryCheckpointOutcomeKind.EnterFocalSelection, isThinWorld: false, isWeakWorld: false, candidateCount: 2);
        ChronicleFocus focus = new();
        focus.SetFocus(world.Polities[0]);
        WatchUiState uiState = new(isPaused: true);
        uiState.SetActiveMainView(WatchViewType.FocalSelection);

        IReadOnlyList<string> lines = WatchScreenBuilder.BuildBodyLines(world, focus, uiState);

        Assert.Contains("READY STARTS", lines);
        Assert.Contains(lines, line => line.Contains("Why it qualified:", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Evidence:", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Strengths:", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Warnings:", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Risks:", StringComparison.Ordinal));
        Assert.Contains("Identity and Form", lines);
        Assert.Contains("Homeland and Movement", lines);
        Assert.Contains("Neighbors and Pressure", lines);
        Assert.Contains("Opportunity and Risk", lines);
        Assert.Contains("Why This Start Qualified", lines);
        Assert.Contains(lines, line => line.Contains("Score tier: Strong", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("0.62", StringComparison.Ordinal));
        Assert.Equal(1, lines.Count(line => line == "Identity and Form"));
    }

    [Theory]
    [InlineData(PrehistoryCheckpointOutcomeKind.EnterFocalSelection, false, false, "READY STARTS")]
    [InlineData(PrehistoryCheckpointOutcomeKind.EnterFocalSelection, true, false, "THIN STARTS")]
    [InlineData(PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection, false, false, "FORCED ENTRY")]
    [InlineData(PrehistoryCheckpointOutcomeKind.EnterFocalSelection, false, true, "WEAK WORLD")]
    public void WatchBodyLines_ShowTruthfulBannerStates(
        PrehistoryCheckpointOutcomeKind outcomeKind,
        bool isThinWorld,
        bool isWeakWorld,
        string expectedBanner)
    {
        World world = CreateWorld(outcomeKind, isThinWorld, isWeakWorld, candidateCount: 1);
        ChronicleFocus focus = new();
        focus.SetFocus(world.Polities[0]);
        WatchUiState uiState = new(isPaused: true);
        uiState.SetActiveMainView(WatchViewType.FocalSelection);

        IReadOnlyList<string> lines = WatchScreenBuilder.BuildBodyLines(world, focus, uiState);

        Assert.Contains(expectedBanner, lines);
    }

    [Fact]
    public void StartupDisplayLines_UseFocalSelectionContractForSingleCandidate()
    {
        World world = CreateWorld(PrehistoryCheckpointOutcomeKind.EnterFocalSelection, isThinWorld: false, isWeakWorld: false, candidateCount: 1);

        IReadOnlyList<string> lines = StartupProgressRenderer.BuildDisplayLines(world, includeDiagnostics: false);

        Assert.Contains("World Generation", lines);
        Assert.Contains("READY STARTS", lines);
        Assert.Contains(lines, line => line.Contains("Start 1 of 1", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("Candidates: viable", StringComparison.Ordinal));
    }

    [Fact]
    public void WatchBodyLines_PreserveSelectedCandidateSummaryState()
    {
        World world = CreateWorld(PrehistoryCheckpointOutcomeKind.EnterFocalSelection, isThinWorld: false, isWeakWorld: false, candidateCount: 2);
        ChronicleFocus focus = new();
        focus.SetFocus(world.Polities[0]);
        WatchUiState uiState = new(isPaused: true);
        uiState.SetActiveMainView(WatchViewType.FocalSelection);
        uiState.SetSelectedIndex(WatchViewType.FocalSelection, 1);

        _ = WatchScreenBuilder.BuildBodyLines(world, focus, uiState);

        Assert.Equal(2, world.FocalSelectionPresentation.CandidateCount);
        Assert.Equal(1, world.FocalSelectionPresentation.HighlightedIndex);
        Assert.Contains("Stone Chorus", world.FocalSelectionPresentation.PresentationSummary, StringComparison.Ordinal);
    }

    private static World CreateWorld(
        PrehistoryCheckpointOutcomeKind outcomeKind,
        bool isThinWorld,
        bool isWeakWorld,
        int candidateCount)
    {
        World world = new(new WorldTime(920, 1))
        {
            StartupStage = WorldStartupStage.FocalSelection
        };

        world.StartupAgeConfiguration = StartupWorldAgeConfiguration.ForPreset(StartupWorldAgePreset.StandardWorld);
        world.PrehistoryRuntime.CurrentPhase = PrehistoryRuntimePhase.FocalSelection;
        world.PrehistoryRuntime.DetailView = PrehistoryRuntimeDetailView.FocalSelection;
        world.PrehistoryRuntime.PhaseLabel = "Focal selection";
        world.PrehistoryRuntime.ActivitySummary = "Reviewing truthful candidate starts.";
        world.PrehistoryRuntime.IsPrehistoryAdvancing = false;
        world.PrehistoryRuntime.LastCheckpointOutcome = outcomeKind switch
        {
            PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection => PrehistoryCheckpointOutcome.ForceEnterFocalSelection("forced"),
            _ => PrehistoryCheckpointOutcome.EnterFocalSelection("ready")
        };

        world.WorldReadinessReport = new WorldReadinessReport(
            new WorldAgeGateReport(920, 700, 900, 1200, PrehistoryAgeGateStatus.TargetAgeReached),
            outcomeKind,
            WorldReadinessReport.Empty.CategoryResults,
            new CandidatePoolReadinessSummary(candidateCount, candidateCount, 1, candidateCount, 0, candidateCount, candidateCount, candidateCount, candidateCount, isThinWorld, "candidate summary"),
            Array.Empty<string>(),
            isThinWorld ? ["thin_world"] : Array.Empty<string>(),
            isWeakWorld,
            isThinWorld,
            new WorldReadinessSummaryData("headline", "candidate headline", "world condition", 6, isThinWorld ? 1 : 0, 0));

        world.Regions.Add(new LivingWorld.Map.Region(0, "Green Basin") { Biome = LivingWorld.Map.RegionBiome.RiverValley });
        world.Regions.Add(new LivingWorld.Map.Region(1, "Stone Shelf") { Biome = LivingWorld.Map.RegionBiome.Highlands });
        world.Species.Add(new LivingWorld.Life.Species(1, "Humans", 0.8, 0.8) { TrophicRole = LivingWorld.Life.TrophicRole.Omnivore });
        world.Species.Add(new LivingWorld.Life.Species(2, "Highland Kin", 0.8, 0.8) { TrophicRole = LivingWorld.Life.TrophicRole.Omnivore });
        world.Polities.Add(new Polity(1, "River Hearth", 1, 0, 180));
        world.Polities.Add(new Polity(2, "Stone Chorus", 2, 1, 140));

        world.PlayerEntryCandidates.Add(CreateCandidate(
            polityId: 1,
            polityName: "River Hearth",
            speciesName: "Humans",
            regionName: "Green Basin",
            scoreTier: CandidateScoreTier.Strong,
            currentCondition: "Recovering",
            qualificationReason: "Solid anchored start with clear internal shape."));

        if (candidateCount > 1)
        {
            world.PlayerEntryCandidates.Add(CreateCandidate(
                polityId: 2,
                polityName: "Stone Chorus",
                speciesName: "Highland Kin",
                regionName: "Stone Shelf",
                scoreTier: CandidateScoreTier.Promising,
                currentCondition: "Frontier strain",
                qualificationReason: "Viable settling start with usable early agency."));
        }

        return world;
    }

    private static PlayerEntryCandidateSummary CreateCandidate(
        int polityId,
        string polityName,
        string speciesName,
        string regionName,
        CandidateScoreTier scoreTier,
        string currentCondition,
        string qualificationReason)
        => new(
            polityId,
            polityName,
            polityId,
            speciesName,
            polityId,
            polityId - 1,
            regionName,
            18,
            920,
            3,
            "Established",
            "Mixed hunter-forager",
            currentCondition,
            "growing settlement web",
            "river valley with usable local support",
            "deep descendant branch; mixed adaptation",
            "River crossings, nearby game",
            "Seasonal Planning, Food Storage",
            "recently consolidated into a polity",
            "exchange edges are already visible",
            0.62,
            StabilityBand.Stable,
            false,
            false,
            string.Empty,
            new CandidateViabilityResult(
                true,
                true,
                [new CandidateViabilityGate("support", true, "Current support must pass", "Support is stable.")],
                Array.Empty<string>(),
                ["thin_local_support"],
                string.Empty,
                "Meets the hard truth floor and normal-entry durability gates."),
            CandidateMaturityBand.Anchored,
            "rooted coherence",
            "Anchored mixed hunter-forager in river valley",
            qualificationReason,
            $"{polityName} is an anchored mixed hunter-forager start on river valley ground, with stable support and deep continuity.",
            ["Reliable current support", "Established continuity"],
            ["Thin durability for a normal stop"],
            ["Recent shock may still cascade"],
            new CandidateScoreBreakdown(0.71, 0.72, 0.69, 0.64, 0.41, 0.56, 0.18, 0.62, scoreTier, "Runs on continuity depth, with no major drag beyond external entanglement."),
            ["maturity:anchored", "home:river_valley"],
            "dup-key");
}
