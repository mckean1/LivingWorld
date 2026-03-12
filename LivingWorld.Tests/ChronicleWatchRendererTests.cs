using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Presentation;
using LivingWorld.Societies;
using LivingWorld.Advancement;
using Xunit;

namespace LivingWorld.Tests;

public sealed class ChronicleWatchRendererTests
{
    [Fact]
    public void StatusPanel_IncludesSpeciesForFocusedPolity()
    {
        World world = new(new WorldTime(12, 1));
        world.Regions.Add(new Region(0, "Green Barrow"));
        world.Species.Add(new Species(1, "Humans", 0.8, 0.7));

        Polity polity = new(7, "Deepfield Tribe", 1, 0, 84)
        {
            FoodStores = 63
        };
        polity.EstablishFirstSettlement(0, "Green Barrow Hearth");

        List<string> statusLines = ChronicleWatchRenderer.BuildStatusLines(
            world,
            polity,
            width: 60,
            stageNameFormatter: stage => stage.ToString());

        Assert.Contains(" Species: Humans", statusLines);
        Assert.Contains(" Region: Green Barrow", statusLines);
        Assert.Contains(" Discoveries: None yet", statusLines);
        Assert.Contains(" Learned: None yet", statusLines);
    }

    [Fact]
    public void StatusPanel_FallsBackToUnknown_WhenSpeciesIsMissing()
    {
        World world = new(new WorldTime(12, 1));
        world.Regions.Add(new Region(0, "Green Barrow"));

        Polity polity = new(7, "Deepfield Tribe", 99, 0, 84);

        List<string> statusLines = ChronicleWatchRenderer.BuildStatusLines(
            world,
            polity,
            width: 60,
            stageNameFormatter: stage => stage.ToString());

        Assert.Contains(" Species: Unknown", statusLines);
    }

    [Fact]
    public void StatusPanel_RendersAdvancementOnlyPolity_Correctly()
    {
        World world = new(new WorldTime(12, 1));
        world.Regions.Add(new Region(0, "Green Barrow"));
        world.Species.Add(new Species(1, "Humans", 0.8, 0.7));

        Polity polity = new(7, "Deepfield Tribe", 1, 0, 84);
        polity.LearnAdvancement(AdvancementId.Fire);

        List<string> statusLines = ChronicleWatchRenderer.BuildStatusLines(world, polity, 60, stage => stage.ToString());

        Assert.Contains(" Discoveries: None yet", statusLines);
        Assert.Contains(" Learned: Fire", statusLines);
        Assert.DoesNotContain(statusLines, line => line.Contains("No major discoveries yet", StringComparison.Ordinal));
    }

    [Fact]
    public void StatusPanel_RendersDiscoveryOnlyPolity_Correctly()
    {
        World world = new(new WorldTime(12, 1));
        world.Regions.Add(new Region(0, "Green Barrow"));
        world.Species.Add(new Species(1, "Humans", 0.8, 0.7));

        Polity polity = new(7, "Deepfield Tribe", 1, 0, 84);
        polity.AddDiscovery(new CulturalDiscovery("species-edible:4", "River Elk Edible", CulturalDiscoveryCategory.SpeciesUse, 4, 0));

        List<string> statusLines = ChronicleWatchRenderer.BuildStatusLines(world, polity, 60, stage => stage.ToString());

        Assert.Contains(" Discoveries: River Elk Edible", statusLines);
        Assert.Contains(" Learned: None yet", statusLines);
    }

    [Fact]
    public void StatusPanel_RendersMixedKnowledgeCompactly()
    {
        World world = new(new WorldTime(12, 1));
        world.Regions.Add(new Region(0, "Green Barrow"));
        world.Species.Add(new Species(1, "Humans", 0.8, 0.7));

        Polity polity = new(7, "Deepfield Tribe", 1, 0, 84);
        polity.AddDiscovery(new CulturalDiscovery("species-edible:4", "River Elk Edible", CulturalDiscoveryCategory.SpeciesUse, 4, 0));
        polity.AddDiscovery(new CulturalDiscovery("species-toxic:5", "Redcap Mushrooms Toxic", CulturalDiscoveryCategory.FoodSafety, 5, 0));
        polity.AddDiscovery(new CulturalDiscovery("region-copper:0", "Green Barrow Copper", CulturalDiscoveryCategory.Resource, null, 0));
        polity.LearnAdvancement(AdvancementId.Fire);
        polity.LearnAdvancement(AdvancementId.SeasonalPlanning);
        polity.LearnAdvancement(AdvancementId.Agriculture);

        List<string> statusLines = ChronicleWatchRenderer.BuildStatusLines(world, polity, 80, stage => stage.ToString());

        Assert.Contains(statusLines, line => line.StartsWith(" Discoveries: ", StringComparison.Ordinal) && line.Contains("+1 more", StringComparison.Ordinal));
        Assert.Contains(statusLines, line => line.StartsWith(" Learned: ", StringComparison.Ordinal) && line.Contains("+1 more", StringComparison.Ordinal));
    }

    [Fact]
    public void StatusPanel_ShowsPauseStateAndActiveView()
    {
        World world = new(new WorldTime(12, 1));
        world.Regions.Add(new Region(0, "Green Barrow"));
        world.Species.Add(new Species(1, "Humans", 0.8, 0.7));
        Polity polity = new(7, "Deepfield Tribe", 1, 0, 84);

        WatchUiState uiState = new();
        uiState.TogglePaused();
        uiState.SetActiveMainView(WatchViewType.KnownSpecies);

        List<string> statusLines = ChronicleWatchRenderer.BuildStatusLines(
            world,
            polity,
            uiState,
            width: 80,
            stageNameFormatter: stage => stage.ToString());

        Assert.Contains(" Status: PAUSED | View: Known Species", statusLines);
    }

    [Fact]
    public void Record_KeepsNewestChronicleEntryFirst()
    {
        World world = new(new WorldTime(12, 1));
        world.Regions.Add(new Region(0, "Green Barrow"));
        world.Species.Add(new Species(1, "Humans", 0.8, 0.7));
        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        world.Polities.Add(new Polity(7, "Deepfield Tribe", 1, 0, 84));
        ChronicleWatchRenderer renderer = new(
            new SimulationOptions { OutputMode = OutputMode.Watch },
            new ChronicleColorWriter(),
            new ChronicleEventFormatter());
        WatchUiState uiState = new();

        renderer.Record(world, focus, uiState, new WorldEvent
        {
            Year = 4,
            Type = WorldEventType.Migration,
            Severity = WorldEventSeverity.Major,
            Narrative = "Older turning point",
            PolityId = 7
        });
        renderer.Record(world, focus, uiState, new WorldEvent
        {
            Year = 5,
            Type = WorldEventType.LearnedAdvancement,
            Severity = WorldEventSeverity.Major,
            Narrative = "Newer turning point",
            PolityId = 7
        });

        IReadOnlyList<string> entries = renderer.SnapshotChronicleEntries();

        Assert.Equal("Year 5 - Newer turning point.", entries[0]);
        Assert.Equal("Year 4 - Older turning point.", entries[1]);
    }
}
