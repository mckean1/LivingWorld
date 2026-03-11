using LivingWorld.Core;
using LivingWorld.Map;
using LivingWorld.Presentation;
using LivingWorld.Societies;
using Xunit;

namespace LivingWorld.Tests;

public sealed class LineagePolityFocusSelectorTests
{
    [Fact]
    public void ResolveYearEndFocus_KeepsCurrentFocus_WhenPolitySurvivesWithoutFragmenting()
    {
        World world = BuildWorld(month: 12);
        Polity focused = AddPolity(world, 1, "River Clan", 1, 0, 140, stage: PolityStage.Tribe, settlements: 1);
        AddPolity(world, 2, "Hill Tribe", 2, 1, 95, stage: PolityStage.Tribe, settlements: 1);

        ChronicleFocus focus = new();
        focus.SetFocus(focused);
        LineagePolityFocusSelector selector = new();

        ChronicleFocusTransition? transition = selector.ResolveYearEndFocus(world, focus, []);

        Assert.Null(transition);
    }

    [Fact]
    public void ResolveYearEndFocus_SelectsStrongestDirectChild_WhenFocusedPolityFragments()
    {
        World world = BuildWorld(month: 12);
        Polity focused = AddPolity(world, 1, "River Clan", 1, 0, 160, stage: PolityStage.Tribe, settlements: 1);
        Polity weakerChild = AddPolity(world, 10, "Marsh Clan", 1, 1, 60, lineageId: 1, parentPolityId: 1, stage: PolityStage.Tribe, settlements: 1);
        Polity strongerChild = AddPolity(world, 11, "Valley Clan", 1, 0, 95, lineageId: 1, parentPolityId: 1, stage: PolityStage.SettledSociety, settlements: 2);

        world.AddEvent(
            WorldEventType.Fragmentation,
            WorldEventSeverity.Major,
            "River Clan fractured",
            polityId: focused.Id,
            polityName: focused.Name,
            relatedPolityId: weakerChild.Id,
            relatedPolityName: weakerChild.Name);
        world.AddEvent(
            WorldEventType.Fragmentation,
            WorldEventSeverity.Major,
            "River Clan fractured",
            polityId: focused.Id,
            polityName: focused.Name,
            relatedPolityId: strongerChild.Id,
            relatedPolityName: strongerChild.Name);

        ChronicleFocus focus = new();
        focus.SetFocus(focused);
        LineagePolityFocusSelector selector = new();

        ChronicleFocusTransition? transition = selector.ResolveYearEndFocus(world, focus, world.Events);

        Assert.NotNull(transition);
        Assert.Equal(ChronicleFocusTransitionKind.Fragmentation, transition!.Kind);
        Assert.Equal(strongerChild.Id, transition.NewPolityId);
    }

    [Fact]
    public void ResolveYearEndFocus_SelectsBestSameLineageSuccessor_AfterCollapse()
    {
        World world = BuildWorld(month: 12);
        AddPolity(world, 1, "River Clan", 1, 0, 0, stage: PolityStage.Tribe, settlements: 1);
        Polity cousin = AddPolity(world, 4, "Hill Tribe", 1, 1, 72, lineageId: 1, parentPolityId: 1, stage: PolityStage.Tribe, settlements: 1);
        Polity heir = AddPolity(world, 5, "Valley Clan", 1, 0, 110, lineageId: 1, parentPolityId: 1, stage: PolityStage.SettledSociety, settlements: 2);

        world.AddEvent(
            WorldEventType.PolityCollapsed,
            WorldEventSeverity.Major,
            "River Clan collapsed",
            polityId: 1,
            polityName: "River Clan",
            regionId: 0,
            metadata: new Dictionary<string, string>
            {
                ["speciesId"] = "1"
            });

        ChronicleFocus focus = new();
        focus.SetFocus(1, 1);
        LineagePolityFocusSelector selector = new();

        ChronicleFocusTransition? transition = selector.ResolveYearEndFocus(world, focus, world.Events);

        Assert.NotNull(transition);
        Assert.Equal(ChronicleFocusTransitionKind.Collapse, transition!.Kind);
        Assert.Equal(heir.Id, transition.NewPolityId);
    }

