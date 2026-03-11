using LivingWorld.Core;
using LivingWorld.Presentation;
using Xunit;

namespace LivingWorld.Tests;

public sealed class ChronicleEventFormatterTests
{
    private readonly ChronicleFocus _focus = new();
    private readonly ChronicleEventFormatter _formatter = new();

    public ChronicleEventFormatterTests()
    {
        _focus.SetFocus(polityId: 7, lineageId: 7);
    }

    [Fact]
    public void DefaultChronicle_ShowsOnlyMajorAndLegendaryEvents()
    {
        WorldEvent notableEvent = CreateEvent(
            WorldEventType.LearnedAdvancement,
            WorldEventSeverity.Notable,
            year: 40,
            narrative: "River Clan refined storage");
        WorldEvent majorEvent = CreateEvent(
            WorldEventType.LearnedAdvancement,
            WorldEventSeverity.Major,
            year: 41,
            narrative: "River Clan began farming");

        Assert.False(_formatter.TryFormat(notableEvent, _focus, out _));
        Assert.True(_formatter.TryFormat(majorEvent, _focus, out string chronicleLine));
        Assert.Equal("Year 41 - River Clan began farming.", chronicleLine);
    }

    [Fact]
    public void RepeatedEventsWithinCooldown_AreSuppressedForSameActor()
    {
        WorldEvent firstMigration = CreateEvent(
            WorldEventType.Migration,
            WorldEventSeverity.Major,
            year: 50,
            narrative: "River Clan migrated to Red Valley");
        WorldEvent repeatedMigration = CreateEvent(
            WorldEventType.Migration,
            WorldEventSeverity.Major,
            year: 61,
            narrative: "River Clan migrated to High Ridge");

        Assert.True(_formatter.TryFormat(firstMigration, _focus, out _));
        Assert.False(_formatter.TryFormat(repeatedMigration, _focus, out _));
    }

    [Fact]
    public void SameEventTypeForDifferentActors_DoNotSuppressEachOther()
    {
        WorldEvent firstMigration = CreateEvent(
            WorldEventType.Migration,
            WorldEventSeverity.Major,
            year: 70,
            polityId: 7,
            narrative: "River Clan migrated to Red Valley");
        WorldEvent otherMigration = CreateEvent(
            WorldEventType.Migration,
            WorldEventSeverity.Major,
            year: 71,
            polityId: 8,
            relatedPolityId: 7,
            narrative: "Hill Clan migrated to Stonewater");

        Assert.True(_formatter.TryFormat(firstMigration, _focus, out _));
        Assert.True(_formatter.TryFormat(otherMigration, _focus, out _));
    }

    [Fact]
    public void SeverityEscalation_BypassesCooldown()
    {
        WorldEvent firstSettlementEvent = CreateEvent(
            WorldEventType.SettlementConsolidated,
            WorldEventSeverity.Major,
            year: 90,
            narrative: "River Clan became a settled people in Red Valley");
        WorldEvent escalatedSettlementEvent = CreateEvent(
            WorldEventType.SettlementConsolidated,
            WorldEventSeverity.Legendary,
            year: 92,
            narrative: "River Clan raised enduring stone settlements in Red Valley");

        Assert.True(_formatter.TryFormat(firstSettlementEvent, _focus, out _));
        Assert.True(_formatter.TryFormat(escalatedSettlementEvent, _focus, out _));
    }

    [Fact]
    public void ConditionStartAndEndTransitions_BypassCooldown()
    {
        WorldEvent shortageStarted = CreateEvent(
            WorldEventType.FoodStress,
            WorldEventSeverity.Major,
            year: 110,
            reason: "hardship_entered",
            narrative: "River Clan entered a period of shortages");
        WorldEvent famineRecovered = CreateEvent(
            WorldEventType.FoodStress,
            WorldEventSeverity.Major,
            year: 112,
            reason: "hardship_recovered",
            narrative: "River Clan recovered from famine");

        Assert.True(_formatter.TryFormat(shortageStarted, _focus, out _));
        Assert.True(_formatter.TryFormat(famineRecovered, _focus, out _));
    }

