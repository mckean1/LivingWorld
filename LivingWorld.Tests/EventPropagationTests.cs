using LivingWorld.Core;
using LivingWorld.Presentation;
using LivingWorld.Societies;
using Xunit;

namespace LivingWorld.Tests;

public sealed class EventPropagationTests
{
    [Fact]
    public void PropagatedEvents_PreserveParentAndRootIdsInStructuredHistory()
    {
        string historyPath = Path.Combine(Path.GetTempPath(), $"livingworld-propagation-{Guid.NewGuid():N}.jsonl");

        try
        {
            World world = CreateWorld();
            world.Polities.Add(new Polity(7, "River Clan", 1, 0, 80)
            {
                SettlementStatus = SettlementStatus.SemiSettled,
                SettlementCount = 1,
                FoodStores = 10
            });

            SimulationOptions options = new()
            {
                OutputMode = OutputMode.Debug,
                WriteStructuredHistory = true,
                HistoryFilePath = historyPath
            };

            using (Simulation simulation = new(world, options))
            {
                world.AddEvent(
                    WorldEventType.FoodStress,
                    WorldEventSeverity.Major,
                    "River Clan entered a period of shortages",
                    reason: "hardship_entered",
                    scope: WorldEventScope.Polity,
                    polityId: 7,
                    polityName: "River Clan",
                    regionId: 0,
                    regionName: "Coast",
                    after: new Dictionary<string, string>
                    {
                        ["hardshipTier"] = "Shortages"
                    },
                    metadata: new Dictionary<string, string>
                    {
                        ["transitionKind"] = "hardship_entered"
                    });
            }

            string[] lines = File.ReadAllLines(historyPath);

            Assert.Contains(lines, line => line.Contains("\"type\":\"food_stress\"", StringComparison.Ordinal));
            Assert.Contains(lines, line => line.Contains("\"type\":\"migration_pressure\"", StringComparison.Ordinal));
            Assert.Contains(
                lines,
                line => line.Contains("\"type\":\"migration_pressure\"", StringComparison.Ordinal)
                    && line.Contains("\"parentEventIds\":[1]", StringComparison.Ordinal)
                    && line.Contains("\"rootEventId\":1", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(historyPath))
            {
                File.Delete(historyPath);
            }
        }
    }

    [Fact]
    public void PropagationCoordinator_StopsAtConfiguredDepth()
    {
        World world = CreateWorld();
        world.ConfigureEventPropagation(new EventPropagationCoordinator(
            [new RecursiveHandler()],
            maxDepth: 2,
            maxEventsPerStep: 20));

        world.AddEvent(new WorldEvent
        {
            Type = "loop",
            Severity = WorldEventSeverity.Minor,
            Scope = WorldEventScope.World,
            Narrative = "Loop start"
        });

        Assert.Equal(3, world.Events.Count);
        Assert.Equal(new[] { 0, 1, 2 }, world.Events.Select(evt => evt.PropagationDepth).ToArray());
    }

    private static World CreateWorld()
    {
        World world = new(new WorldTime(12, 1));
        world.Regions.Add(new LivingWorld.Map.Region(0, "Coast"));
        world.Species.Add(new LivingWorld.Life.Species(1, "People", 0.5, 0.5));
        return world;
    }

    private sealed class RecursiveHandler : IWorldEventHandler
    {
        public bool CanHandle(WorldEvent worldEvent) => worldEvent.Type == "loop";

        public IEnumerable<WorldEvent> Handle(World world, WorldEvent worldEvent)
        {
            yield return new WorldEvent
            {
                Type = "loop",
                Severity = WorldEventSeverity.Minor,
                Scope = WorldEventScope.World,
                Narrative = $"Loop depth {worldEvent.PropagationDepth + 1}",
                Reason = "recursive_follow_up"
            };
        }
    }
}
