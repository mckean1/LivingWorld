using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Presentation;
using LivingWorld.Societies;
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
            SettlementCount = 1,
            FoodStores = 63
        };

        List<string> statusLines = ChronicleWatchRenderer.BuildStatusLines(
            world,
            polity,
            width: 60,
            stageNameFormatter: stage => stage.ToString());

        Assert.Contains(" Species: Humans", statusLines);
        Assert.Contains(" Region: Green Barrow", statusLines);
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
}