    [Fact]
    public void NoCooldownEventTypes_AlwaysAppear()
    {
        WorldEvent firstDiscovery = CreateEvent(
            WorldEventType.LearnedAdvancement,
            WorldEventSeverity.Major,
            year: 130,
            narrative: "River Clan mastered Fire");
        WorldEvent secondDiscovery = CreateEvent(
            WorldEventType.LearnedAdvancement,
            WorldEventSeverity.Major,
            year: 131,
            narrative: "River Clan began farming");

        Assert.True(_formatter.TryFormat(firstDiscovery, _focus, out _));
        Assert.True(_formatter.TryFormat(secondDiscovery, _focus, out _));
    }

    [Fact]
    public void MajorTurningPoints_RemainVisible()
    {
        WorldEvent stageChange = CreateEvent(
            WorldEventType.StageChanged,
            WorldEventSeverity.Legendary,
            year: 150,
            narrative: "River Clan formed a Civilization");

        Assert.True(_formatter.TryFormat(stageChange, _focus, out string chronicleLine));
        Assert.Equal("Year 150 - River Clan formed a Civilization.", chronicleLine);
    }

    [Fact]
    public void PopulationChanged_IsSuppressedFromDefaultChronicle()
    {
        WorldEvent populationChange = CreateEvent(
            WorldEventType.PopulationChanged,
            WorldEventSeverity.Legendary,
            year: 160,
            narrative: "River Clan declined from 200 to 90");

        Assert.False(_formatter.TryFormat(populationChange, _focus, out _));
    }

    [Fact]
    public void FocusHandoffEvent_RemainsVisibleForCurrentFocusedLine()
    {
        WorldEvent handoffEvent = CreateFocusTransitionEvent(
            WorldEventType.FocusHandoffCollapse,
            year: 170,
            currentFocusedPolityId: 7,
            currentFocusedLineageId: 7,
            successorPolityId: 11,
            successorLineageId: 7,
            narrative: "River Clan collapsed. Its legacy continued through Valley Clan");

        Assert.True(_formatter.TryFormat(handoffEvent, _focus, out string chronicleLine));
        Assert.Equal("Year 170 - River Clan collapsed. Its legacy continued through Valley Clan.", chronicleLine);
    }

    [Fact]
    public void ChronicleFollowsSuccessorAfterHandoff_AndSuppressesOldNonFocalEvents()
    {
        WorldEvent handoffEvent = CreateFocusTransitionEvent(
            WorldEventType.FocusHandoffFragmentation,
            year: 180,
            currentFocusedPolityId: 7,
            currentFocusedLineageId: 7,
            successorPolityId: 11,
            successorLineageId: 7,
            narrative: "River Clan fractured into rival groups. The chronicle now follows Valley Clan");

        Assert.True(_formatter.TryFormat(handoffEvent, _focus, out _));

        _focus.SetFocus(polityId: 11, lineageId: 7);

        WorldEvent oldPolityEvent = CreateEvent(
            WorldEventType.LearnedAdvancement,
            WorldEventSeverity.Major,
            year: 181,
            polityId: 7,
            narrative: "River Clan mastered Fire");
        WorldEvent successorEvent = CreateEvent(
            WorldEventType.LearnedAdvancement,
            WorldEventSeverity.Major,
            year: 181,
            polityId: 11,
            narrative: "Valley Clan began farming");

        Assert.False(_formatter.TryFormat(oldPolityEvent, _focus, out _));
        Assert.True(_formatter.TryFormat(successorEvent, _focus, out string chronicleLine));
        Assert.Equal("Year 181 - Valley Clan began farming.", chronicleLine);
    }

