using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Presentation;
using LivingWorld.Societies;
using Xunit;

namespace LivingWorld.Tests;

public sealed class SimulationBootstrapTests
{
    [Fact]
    public void BootstrapEconomyBaseline_IsRecordedInternally_WithoutVisibleStartupChronicleDump()
    {
        World world = CreateBootstrapWorld();

        using Simulation simulation = new(world, new SimulationOptions());

        Assert.Equal(WorldSimulationPhase.Active, world.SimulationPhase);
        Assert.Contains(world.Events, evt =>
            evt.IsBootstrapEvent
            && (evt.Type == WorldEventType.MaterialHighlyValued
                || evt.Type == WorldEventType.TradeGoodEstablished
                || evt.Type == WorldEventType.SettlementSpecialized));

        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        WatchKnowledgeSnapshot snapshot = WatchInspectionData.CreateSnapshot(world, focus);

        Assert.DoesNotContain(snapshot.GetVisibleMajorEvents(world, limit: 20), evt => evt.IsBootstrapEvent);
    }

    [Fact]
    public void LiveYearZeroEvents_AfterBootstrapRemainEligibleForChronicle()
    {
        World world = CreateBootstrapWorld();

        using Simulation simulation = new(world, new SimulationOptions());

        ChronicleFocus focus = new();
        focus.SetFocus(polityId: 7, lineageId: 7);
        ChronicleEventFormatter formatter = new();

        world.AddEvent(
            WorldEventType.MaterialHighlyValued,
            WorldEventSeverity.Major,
            "Simple Tools became highly valued in Green Hearth",
            reason: "material_became_highly_valued",
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
                ["materialType"] = "SimpleTools"
            });

        WorldEvent liveEvent = Assert.Single(world.Events, evt =>
            evt.Type == WorldEventType.MaterialHighlyValued
            && !evt.IsBootstrapEvent
            && evt.Year == 0);

        Assert.True(formatter.TryFormat(liveEvent, focus, out string chronicleLine));
        Assert.Equal("Year 0 - Simple Tools became highly valued in Green Hearth.", chronicleLine);
    }

    private static World CreateBootstrapWorld()
    {
        World world = new(new WorldTime(0, 1));
        world.Regions.Add(new Region(0, "Green Barrow")
        {
            Fertility = 0.82,
            WaterAvailability = 0.76,
            WoodAbundance = 0.94,
            StoneAbundance = 0.70,
            ClayAbundance = 0.96,
            FiberAbundance = 0.84,
            SaltAbundance = 0.88,
            CopperOreAbundance = 0.24,
            IronOreAbundance = 0.12
        });
        world.Species.Add(new Species(1, "Humans", 0.8, 0.7) { IsSapient = true });

        Polity polity = new(7, "Deepfield Tribe", 1, 0, 120, lineageId: 7)
        {
            FoodStores = 180,
            FoodNeededThisMonth = 30
        };
        Settlement settlement = polity.EstablishFirstSettlement(0, "Green Hearth");
        settlement.YearsEstablished = 8;
        settlement.FoodRequired = 30;
        polity.LearnAdvancement(AdvancementId.Fire);
        polity.LearnAdvancement(AdvancementId.StoneTools);
        polity.LearnAdvancement(AdvancementId.SeasonalPlanning);
        polity.LearnAdvancement(AdvancementId.FoodStorage);
        polity.LearnAdvancement(AdvancementId.BasicConstruction);
        polity.LearnAdvancement(AdvancementId.CraftSpecialization);
        polity.LearnAdvancement(AdvancementId.Agriculture);
        world.Polities.Add(polity);

        return world;
    }
}
