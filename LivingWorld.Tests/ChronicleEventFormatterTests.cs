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
            WorldEventType.KnowledgeDiscovered,
            WorldEventSeverity.Notable,
            year: 40,
            narrative: "River Clan refined storage");
        WorldEvent majorEvent = CreateEvent(
            WorldEventType.KnowledgeDiscovered,
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
            WorldEventType.KnowledgeDiscovered,
            WorldEventSeverity.Major,
            year: 130,
            narrative: "River Clan mastered Fire");
        WorldEvent secondDiscovery = CreateEvent(
            WorldEventType.KnowledgeDiscovered,
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
}

