using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Presentation;
using LivingWorld.Societies;
using LivingWorld.Advancement;
using Xunit;

namespace LivingWorld.Tests;

public sealed class WatchNavigationTests
{
    [Fact]
    public void KnownRegions_UsesSettlementAndNeighborVisibilityRule()
    {
        World world = CreateWorld();
        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);

        List<Region> knownRegions = WatchInspectionData.GetKnownRegions(world, focus);

        Assert.Equal(["Green Barrow", "Red Valley"], knownRegions.Select(region => region.Name).ToArray());
    }

    [Fact]
    public void KnownSpecies_ComesFromKnownRegions()
    {
        World world = CreateWorld();
        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);

        List<Species> knownSpecies = WatchInspectionData.GetKnownSpecies(world, focus);

        Assert.Equal(["Humans", "River Elk"], knownSpecies.Select(species => species.Name).ToArray());
    }

    [Fact]
    public void EnterOnKnownSpecies_OpensSpeciesDetail_AndEscapeReturnsToList()
    {
        World world = CreateWorld();
        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchUiState uiState = new();
        uiState.SetActiveMainView(WatchViewType.KnownSpecies);
        WatchInputController controller = new(uiState);

        bool inspected = controller.HandleKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false), world, focus);

        Assert.True(inspected);
        Assert.Equal(WatchViewType.SpeciesDetail, uiState.ActiveView);
        Assert.True(uiState.SelectedSpeciesId.HasValue);

        bool returned = controller.HandleKey(new ConsoleKeyInfo((char)27, ConsoleKey.Escape, false, false, false), world, focus);

        Assert.True(returned);
        Assert.Equal(WatchViewType.KnownSpecies, uiState.ActiveView);
    }

    [Fact]
    public void Space_TogglesPause_WithoutChangingView()
    {
        World world = CreateWorld();
        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchUiState uiState = new();
        uiState.SetActiveMainView(WatchViewType.WorldOverview);
        WatchInputController controller = new(uiState);

        bool handled = controller.HandleKey(new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false), world, focus);

        Assert.True(handled);
        Assert.True(uiState.IsPaused);
        Assert.Equal(WatchViewType.WorldOverview, uiState.ActiveView);
    }

    [Fact]
    public void MyPolity_Enter_KeepsViewAndDoesNotReduceVisibleSummaryData()
    {
        World world = CreateWorld();
        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchUiState uiState = new();
        uiState.SetActiveMainView(WatchViewType.MyPolity);
        WatchInputController controller = new(uiState);

        IReadOnlyList<string> beforeLines = WatchScreenBuilder.BuildBodyLines(world, focus, uiState);

        bool handled = controller.HandleKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false), world, focus);

        IReadOnlyList<string> afterLines = WatchScreenBuilder.BuildBodyLines(world, focus, uiState);

        Assert.True(handled);
        Assert.Equal(WatchViewType.MyPolity, uiState.ActiveView);
        Assert.Contains(" Name: Deepfield Tribe", beforeLines);
        Assert.Contains(" Name: Deepfield Tribe", afterLines);
        Assert.Contains(afterLines, line => line.StartsWith(" Discoveries: ", StringComparison.Ordinal));
        Assert.Contains(afterLines, line => line.StartsWith(" Learned: ", StringComparison.Ordinal));
        Assert.Contains(afterLines, line => line.StartsWith(" Food Stores: ", StringComparison.Ordinal));
        Assert.Contains(afterLines, line => line.StartsWith(" Major Pressures: ", StringComparison.Ordinal));
    }

    [Fact]
    public void MyPolity_Enter_DoesNotLoseDiscoveriesOrLearned()
    {
        World world = CreateWorld();
        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchUiState uiState = new();
        uiState.SetActiveMainView(WatchViewType.MyPolity);
        WatchInputController controller = new(uiState);

        bool handled = controller.HandleKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false), world, focus);
        IReadOnlyList<string> lines = WatchScreenBuilder.BuildBodyLines(world, focus, uiState);

        Assert.True(handled);
        Assert.Contains(" Discoveries: Green Barrow Copper", lines);
        Assert.Contains(" Learned: Agriculture, Fire", lines);
    }

    [Fact]
    public void LeftRight_PagesThroughChronicleScrollback()
    {
        World world = CreateWorld();
        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchUiState uiState = new();
        uiState.SetActiveMainView(WatchViewType.Chronicle);
        WatchInputController controller = new(uiState);

        bool handled = controller.HandleKey(new ConsoleKeyInfo((char)0, ConsoleKey.RightArrow, false, false, false), world, focus);

        Assert.True(handled);
        Assert.True(uiState.GetScrollOffset(WatchViewType.Chronicle) > 0);
    }

    [Fact]
    public void LeftRight_PagesKnownRegionSelection()
    {
        World world = CreateWideVisibilityWorld();
        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchUiState uiState = new();
        uiState.SetActiveMainView(WatchViewType.KnownRegions);
        WatchInputController controller = new(uiState);

        bool handled = controller.HandleKey(new ConsoleKeyInfo((char)0, ConsoleKey.RightArrow, false, false, false), world, focus);

        Assert.True(handled);
        Assert.True(uiState.GetSelectedIndex(WatchViewType.KnownRegions) >= 4);
    }

    [Fact]
    public void SpeciesDetail_ShowsOnlyKnownRegionalPopulations()
    {
        World world = CreateWorld();
        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchUiState uiState = new();
        uiState.PushDetailView(WatchViewType.SpeciesDetail, entityId: 2);

        IReadOnlyList<string> lines = WatchScreenBuilder.BuildBodyLines(world, focus, uiState);

        Assert.Contains(lines, line => line.Contains("Green Barrow", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Red Valley", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("Far North", StringComparison.Ordinal));
    }

    [Fact]
    public void ForeignPolityDetail_HidesDiscoveriesAndLearned()
    {
        World world = CreateWorld();
        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchUiState uiState = new();
        uiState.PushDetailView(WatchViewType.PolityDetail, entityId: 8);

        IReadOnlyList<string> lines = WatchScreenBuilder.BuildBodyLines(world, focus, uiState);

        Assert.Contains(" Discoveries: Not yet known", lines);
        Assert.Contains(" Learned: Not yet known", lines);
    }

    [Fact]
    public void WorldOverview_UsesKnownCountsRatherThanGlobalTotals()
    {
        World world = CreateWorld();
        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchUiState uiState = new();
        uiState.SetActiveMainView(WatchViewType.WorldOverview);

        IReadOnlyList<string> lines = WatchScreenBuilder.BuildBodyLines(world, focus, uiState);

        Assert.Contains(" Known Regions: 2", lines);
        Assert.Contains(" Known Species: 2", lines);
        Assert.Contains(" Known Polities: 1", lines);
        Assert.DoesNotContain(lines, line => line.Contains("Total Regions: 4", StringComparison.Ordinal));
    }

    private static World CreateWorld()
    {
        World world = new(new WorldTime(10, 6));
        Region greenBarrow = new(0, "Green Barrow") { Fertility = 0.6, WaterAvailability = 0.5 };
        Region redValley = new(1, "Red Valley") { Fertility = 0.8, WaterAvailability = 0.7 };
        Region amberReach = new(2, "Amber Reach") { Fertility = 0.4, WaterAvailability = 0.3 };
        Region farNorth = new(3, "Far North") { Fertility = 0.2, WaterAvailability = 0.1 };
        greenBarrow.AddConnection(1);
        redValley.AddConnection(0);
        redValley.AddConnection(2);
        amberReach.AddConnection(1);
        world.Regions.AddRange([greenBarrow, redValley, amberReach, farNorth]);

        Species humans = new(1, "Humans", 0.8, 0.7) { IsSapient = true };
        Species riverElk = new(2, "River Elk", 0.1, 0.3);
        Species stoneWolf = new(3, "Stone Wolf", 0.2, 0.5) { TrophicRole = TrophicRole.Predator };
        Species snowMite = new(4, "Snow Mite", 0.0, 0.1);
        world.Species.AddRange([humans, riverElk, stoneWolf, snowMite]);

        greenBarrow.GetOrCreateSpeciesPopulation(1).PopulationCount = 80;
        greenBarrow.GetOrCreateSpeciesPopulation(2).PopulationCount = 140;
        redValley.GetOrCreateSpeciesPopulation(2).PopulationCount = 220;
        amberReach.GetOrCreateSpeciesPopulation(3).PopulationCount = 60;
        farNorth.GetOrCreateSpeciesPopulation(4).PopulationCount = 500;

        Polity focalPolity = new(7, "Deepfield Tribe", 1, 0, 84);
        focalPolity.EstablishFirstSettlement(0, "Green Hearth");
        focalPolity.AddDiscovery(new CulturalDiscovery("region-copper:0", "Green Barrow Copper", CulturalDiscoveryCategory.Resource, null, 0));
        focalPolity.LearnAdvancement(AdvancementId.Fire);
        focalPolity.LearnAdvancement(AdvancementId.Agriculture);
        world.Polities.Add(focalPolity);

        Polity neighborPolity = new(8, "Valley Clan", 1, 1, 55);
        neighborPolity.EstablishFirstSettlement(1, "Valley Hold");
        neighborPolity.LearnAdvancement(AdvancementId.Fire);
        neighborPolity.AddDiscovery(new CulturalDiscovery("species-edible:2", "River Elk Edible", CulturalDiscoveryCategory.SpeciesUse, 2, 1));
        world.Polities.Add(neighborPolity);

        Polity distantPolity = new(9, "Northwatch", 1, 3, 40);
        distantPolity.EstablishFirstSettlement(3, "Northwatch Camp");
        world.Polities.Add(distantPolity);

        return world;
    }

    private static World CreateWideVisibilityWorld()
    {
        World world = new(new WorldTime(10, 6));
        for (int index = 0; index < 14; index++)
        {
            Region region = new(index, $"Region {index:D2}") { Fertility = 0.5 + (index * 0.01), WaterAvailability = 0.4 };
            if (index > 0)
            {
                region.AddConnection(index - 1);
                world.Regions[index - 1].AddConnection(index);
            }

            world.Regions.Add(region);
        }

        Species humans = new(1, "Humans", 0.8, 0.7) { IsSapient = true };
        world.Species.Add(humans);

        Polity focalPolity = new(7, "Deepfield Tribe", 1, 6, 84);
        focalPolity.EstablishFirstSettlement(6, "Green Hearth");
        focalPolity.AddDiscovery(new CulturalDiscovery("region-route:10", "Region 10 Trade Path", CulturalDiscoveryCategory.Geography, null, 10));
        world.Polities.Add(focalPolity);

        return world;
    }
}