    [Fact]
    public void CollapseHandoff_KeepsChronicleFollowingSameLineage()
    {
        WorldEvent handoffEvent = CreateFocusTransitionEvent(
            WorldEventType.FocusHandoffCollapse,
            year: 190,
            currentFocusedPolityId: 7,
            currentFocusedLineageId: 7,
            successorPolityId: 12,
            successorLineageId: 7,
            narrative: "River Clan collapsed. Its legacy continued through Marsh Clan");

        Assert.True(_formatter.TryFormat(handoffEvent, _focus, out _));

        _focus.SetFocus(polityId: 12, lineageId: 7);

        WorldEvent successorStageChange = CreateEvent(
            WorldEventType.StageChanged,
            WorldEventSeverity.Major,
            year: 191,
            polityId: 12,
            narrative: "Marsh Clan became a Settled Society");

        Assert.True(_formatter.TryFormat(successorStageChange, _focus, out _));
    }

    [Fact]
    public void InternalPropagationEvents_RemainSuppressedFromDefaultChronicle()
    {
        WorldEvent migrationPressure = CreateEvent(
            WorldEventType.MigrationPressure,
            WorldEventSeverity.Legendary,
            year: 200,
            narrative: "River Clan came under migration pressure");

        Assert.False(_formatter.TryFormat(migrationPressure, _focus, out _));
    }

    [Fact]
    public void ChronicleKeepsVisiblePolityNames_WithoutSpeciesSuffix()
    {
        WorldEvent fragmentation = new()
        {
            EventId = 210,
            Year = 210,
            Month = 12,
            Season = Season.Winter,
            Type = WorldEventType.Fragmentation,
            Severity = WorldEventSeverity.Major,
            Narrative = "River Clan founded Valley Clan in Red Valley",
            PolityId = 7,
            PolityName = "River Clan",
            RelatedPolityId = 11,
            RelatedPolityName = "Valley Clan",
            SpeciesId = 1,
            SpeciesName = "Humans",
            RelatedPolitySpeciesId = 2,
            RelatedPolitySpeciesName = "Wolfkin",
            RegionId = 1,
            RegionName = "Red Valley"
        };

        Assert.True(_formatter.TryFormat(fragmentation, _focus, out string chronicleLine));
        Assert.Equal("Year 210 - River Clan founded Valley Clan in Red Valley.", chronicleLine);
    }

    private static WorldEvent CreateEvent(
        string type,
        WorldEventSeverity severity,
        int year,
        string narrative,
        int polityId = 7,
        int? relatedPolityId = null,
        string? reason = null)
    {
        return new WorldEvent
        {
            EventId = year,
            Year = year,
            Month = 12,
            Season = Season.Winter,
            Type = type,
            Severity = severity,
            Narrative = narrative,
            Reason = reason,
            PolityId = polityId,
            PolityName = polityId == 7 ? "River Clan" : "Hill Clan",
            RelatedPolityId = relatedPolityId,
            RelatedPolityName = relatedPolityId == 7 ? "River Clan" : null,
            RegionId = 1,
            RegionName = "Red Valley"
        };
    }

    private static WorldEvent CreateFocusTransitionEvent(
        string type,
        int year,
        int currentFocusedPolityId,
        int currentFocusedLineageId,
        int successorPolityId,
        int successorLineageId,
        string narrative)
    {
        return new WorldEvent
        {
            EventId = year,
            Year = year,
            Month = 12,
            Season = Season.Winter,
            Type = type,
            Severity = WorldEventSeverity.Major,
            Narrative = narrative,
            PolityId = successorPolityId,
            PolityName = successorPolityId == 11 ? "Valley Clan" : "Marsh Clan",
            RelatedPolityId = currentFocusedPolityId,
            RelatedPolityName = "River Clan",
            Before = new Dictionary<string, string>
            {
                ["focusedPolityId"] = currentFocusedPolityId.ToString(),
                ["focusedLineageId"] = currentFocusedLineageId.ToString()
            },
            After = new Dictionary<string, string>
            {
                ["focusedPolityId"] = successorPolityId.ToString(),
                ["focusedLineageId"] = successorLineageId.ToString()
            },
            Metadata = new Dictionary<string, string>
            {
                ["previousPolityId"] = currentFocusedPolityId.ToString(),
                ["previousLineageId"] = currentFocusedLineageId.ToString(),
                ["newPolityId"] = successorPolityId.ToString(),
                ["newLineageId"] = successorLineageId.ToString()
            }
        };
    }
}
