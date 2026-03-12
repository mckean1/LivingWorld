using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Presentation;
using LivingWorld.Societies;
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

        greenBarrow.SpeciesPopulations.Add(new RegionSpeciesPopulation(1, 0, 80));
        greenBarrow.SpeciesPopulations.Add(new RegionSpeciesPopulation(2, 0, 140));
        redValley.SpeciesPopulations.Add(new RegionSpeciesPopulation(2, 1, 220));
        amberReach.SpeciesPopulations.Add(new RegionSpeciesPopulation(3, 2, 60));
        farNorth.SpeciesPopulations.Add(new RegionSpeciesPopulation(4, 3, 500));

        Polity focalPolity = new(7, "Deepfield Tribe", 1, 0, 84);
        focalPolity.EstablishFirstSettlement(0, "Green Hearth");
        world.Polities.Add(focalPolity);

        Polity neighborPolity = new(8, "Valley Clan", 1, 1, 55);
        neighborPolity.EstablishFirstSettlement(1, "Valley Hold");
        world.Polities.Add(neighborPolity);

        Polity distantPolity = new(9, "Northwatch", 1, 3, 40);
        distantPolity.EstablishFirstSettlement(3, "Northwatch Camp");
        world.Polities.Add(distantPolity);

        return world;
    }
}
