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
            ResolvePriorYearFoodState(world.Time.Year, polity),
            polity.RegionId,
            world.Regions.First(region => region.Id == polity.RegionId).Name);
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

        FocalYearSnapshot startSnapshot = ResolveStartSnapshot(world, world.Time.Year, focusedPolity);
        int populationDelta = focusedPolity.Population - startSnapshot.Population;

        Region region = world.Regions.First(region => region.Id == focusedPolity.RegionId);
        List<WorldEvent> eventsThisYear = world.Events
            .Where(evt => evt.Year == world.Time.Year)
            .OrderBy(evt => evt.Month)
            .ThenBy(evt => evt.EventId)
            .ToList();

        List<string> focalEvents = BuildChronicleEvents(world, focusedPolity, startSnapshot, eventsThisYear);
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
            foreach (string narrative in focalEvents.Take(3))
            {
                lines.Add($"- {narrative}");
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

    private static List<string> BuildChronicleEvents(
        World world,
        Polity focusedPolity,
        FocalYearSnapshot startSnapshot,
        IReadOnlyList<WorldEvent> eventsThisYear)
    {
        List<string> lines = new();
        List<WorldEvent> focalEvents = SelectFocalEvents(eventsThisYear, focusedPolity.Id);

        string? migrationLine = BuildMigrationSummaryLine(world, focusedPolity, startSnapshot, focalEvents);
        if (!string.IsNullOrWhiteSpace(migrationLine))
        {
            lines.Add(migrationLine);
        }

        string? foodLine = BuildFoodSummaryLine(focusedPolity, startSnapshot);
        if (!string.IsNullOrWhiteSpace(foodLine))
        {
            lines.Add(foodLine);
        }

        List<string> milestoneLines = focalEvents
            .OrderBy(GetMilestonePriority)
            .ThenBy(evt => evt.Month)
            .ThenBy(evt => evt.EventId)
            .Where(evt => IsMajorMilestoneEvent(evt, focusedPolity.Id))
            .Select(evt => evt.Narrative)
            .Where(narrative => !string.IsNullOrWhiteSpace(narrative))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string milestoneLine in milestoneLines)
        {
            if (lines.Count >= 3)
            {
                break;
            }

            // Skip duplicate migration/food narratives once synthesized.
            if (ContainsSameMeaning(lines, milestoneLine))
            {
                continue;
            }

            lines.Add(milestoneLine);
        }

        return lines;
    }

    private static bool ContainsSameMeaning(IReadOnlyList<string> currentLines, string candidate)
    {
        return currentLines.Any(line =>
            string.Equals(line, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string? BuildMigrationSummaryLine(
        World world,
        Polity focusedPolity,
        FocalYearSnapshot startSnapshot,
        IReadOnlyList<WorldEvent> focalEvents)
    {
        List<WorldEvent> migrationEvents = focalEvents
            .Where(evt => evt.Type == WorldEventType.Migration && evt.PolityId == focusedPolity.Id)
            .OrderBy(evt => evt.Month)
            .ThenBy(evt => evt.EventId)
            .ToList();

        if (migrationEvents.Count == 0)
        {
            return null;
        }

        string endRegionName = world.Regions.First(region => region.Id == focusedPolity.RegionId).Name;
        bool endedWhereStarted = startSnapshot.RegionId == focusedPolity.RegionId;

        if (endedWhereStarted && migrationEvents.Count > 1)
        {
            return $"{focusedPolity.Name} wandered in search of food.";
        }

        if (!endedWhereStarted)
        {
            if (migrationEvents.Count == 1)
            {
                return $"{focusedPolity.Name} migrated to {endRegionName}.";
            }

            return $"{focusedPolity.Name} migrated from {startSnapshot.RegionName} to {endRegionName}.";
        }

        return $"{focusedPolity.Name} migrated to {endRegionName}.";
    }

    private static string? BuildFoodSummaryLine(Polity focusedPolity, FocalYearSnapshot startSnapshot)
    {
        ChronicleFoodCondition condition = ChronicleTextFormatter.ResolveChronicleFoodCondition(
            focusedPolity,
            startSnapshot.Population);

        return ChronicleTextFormatter.DescribeFoodConditionNarrative(focusedPolity.Name, condition);
    }

    private static bool IsMajorMilestoneEvent(WorldEvent evt, int focusedPolityId)
    {
        bool isFocusedSubject = evt.PolityId == focusedPolityId;

        return evt.Type is WorldEventType.KnowledgeDiscovered
            or WorldEventType.SettlementFounded
            or WorldEventType.SettlementConsolidated
            or WorldEventType.StageChanged
            or WorldEventType.Fragmentation
            or WorldEventType.PolityCollapsed
            || (isFocusedSubject && evt.Type is WorldEventType.TradeLinkStarted
                or WorldEventType.TradeRelief
                or WorldEventType.TradeDependency
                or WorldEventType.TradeLinkCollapsed);
    }

    private static int GetMilestonePriority(WorldEvent evt)
    {
        return evt.Type switch
        {
            WorldEventType.PolityCollapsed => 0,
            WorldEventType.Fragmentation => 1,
            WorldEventType.StageChanged => 2,
            WorldEventType.TradeDependency => 3,
            WorldEventType.TradeRelief => 4,
            WorldEventType.TradeLinkStarted => 5,
            WorldEventType.TradeLinkCollapsed => 6,
            WorldEventType.SettlementFounded => 7,
            WorldEventType.SettlementConsolidated => 8,
            WorldEventType.KnowledgeDiscovered => 9,
            _ => 10
        };
    }

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

        FoodStateSummary currentFood = ChronicleTextFormatter.ResolveFoodState(current);
        if (startSnapshot.FoodState.HasValue && startSnapshot.FoodState.Value != currentFood)
        {
            changes.Add(
                $"Food: {ChronicleTextFormatter.DescribeFoodState(startSnapshot.FoodState.Value)} -> {ChronicleTextFormatter.DescribeFoodState(currentFood)}");
        }

        return changes;
    }

    private static string RenderStageName(PolityStage stage)
        => stage switch
        {
            PolityStage.SettledSociety => "Settled Society",
            _ => stage.ToString()
        };

    private FocalYearSnapshot ResolveStartSnapshot(World world, int year, Polity polity)
    {
        if (_yearStartSnapshots.TryGetValue(year, out FocalYearSnapshot? snapshot) && snapshot.PolityId == polity.Id)
        {
            return snapshot;
        }

        Region currentRegion = world.Regions.First(region => region.Id == polity.RegionId);

        return new FocalYearSnapshot(
            polity.Id,
            polity.Population,
            polity.Stage,
            ResolvePriorYearFoodState(year, polity),
            polity.RegionId,
            currentRegion.Name);
    }

    private static FoodStateSummary? ResolvePriorYearFoodState(int currentYear, Polity polity)
    {
        if (!polity.LastResolvedFoodState.HasValue || !polity.LastResolvedFoodStateYear.HasValue)
        {
            return null;
        }

        return polity.LastResolvedFoodStateYear.Value == currentYear - 1
            ? polity.LastResolvedFoodState
            : null;
    }

    private sealed record FocalYearSnapshot(
        int PolityId,
        int Population,
        PolityStage Stage,
        FoodStateSummary? FoodState,
        int RegionId,
        string RegionName);
}