    [Fact]
    public void ResolveYearEndFocus_FallsBackOutsideLineage_WhenLineageIsExtinct()
    {
        World world = BuildWorld(month: 12);
        AddPolity(world, 1, "River Clan", 1, 0, 0, stage: PolityStage.Tribe, settlements: 1);
        Polity nearby = AddPolity(world, 6, "Stone Clan", 1, 0, 85, lineageId: 6, stage: PolityStage.Tribe, settlements: 1);
        AddPolity(world, 7, "Far Kingdom", 2, 2, 90, lineageId: 7, stage: PolityStage.Civilization, settlements: 3);

        world.AddEvent(
            WorldEventType.PolityCollapsed,
            WorldEventSeverity.Major,
            "River Clan collapsed",
            polityId: 1,
            polityName: "River Clan",
            regionId: 0,
            metadata: new Dictionary<string, string>
            {
                ["speciesId"] = "1"
            });

        ChronicleFocus focus = new();
        focus.SetFocus(1, 1);
        LineagePolityFocusSelector selector = new();

        ChronicleFocusTransition? transition = selector.ResolveYearEndFocus(world, focus, world.Events);

        Assert.NotNull(transition);
        Assert.Equal(ChronicleFocusTransitionKind.LineageExtinctionFallback, transition!.Kind);
        Assert.Equal(nearby.Id, transition.NewPolityId);
    }

    [Fact]
    public void Simulation_WritesFocusTransitionToStructuredHistory()
    {
        string historyPath = Path.Combine(Path.GetTempPath(), $"livingworld-focus-{Guid.NewGuid():N}.jsonl");

        try
        {
            World world = BuildWorld(month: 12);
            AddPolity(world, 1, "River Clan", 1, 0, 0, stage: PolityStage.Tribe, settlements: 1);
            AddPolity(world, 5, "Valley Clan", 1, 0, 110, lineageId: 1, parentPolityId: 1, stage: PolityStage.SettledSociety, settlements: 2);

            world.AddEvent(
                WorldEventType.PolityCollapsed,
                WorldEventSeverity.Major,
                "River Clan collapsed",
                polityId: 1,
                polityName: "River Clan",
                regionId: 0,
                metadata: new Dictionary<string, string>
                {
                    ["speciesId"] = "1"
                });

            SimulationOptions options = new()
            {
                OutputMode = OutputMode.Debug,
                FocusedChronicleEnabled = true,
                FocusedPolityId = 1,
                WriteStructuredHistory = true,
                HistoryFilePath = historyPath
            };

            using (Simulation simulation = new(world, options))
            {
                simulation.RunMonths(1);
            }

            string[] lines = File.ReadAllLines(historyPath);

            Assert.Contains(lines, line => line.Contains("\"type\":\"focus_handoff_collapse\"", StringComparison.Ordinal));
            Assert.Contains(lines, line => line.Contains("Its legacy continued through Valley Clan", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(historyPath))
            {
                File.Delete(historyPath);
            }
        }
    }

    private static World BuildWorld(int month)
    {
        World world = new(new WorldTime(startingYear: 12, startingMonth: month));
        world.Species.Add(new LivingWorld.Life.Species(1, "People", 0.6, 0.6));
        world.Species.Add(new LivingWorld.Life.Species(2, "Others", 0.6, 0.6));

        Region coast = new(0, "Coast") { Fertility = 0.7, WaterAvailability = 0.7, PlantBiomass = 500, AnimalBiomass = 200, MaxPlantBiomass = 1000, MaxAnimalBiomass = 400 };
        Region valley = new(1, "Valley") { Fertility = 0.7, WaterAvailability = 0.7, PlantBiomass = 500, AnimalBiomass = 200, MaxPlantBiomass = 1000, MaxAnimalBiomass = 400 };
        Region uplands = new(2, "Uplands") { Fertility = 0.7, WaterAvailability = 0.7, PlantBiomass = 500, AnimalBiomass = 200, MaxPlantBiomass = 1000, MaxAnimalBiomass = 400 };

        coast.AddConnection(valley.Id);
        valley.AddConnection(coast.Id);
        valley.AddConnection(uplands.Id);
        uplands.AddConnection(valley.Id);

        world.Regions.Add(coast);
        world.Regions.Add(valley);
        world.Regions.Add(uplands);
        return world;
    }

    private static Polity AddPolity(
        World world,
        int id,
        string name,
        int speciesId,
        int regionId,
        int population,
        int? lineageId = null,
        int? parentPolityId = null,
        PolityStage stage = PolityStage.Band,
        int settlements = 0)
    {
        Polity polity = new(id, name, speciesId, regionId, population, lineageId, parentPolityId, stage)
        {
            SettlementCount = settlements,
            SettlementStatus = settlements > 0 ? SettlementStatus.Settled : SettlementStatus.Nomadic
        };

        world.Polities.Add(polity);
        return polity;
    }
}
