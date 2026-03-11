using LivingWorld.Core;
using LivingWorld.Presentation;
using Xunit;

namespace LivingWorld.Tests;

public sealed class StructuredHistoryPipelineTests
{
    [Fact]
    public void LowerSeverityEvents_AreStillWrittenToStructuredHistory()
    {
        string historyPath = Path.Combine(Path.GetTempPath(), $"livingworld-history-{Guid.NewGuid():N}.jsonl");

        try
        {
            World world = new(new WorldTime(12, 1));
            world.Regions.Add(new LivingWorld.Map.Region(0, "Coast"));
            world.Species.Add(new LivingWorld.Life.Species(1, "People", 0.5, 0.5));

            SimulationOptions options = new()
            {
                OutputMode = OutputMode.Debug,
                WriteStructuredHistory = true,
                HistoryFilePath = historyPath
            };

            using (Simulation simulation = new(world, options))
            {
                world.AddEvent(
                    WorldEventType.TradeTransfer,
                    WorldEventSeverity.Minor,
                    "River Clan sent food to Hill Clan",
                    polityId: 7,
                    polityName: "River Clan",
                    relatedPolityId: 8,
                    relatedPolityName: "Hill Clan");

                world.AddEvent(
                    WorldEventType.WorldEvent,
                    WorldEventSeverity.Debug,
                    "Debug bookkeeping event");
            }

            string[] lines = File.ReadAllLines(historyPath);

            Assert.Contains(lines, line => line.Contains("\"severity\":\"Minor\"", StringComparison.Ordinal));
            Assert.Contains(lines, line => line.Contains("\"severity\":\"Debug\"", StringComparison.Ordinal));
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
    public void PopulationChanged_RemainsInStructuredHistory_WhenSuppressedFromChronicle()
    {
        string historyPath = Path.Combine(Path.GetTempPath(), $"livingworld-population-{Guid.NewGuid():N}.jsonl");

        try
        {
            World world = new(new WorldTime(12, 1));
            world.Regions.Add(new LivingWorld.Map.Region(0, "Coast"));
            world.Species.Add(new LivingWorld.Life.Species(1, "People", 0.5, 0.5));

            SimulationOptions options = new()
            {
                OutputMode = OutputMode.Debug,
                WriteStructuredHistory = true,
                HistoryFilePath = historyPath
            };

            using (Simulation simulation = new(world, options))
            {
                world.AddEvent(
                    WorldEventType.PopulationChanged,
                    WorldEventSeverity.Legendary,
                    "River Clan declined from 200 to 90",
                    polityId: 7,
                    polityName: "River Clan");
            }

            string[] lines = File.ReadAllLines(historyPath);

            Assert.Contains(lines, line => line.Contains("\"type\":\"population_changed\"", StringComparison.Ordinal));
            Assert.Contains(lines, line => line.Contains("\"severity\":\"Legendary\"", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(historyPath))
            {
                File.Delete(historyPath);
            }
        }
    }
}
