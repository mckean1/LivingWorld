using LivingWorld.Core;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Presentation;

public sealed class NarrativeRenderer
{
    private readonly Dictionary<int, FocalYearSnapshot> _yearStartSnapshots = new();

    public void CaptureYearStart(World world, ChronicleFocus focus)
    {
        Polity? polity = focus.ResolvePolity(world);
        if (polity is null)
        {
            return;
        }

        _yearStartSnapshots[world.Time.Year] = new FocalYearSnapshot(
            polity.Id,
            polity.Population,
            polity.Stage,
            ChronicleTextFormatter.DescribeFoodState(polity),
            polity.Advancements.Count);
    }

    public IReadOnlyList<string> RenderTickChronicle(World world)
    {
        return [];
    }

    public IReadOnlyList<string> RenderYearReport(World world, ChronicleFocus focus)
    {
        List<string> lines = new();
        Polity? focusedPolity = focus.ResolvePolity(world);

        if (focusedPolity is null)
        {
            lines.Add(string.Empty);
            lines.Add($"==================================================");
            lines.Add($"Year {world.Time.Year} - No Focal Polity");
            lines.Add("==================================================");
            lines.Add("This Year");
            lines.Add("- The tracked polity no longer survives.");
            lines.Add(string.Empty);
            return lines;
        }

        FocalYearSnapshot startSnapshot = ResolveStartSnapshot(world.Time.Year, focusedPolity);
        int populationDelta = focusedPolity.Population - startSnapshot.Population;

        Region region = world.Regions.First(region => region.Id == focusedPolity.RegionId);
        List<WorldEvent> eventsThisYear = world.Events
            .Where(evt => evt.Year == world.Time.Year)
            .OrderBy(evt => evt.Month)
            .ThenBy(evt => evt.EventId)
            .ToList();

        List<WorldEvent> focalEvents = SelectFocalEvents(eventsThisYear, focusedPolity.Id);
        List<string> notableChanges = BuildNotableChanges(startSnapshot, focusedPolity);
        List<WorldEvent> worldNotes = SelectWorldNotes(eventsThisYear, focusedPolity.Id);

        lines.Add(string.Empty);
        lines.Add("==================================================");
        lines.Add($"Year {world.Time.Year} - {focusedPolity.Name}");
        lines.Add($"Region: {region.Name}");
        lines.Add($"Population: {focusedPolity.Population} ({ChronicleTextFormatter.RenderPopulationDelta(populationDelta)})");
        lines.Add($"Food: {ChronicleTextFormatter.DescribeFoodState(focusedPolity)}");
        lines.Add($"Status: {RenderStageName(focusedPolity.Stage)}");
        lines.Add($"Knowledge: {ChronicleTextFormatter.DescribeKnowledge(focusedPolity)}");
        lines.Add("==================================================");
        lines.Add(string.Empty);

        lines.Add("This Year");
        if (focalEvents.Count == 0)
        {
            lines.Add("- Life held steady without major upheaval.");
        }
        else
        {
            foreach (WorldEvent worldEvent in focalEvents.Take(5))
            {
                lines.Add($"- {worldEvent.Narrative}");
            }
        }

        if (notableChanges.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Notable Changes");
            foreach (string change in notableChanges)
            {
                lines.Add($"- {change}");
            }
        }

        if (worldNotes.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("World Notes");
            foreach (WorldEvent worldNote in worldNotes)
            {
                lines.Add($"- {worldNote.Narrative}");
            }
        }

        return lines;
    }

    private static List<WorldEvent> SelectFocalEvents(IReadOnlyList<WorldEvent> eventsThisYear, int focusedPolityId)
    {
        List<WorldEvent> prioritized = eventsThisYear
            .Where(evt => IsFocalEvent(evt, focusedPolityId))
            .Where(evt => evt.Severity >= WorldEventSeverity.Notable)
            .ToList();

        if (prioritized.Count > 0)
        {
            return prioritized;
        }

        return eventsThisYear
            .Where(evt => IsFocalEvent(evt, focusedPolityId))
            .Where(evt => evt.Severity >= WorldEventSeverity.Normal)
            .ToList();
    }

    private static bool IsFocalEvent(WorldEvent evt, int focusedPolityId)
        => evt.PolityId == focusedPolityId || evt.RelatedPolityId == focusedPolityId;

    private static List<WorldEvent> SelectWorldNotes(IReadOnlyList<WorldEvent> eventsThisYear, int focusedPolityId)
    {
        return eventsThisYear
            .Where(evt => !IsFocalEvent(evt, focusedPolityId))
            .Where(evt => evt.Severity >= WorldEventSeverity.Critical
                || evt.Type is WorldEventType.PolityCollapsed
                    or WorldEventType.Fragmentation
                    or WorldEventType.StageChanged
                    or WorldEventType.SettlementFounded)
            .OrderByDescending(evt => evt.Severity)
            .ThenBy(evt => evt.Month)
            .Take(2)
            .ToList();
    }

    private static List<string> BuildNotableChanges(FocalYearSnapshot startSnapshot, Polity current)
    {
        List<string> changes = new();

        if (startSnapshot.Population != current.Population)
        {
            changes.Add($"Population: {startSnapshot.Population} -> {current.Population}");
        }

        if (startSnapshot.Stage != current.Stage)
        {
            changes.Add($"Status: {RenderStageName(startSnapshot.Stage)} -> {RenderStageName(current.Stage)}");
        }

        string currentFood = ChronicleTextFormatter.DescribeFoodState(current);
        if (!string.Equals(startSnapshot.FoodState, currentFood, StringComparison.Ordinal))
        {
            changes.Add($"Food: {startSnapshot.FoodState} -> {currentFood}");
        }

        if (startSnapshot.AdvancementCount != current.Advancements.Count)
        {
            changes.Add($"Knowledge breadth: {startSnapshot.AdvancementCount} -> {current.Advancements.Count}");
        }

        return changes;
    }

    private static string RenderStageName(PolityStage stage)
        => stage switch
        {
            PolityStage.SettledSociety => "Settled Society",
            _ => stage.ToString()
        };

    private FocalYearSnapshot ResolveStartSnapshot(int year, Polity polity)
    {
        if (_yearStartSnapshots.TryGetValue(year, out FocalYearSnapshot? snapshot) && snapshot.PolityId == polity.Id)
        {
            return snapshot;
        }

        return new FocalYearSnapshot(
            polity.Id,
            polity.Population,
            polity.Stage,
            ChronicleTextFormatter.DescribeFoodState(polity),
            polity.Advancements.Count);
    }

    private sealed record FocalYearSnapshot(
        int PolityId,
        int Population,
        PolityStage Stage,
        string FoodState,
        int AdvancementCount);
}
