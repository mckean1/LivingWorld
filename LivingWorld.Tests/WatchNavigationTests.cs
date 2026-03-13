using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Presentation;
using LivingWorld.Societies;
using LivingWorld.Advancement;
using LivingWorld.Economy;
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
    public void MyPolity_ShowsSettlementFoodStateBalanceAndAid()
    {
        World world = CreateWorld();
        Polity polity = world.Polities.First(candidate => candidate.Id == 7);
        Settlement settlement = polity.Settlements[0];
        settlement.FoodProduced = 20;
        settlement.FoodStored = 5;
        settlement.FoodRequired = 40;
        settlement.AidReceivedThisYear = 12;
        settlement.CalculateFoodState();

        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchUiState uiState = new();
        uiState.SetActiveMainView(WatchViewType.MyPolity);

        IReadOnlyList<string> lines = WatchScreenBuilder.BuildBodyLines(world, focus, uiState);

        Assert.Contains(lines, line => line.Contains("food Deficit (-15.0)", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("aid ytd 12.0", StringComparison.Ordinal));
    }

    [Fact]
    public void MyPolity_ShowsManagedFoodSummary()
    {
        World world = CreateWorld();
        Polity polity = world.Polities.First(candidate => candidate.Id == 7);
        Settlement settlement = polity.Settlements[0];
        polity.AnnualFoodManaged = 18;
        settlement.ManagedHerds.Add(new ManagedHerd(2, "Domestic River Elk", 9, 1, 6, 0.72, 1.0, 0.84, 0.18));
        settlement.CultivatedCrops.Add(new CultivatedCrop(2, "River grain", 9, 1, 0.12, 0.08, 0.06));

        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchUiState uiState = new();
        uiState.SetActiveMainView(WatchViewType.MyPolity);

        IReadOnlyList<string> lines = WatchScreenBuilder.BuildBodyLines(world, focus, uiState);

        Assert.Contains(" Managed Food This Year: 18", lines);
        Assert.Contains(" Managed Sources: Herds 1 | Crops 1", lines);
    }

    [Fact]
    public void MyPolity_ShowsMaterialEconomySummary()
    {
        World world = CreateWorld();
        Polity polity = world.Polities.First(candidate => candidate.Id == 7);
        Settlement settlement = polity.Settlements[0];
        settlement.MaterialPressureStates[MaterialType.Wood] = MaterialPressureState.Surplus;
        settlement.MaterialPressureStates[MaterialType.SimpleTools] = MaterialPressureState.Deficit;
        settlement.MaterialProducedThisYear[MaterialType.Pottery] = 12;
        settlement.AddMaterial(MaterialType.PreservedFood, 7);

        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchUiState uiState = new();
        uiState.SetActiveMainView(WatchViewType.MyPolity);

        IReadOnlyList<string> lines = WatchScreenBuilder.BuildBodyLines(world, focus, uiState);

        Assert.Contains(lines, line => line.StartsWith(" Material Surpluses: ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith(" Critical Shortages: ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith(" Leading Production: ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith(" Tool / Storage / Preservation: ", StringComparison.Ordinal));
    }

    [Fact]
    public void MyPolity_ShowsEconomySignalLabels()
    {
        World world = CreateWorld();
        Polity polity = world.Polities.First(candidate => candidate.Id == 7);
        Settlement settlement = polity.Settlements[0];
        settlement.MaterialPressureStates[MaterialType.Pottery] = MaterialPressureState.Deficit;
        settlement.HighlyValuedMaterials.Add(MaterialType.Pottery);
        settlement.TradeGoodMaterials.Add(MaterialType.Textiles);
        settlement.LocallyCommonMaterials.Add(MaterialType.Wood);
        settlement.DominantProductionFocusMaterial = MaterialType.Pottery;

        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchUiState uiState = new();
        uiState.SetActiveMainView(WatchViewType.MyPolity);

        IReadOnlyList<string> lines = WatchScreenBuilder.BuildBodyLines(world, focus, uiState);

        Assert.Contains(lines, line => line.Contains("Economy Needs: ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Trade Goods: ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Locally Common: ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Highly Valued", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Trade Good", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Locally Common", StringComparison.Ordinal));
    }

    [Fact]
    public void RegionDetail_ShowsExtractableResources_AndLocalProduction()
    {
        World world = CreateWorld();
        Region region = world.Regions.First(candidate => candidate.Id == 0);
        region.WoodAbundance = 0.90;
        region.ClayAbundance = 0.82;
        Polity polity = world.Polities.First(candidate => candidate.Id == 7);
        polity.Settlements[0].SpecializationTags.Add(SettlementSpecializationTag.PotteryTradition);

        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchUiState uiState = new();
        uiState.PushDetailView(WatchViewType.RegionDetail, entityId: 0);

        IReadOnlyList<string> lines = WatchScreenBuilder.BuildBodyLines(world, focus, uiState);

        Assert.Contains(lines, line => line.StartsWith(" Extractable Resources: ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith(" Local Production: ", StringComparison.Ordinal));
    }

    [Fact]
    public void SpeciesDetail_ShowsCultivationAndManagementStatus()
    {
        World world = CreateWorld();
        Polity polity = world.Polities.First(candidate => candidate.Id == 7);
        Settlement settlement = polity.Settlements[0];
        polity.AddDiscovery(new CulturalDiscovery("species-domestication-candidate:2", "River Elk Manageable", CulturalDiscoveryCategory.AnimalBehavior, 2, 0));
        settlement.ManagedHerds.Add(new ManagedHerd(2, "Domestic River Elk", 9, 1, 6, 0.72, 1.0, 0.84, 0.18));

        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchUiState uiState = new();
        uiState.PushDetailView(WatchViewType.SpeciesDetail, entityId: 2);

        IReadOnlyList<string> lines = WatchScreenBuilder.BuildBodyLines(world, focus, uiState);

        Assert.Contains(lines, line => line.Contains("Domestication Status: managed herd", StringComparison.Ordinal));
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

    [Fact]
    public void VisibleMajorEvents_DedupesDuplicateSettlementRecoveryLines()
    {
        World world = CreateWorld();
        world.AddEvent(
            WorldEventType.FamineRelief,
            WorldEventSeverity.Major,
            "Gloam Fen Hearth recovered from starvation",
            reason: "settlement_starvation_recovered",
            scope: WorldEventScope.Local,
            polityId: 7,
            polityName: "Deepfield Tribe",
            speciesId: 1,
            speciesName: "Humans",
            regionId: 0,
            regionName: "Green Barrow",
            settlementId: 7001,
            settlementName: "Gloam Fen Hearth");
        world.AddEvent(
            WorldEventType.FamineRelief,
            WorldEventSeverity.Major,
            "Gloam Fen Hearth recovered from starvation",
            reason: "settlement_starvation_recovered",
            scope: WorldEventScope.Local,
            polityId: 7,
            polityName: "Deepfield Tribe",
            speciesId: 1,
            speciesName: "Humans",
            regionId: 0,
            regionName: "Green Barrow",
            settlementId: 7001,
            settlementName: "Gloam Fen Hearth");

        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchKnowledgeSnapshot snapshot = WatchInspectionData.CreateSnapshot(world, focus);

        IReadOnlyList<WorldEvent> visibleEvents = snapshot.GetVisibleMajorEvents(world, limit: 10);

        Assert.Equal(1, visibleEvents.Count(evt => evt.Narrative.Contains("Gloam Fen Hearth recovered from starvation", StringComparison.Ordinal)));
    }

    [Fact]
    public void VisibleMajorEvents_KeepDistinctSameYearEvents()
    {
        World world = CreateWorld();
        world.AddEvent(
            WorldEventType.FamineRelief,
            WorldEventSeverity.Major,
            "Gloam Fen Hearth recovered from starvation",
            reason: "settlement_starvation_recovered",
            scope: WorldEventScope.Local,
            polityId: 7,
            polityName: "Deepfield Tribe",
            speciesId: 1,
            speciesName: "Humans",
            regionId: 0,
            regionName: "Green Barrow",
            settlementId: 7001,
            settlementName: "Gloam Fen Hearth");
        world.AddEvent(
            WorldEventType.MaterialCrisisResolved,
            WorldEventSeverity.Major,
            "Gloam Fen Hearth recovered from a broader material crisis",
            reason: "grouped_material_crisis_resolved",
            scope: WorldEventScope.Local,
            polityId: 7,
            polityName: "Deepfield Tribe",
            speciesId: 1,
            speciesName: "Humans",
            regionId: 0,
            regionName: "Green Barrow",
            settlementId: 7001,
            settlementName: "Gloam Fen Hearth",
            metadata: new Dictionary<string, string>
            {
                ["groupedMaterials"] = "Pottery,SimpleTools",
                ["groupedCount"] = "2"
            });

        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchKnowledgeSnapshot snapshot = WatchInspectionData.CreateSnapshot(world, focus);

        IReadOnlyList<WorldEvent> visibleEvents = snapshot.GetVisibleMajorEvents(world, limit: 10);

        Assert.Contains(visibleEvents, evt => evt.Narrative.Contains("Gloam Fen Hearth recovered from starvation", StringComparison.Ordinal));
        Assert.Contains(visibleEvents, evt => evt.Narrative.Contains("Gloam Fen Hearth recovered from a broader material crisis", StringComparison.Ordinal));
    }

    [Fact]
    public void VisibleMajorEvents_DedupesDuplicateTradeGoodTurns()
    {
        World world = CreateWorld();
        for (int index = 0; index < 2; index++)
        {
            world.AddEvent(
                WorldEventType.TradeGoodEstablished,
                WorldEventSeverity.Major,
                "Green Hearth became known for pottery as a trade good",
                reason: "trade_good_established",
                scope: WorldEventScope.Local,
                polityId: 7,
                polityName: "Deepfield Tribe",
                speciesId: 1,
                speciesName: "Humans",
                regionId: 0,
                regionName: "Green Barrow",
                settlementId: 7001,
                settlementName: "Green Hearth",
                metadata: new Dictionary<string, string>
                {
                    ["materialType"] = "Pottery"
                });
        }

        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchKnowledgeSnapshot snapshot = WatchInspectionData.CreateSnapshot(world, focus);

        IReadOnlyList<WorldEvent> visibleEvents = snapshot.GetVisibleMajorEvents(world, limit: 10);

        Assert.Equal(1, visibleEvents.Count(evt => evt.Type == WorldEventType.TradeGoodEstablished));
    }

    [Fact]
    public void VisibleMajorEvents_SkipBootstrapEntries_ButKeepLiveYearZeroEvents()
    {
        World world = CreateWorld(startingYear: 0);
        world.AddEvent(new WorldEvent
        {
            Type = WorldEventType.TradeGoodEstablished,
            Severity = WorldEventSeverity.Major,
            Narrative = "Green Hearth became known for pottery as a trade good",
            Reason = "trade_good_established",
            Scope = WorldEventScope.Local,
            SimulationPhase = WorldSimulationPhase.Bootstrap,
            PolityId = 7,
            PolityName = "Deepfield Tribe",
            SpeciesId = 1,
            SpeciesName = "Humans",
            RegionId = 0,
            RegionName = "Green Barrow",
            SettlementId = 7001,
            SettlementName = "Green Hearth",
            Metadata = new Dictionary<string, string>
            {
                ["materialType"] = "Pottery"
            }
        });
        world.AddEvent(
            WorldEventType.TradeGoodEstablished,
            WorldEventSeverity.Major,
            "Green Hearth became known for pottery as a trade good",
            reason: "trade_good_established",
            scope: WorldEventScope.Local,
            polityId: 7,
            polityName: "Deepfield Tribe",
            speciesId: 1,
            speciesName: "Humans",
            regionId: 0,
            regionName: "Green Barrow",
            settlementId: 7001,
            settlementName: "Green Hearth",
            metadata: new Dictionary<string, string>
            {
                ["materialType"] = "Pottery"
            });

        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchKnowledgeSnapshot snapshot = WatchInspectionData.CreateSnapshot(world, focus);

        IReadOnlyList<WorldEvent> visibleEvents = snapshot.GetVisibleMajorEvents(world, limit: 10);

        Assert.Single(visibleEvents);
        Assert.False(visibleEvents[0].IsBootstrapEvent);
        Assert.Equal(0, visibleEvents[0].Year);
    }

    private static World CreateWorld(int startingYear = 10)
    {
        World world = new(new WorldTime(startingYear, 6));
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
