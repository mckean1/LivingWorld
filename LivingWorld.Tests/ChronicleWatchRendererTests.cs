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
}
