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
    public void RepeatedFoodRecoveryState_WithinCooldown_IsSuppressed()
    {
        WorldEvent firstRecovery = CreateEvent(
            WorldEventType.FoodStabilized,
            WorldEventSeverity.Major,
            year: 113,
            reason: "food_recovered",
            narrative: "River Clan stabilized after hardship");
        firstRecovery.After["hardshipTier"] = "Stable";

        WorldEvent repeatedRecovery = CreateEvent(
            WorldEventType.FoodStabilized,
            WorldEventSeverity.Major,
            year: 118,
            reason: "food_recovered",
            narrative: "River Clan stabilized after hardship");
        repeatedRecovery.After["hardshipTier"] = "Stable";

        Assert.True(_formatter.TryFormat(firstRecovery, _focus, out _));
        Assert.False(_formatter.TryFormat(repeatedRecovery, _focus, out _));
    }

    [Fact]
    public void NoCooldownEventTypes_AlwaysAppear()
    {
        WorldEvent firstDiscovery = CreateEvent(
            WorldEventType.LearnedAdvancement,
            WorldEventSeverity.Major,
            year: 130,
            narrative: "River Clan learned Fire");
        WorldEvent secondDiscovery = CreateEvent(
            WorldEventType.LearnedAdvancement,
            WorldEventSeverity.Major,
            year: 131,
            narrative: "River Clan began farming");

        Assert.True(_formatter.TryFormat(firstDiscovery, _focus, out _));
        Assert.True(_formatter.TryFormat(secondDiscovery, _focus, out _));
    }

    [Fact]
    public void RepeatedExactNarrative_ForDefaultVisibleProfile_IsSuppressed()
    {
        WorldEvent firstFragmentation = CreateEvent(
            WorldEventType.Fragmentation,
            WorldEventSeverity.Major,
            year: 131,
            narrative: "River Clan founded Valley Clan in Red Valley");
        WorldEvent repeatedFragmentation = CreateEvent(
            WorldEventType.Fragmentation,
            WorldEventSeverity.Major,
            year: 132,
            narrative: "River Clan founded Valley Clan in Red Valley");

        Assert.True(_formatter.TryFormat(firstFragmentation, _focus, out _));
        Assert.False(_formatter.TryFormat(repeatedFragmentation, _focus, out _));
    }

    [Fact]
    public void DifferentNarratives_ForDefaultVisibleProfile_RemainVisible()
    {
        WorldEvent firstFragmentation = CreateEvent(
            WorldEventType.Fragmentation,
            WorldEventSeverity.Major,
            year: 133,
            narrative: "River Clan founded Valley Clan in Red Valley");
        WorldEvent differentFragmentation = CreateEvent(
            WorldEventType.Fragmentation,
            WorldEventSeverity.Major,
            year: 134,
            narrative: "River Clan founded Marsh Clan in Stonewater");

        Assert.True(_formatter.TryFormat(firstFragmentation, _focus, out _));
        Assert.True(_formatter.TryFormat(differentFragmentation, _focus, out _));
    }

    [Fact]
    public void AdaptationEvents_WithinCooldown_AreSuppressed_ForSameSpeciesRegionReasonAndMilestone()
    {
        WorldEvent firstAdaptation = CreateAdaptationEvent(
            year: 132,
            milestone: 2,
            narrative: "Ashfang Wolf in Amber Reach became strongly adapted to the region");
        WorldEvent repeatedAdaptation = CreateAdaptationEvent(
            year: 138,
            milestone: 2,
            narrative: "Ashfang Wolf in Amber Reach became strongly adapted to the region");

        Assert.True(_formatter.TryFormat(firstAdaptation, _focus, out _));
        Assert.False(_formatter.TryFormat(repeatedAdaptation, _focus, out _));
    }

    [Fact]
    public void AdaptationEscalation_WithNewMilestone_RemainsVisibleWithinCooldown()
    {
        WorldEvent firstAdaptation = CreateAdaptationEvent(
            year: 140,
            milestone: 1,
            severity: WorldEventSeverity.Major,
            narrative: "Ashfang Wolf adapted to Amber Reach");
        WorldEvent strongerAdaptation = CreateAdaptationEvent(
            year: 142,
            milestone: 2,
            severity: WorldEventSeverity.Legendary,
            narrative: "Ashfang Wolf in Amber Reach became strongly adapted to the region");

        Assert.True(_formatter.TryFormat(firstAdaptation, _focus, out _));
        Assert.True(_formatter.TryFormat(strongerAdaptation, _focus, out string chronicleLine));
        Assert.Equal("Year 142 - Ashfang Wolf in Amber Reach became strongly adapted to the region.", chronicleLine);
    }

    [Fact]
    public void DifferentRegionalEvolutionaryTurns_DoNotSuppressEachOther()
    {
        WorldEvent firstTurningPoint = CreateEvolutionEvent(
            year: 145,
            regionId: 2,
            regionName: "Amber Reach",
            milestone: 2,
            narrative: "Ashfang Wolf in Amber Reach reached a clear evolutionary turning point");
        WorldEvent secondTurningPoint = CreateEvolutionEvent(
            year: 146,
            regionId: 3,
            regionName: "Stonewater",
            milestone: 2,
            narrative: "Ashfang Wolf in Stonewater reached a clear evolutionary turning point");

        Assert.True(_formatter.TryFormat(firstTurningPoint, _focus, out _));
        Assert.True(_formatter.TryFormat(secondTurningPoint, _focus, out _));
    }

    [Fact]
    public void MigrationNeedsMeaningfulGapBeforeDifferentDestinationRepeatsAppear()
    {
        WorldEvent firstMigration = CreateMigrationEvent(
            year: 150,
            fromRegionId: 1,
            toRegionId: 2,
            toRegionName: "Amber Reach",
            narrative: "River Clan migrated to Amber Reach");
        WorldEvent tooSoonMigration = CreateMigrationEvent(
            year: 154,
            fromRegionId: 2,
            toRegionId: 3,
            toRegionName: "Stonewater",
            narrative: "River Clan migrated to Stonewater");
        WorldEvent laterMigration = CreateMigrationEvent(
            year: 159,
            fromRegionId: 3,
            toRegionId: 4,
            toRegionName: "High Ridge",
            narrative: "River Clan migrated to High Ridge");

        Assert.True(_formatter.TryFormat(firstMigration, _focus, out _));
        Assert.False(_formatter.TryFormat(tooSoonMigration, _focus, out _));
        Assert.True(_formatter.TryFormat(laterMigration, _focus, out _));
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
            narrative: "River Clan learned Fire");
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
    public void SuppressedEventsRemainCanonicalEvenWhenChronicleHidesThem()
    {
        World world = new(new WorldTime(200, 12));
        WorldEvent migrationPressure = CreateEvent(
            WorldEventType.MigrationPressure,
            WorldEventSeverity.Legendary,
            year: 200,
            narrative: "River Clan came under migration pressure");

        world.AddEvent(migrationPressure);

        Assert.Single(world.Events);
        Assert.False(_formatter.TryFormat(world.Events[0], _focus, out _));
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

    [Fact]
    public void PlayerFacingTerminology_UsesDiscoveredForDiscoveries_AndLearnedForAdvancements()
    {
        WorldEvent discoveryEvent = CreateEvent(
            WorldEventType.KnowledgeDiscovered,
            WorldEventSeverity.Major,
            year: 220,
            narrative: "River Clan discovered that River Elk are edible");
        WorldEvent learnedEvent = CreateEvent(
            WorldEventType.LearnedAdvancement,
            WorldEventSeverity.Major,
            year: 221,
            narrative: "River Clan learned Organized Hunting");

        Assert.True(_formatter.TryFormat(discoveryEvent, _focus, out string discoveryLine));
        Assert.True(_formatter.TryFormat(learnedEvent, _focus, out string learnedLine));
        Assert.Contains("discovered", discoveryLine, StringComparison.Ordinal);
        Assert.DoesNotContain("learned", discoveryLine, StringComparison.Ordinal);
        Assert.Contains("learned", learnedLine, StringComparison.Ordinal);
    }

    [Fact]
    public void MajorFamineRelief_RemainsVisibleInChronicle()
    {
        WorldEvent reliefEvent = new()
        {
            EventId = 230,
            Year = 230,
            Month = 1,
            Season = Season.Winter,
            Type = WorldEventType.FamineRelief,
            Severity = WorldEventSeverity.Major,
            Narrative = "Food caravans arrived from Stone Valley, relieving famine in Hill Camp",
            Reason = "settlement_starvation_prevented",
            PolityId = 7,
            PolityName = "River Clan",
            RegionId = 1,
            RegionName = "Red Valley",
            SettlementId = 7001,
            SettlementName = "Hill Camp",
            Metadata = new Dictionary<string, string>
            {
                ["senderSettlementId"] = "7002"
            },
            After = new Dictionary<string, string>
            {
                ["foodState"] = "Stable"
            }
        };

        Assert.True(_formatter.TryFormat(reliefEvent, _focus, out string chronicleLine));
        Assert.Equal("Year 230 - Food caravans arrived from Stone Valley, relieving famine in Hill Camp.", chronicleLine);
    }

    [Fact]
    public void RepeatedAidFailureForSameSettlementState_IsSuppressed()
    {
        WorldEvent firstFailure = CreateEvent(
            WorldEventType.AidFailed,
            WorldEventSeverity.Major,
            year: 233,
            narrative: "No aid arrived. Stonefen Hearth began to starve",
            reason: "settlement_starvation_began_unaided",
            settlementId: 7003,
            settlementName: "Stonefen Hearth");
        firstFailure.After["foodState"] = "Starving";
        firstFailure.After["starvationStage"] = "Starving";

        WorldEvent repeatedFailure = CreateEvent(
            WorldEventType.AidFailed,
            WorldEventSeverity.Major,
            year: 234,
            narrative: "No aid arrived. Stonefen Hearth began to starve",
            reason: "settlement_starvation_began_unaided",
            settlementId: 7003,
            settlementName: "Stonefen Hearth");
        repeatedFailure.After["foodState"] = "Starving";
        repeatedFailure.After["starvationStage"] = "Starving";

        Assert.True(_formatter.TryFormat(firstFailure, _focus, out _));
        Assert.False(_formatter.TryFormat(repeatedFailure, _focus, out _));
    }

    [Fact]
    public void WorsenedAidFailureState_RemainsVisible()
    {
        WorldEvent firstFailure = CreateEvent(
            WorldEventType.AidFailed,
            WorldEventSeverity.Major,
            year: 235,
            narrative: "No aid arrived. Stonefen Hearth began to starve",
            reason: "settlement_starvation_began_unaided",
            settlementId: 7003,
            settlementName: "Stonefen Hearth");
        firstFailure.After["foodState"] = "Starving";
        firstFailure.After["starvationStage"] = "Starving";

        WorldEvent worsenedFailure = CreateEvent(
            WorldEventType.AidFailed,
            WorldEventSeverity.Legendary,
            year: 238,
            narrative: "No aid arrived. Starvation worsened in Stonefen Hearth",
            reason: "settlement_starvation_worsened_unaided",
            settlementId: 7003,
            settlementName: "Stonefen Hearth");
        worsenedFailure.After["foodState"] = "Starving";
        worsenedFailure.After["starvationStage"] = "Severe";

        Assert.True(_formatter.TryFormat(firstFailure, _focus, out _));
        Assert.True(_formatter.TryFormat(worsenedFailure, _focus, out string chronicleLine));
        Assert.Equal("Year 238 - No aid arrived. Starvation worsened in Stonefen Hearth.", chronicleLine);
    }

    [Fact]
    public void MajorAnimalDomestication_RemainsVisibleInChronicle()
    {
        WorldEvent domesticationEvent = CreateEvent(
            WorldEventType.AnimalDomesticated,
            WorldEventSeverity.Major,
            year: 231,
            narrative: "Hill Camp established herds of River Goat");

        domesticationEvent.Metadata["targetSpeciesId"] = "2";
        domesticationEvent.Metadata["targetSpeciesName"] = "River Goat";
        domesticationEvent.Metadata["managedKind"] = "herd";

        Assert.True(_formatter.TryFormat(domesticationEvent, _focus, out string chronicleLine));
        Assert.Equal("Year 231 - Hill Camp established herds of River Goat.", chronicleLine);
    }

    [Fact]
    public void NotableDomesticationCandidate_RemainsSuppressedFromDefaultChronicle()
    {
        WorldEvent candidateEvent = CreateEvent(
            WorldEventType.SpeciesDomesticationCandidateIdentified,
            WorldEventSeverity.Notable,
            year: 232,
            narrative: "River Clan discovered that River Goat could be kept near camp");

        candidateEvent.Metadata["targetSpeciesId"] = "2";
        candidateEvent.Metadata["targetSpeciesName"] = "River Goat";

        Assert.False(_formatter.TryFormat(candidateEvent, _focus, out _));
    }

    [Fact]
    public void RepeatedMaterialSpecialization_ForSameSettlement_IsSuppressed()
    {
        WorldEvent firstEvent = CreateEvent(
            WorldEventType.SettlementSpecialized,
            WorldEventSeverity.Major,
            year: 240,
            narrative: "Stonefen became known for pottery",
            settlementId: 7004,
            settlementName: "Stonefen");
        firstEvent.Metadata["specializationTag"] = "PotteryTradition";
        firstEvent.Metadata["materialType"] = "Pottery";

        WorldEvent repeatedEvent = CreateEvent(
            WorldEventType.SettlementSpecialized,
            WorldEventSeverity.Major,
            year: 245,
            narrative: "Stonefen became known for pottery",
            settlementId: 7004,
            settlementName: "Stonefen");
        repeatedEvent.Metadata["specializationTag"] = "PotteryTradition";
        repeatedEvent.Metadata["materialType"] = "Pottery";

        Assert.True(_formatter.TryFormat(firstEvent, _focus, out _));
        Assert.False(_formatter.TryFormat(repeatedEvent, _focus, out _));
    }

    [Fact]
    public void LowSignalMaterialEvents_StayOutOfMainChronicle_WhileGroupedCrisisAppearsOnce()
    {
        WorldEvent perMaterialFailure = CreateEvent(
            WorldEventType.MaterialConvoyFailed,
            WorldEventSeverity.Notable,
            year: 241,
            narrative: "No material convoy arrived. Stonefen Hearth fell into salt shortage",
            reason: "critical_material_shortage_unaided",
            settlementId: 7003,
            settlementName: "Stonefen Hearth");
        perMaterialFailure.Metadata["materialType"] = "Salt";
        perMaterialFailure.Metadata["shortageBand"] = "2";

        WorldEvent groupedCrisis = CreateEvent(
            WorldEventType.MaterialCrisisStarted,
            WorldEventSeverity.Major,
            year: 241,
            narrative: "No material convoy arrived. Stonefen Hearth fell into shortages of salt, pottery, and simple tools",
            reason: "grouped_material_crisis_unaided",
            settlementId: 7003,
            settlementName: "Stonefen Hearth");
        groupedCrisis.Metadata["groupedMaterials"] = "Salt,Pottery,SimpleTools";
        groupedCrisis.Metadata["groupedCount"] = "3";

        Assert.False(_formatter.TryFormat(perMaterialFailure, _focus, out _));
        Assert.True(_formatter.TryFormat(groupedCrisis, _focus, out string chronicleLine));
        Assert.Equal("Year 241 - No material convoy arrived. Stonefen Hearth fell into shortages of salt, pottery, and simple tools.", chronicleLine);
    }

    [Fact]
    public void RepeatedGroupedMaterialCrisis_ForSameSettlementState_IsSuppressed()
    {
        WorldEvent firstCrisis = CreateEvent(
            WorldEventType.MaterialCrisisWorsened,
            WorldEventSeverity.Major,
            year: 242,
            narrative: "Stonefen Hearth's material shortages deepened",
            reason: "grouped_material_crisis_worsened",
            settlementId: 7003,
            settlementName: "Stonefen Hearth");
        firstCrisis.Metadata["groupedMaterials"] = "Pottery,SimpleTools";
        firstCrisis.Metadata["groupedCount"] = "2";

        WorldEvent repeatedCrisis = CreateEvent(
            WorldEventType.MaterialCrisisWorsened,
            WorldEventSeverity.Major,
            year: 243,
            narrative: "Stonefen Hearth's material shortages deepened",
            reason: "grouped_material_crisis_worsened",
            settlementId: 7003,
            settlementName: "Stonefen Hearth");
        repeatedCrisis.Metadata["groupedMaterials"] = "Pottery,SimpleTools";
        repeatedCrisis.Metadata["groupedCount"] = "2";

        Assert.True(_formatter.TryFormat(firstCrisis, _focus, out _));
        Assert.False(_formatter.TryFormat(repeatedCrisis, _focus, out _));
    }

    [Fact]
    public void TradeGoodEstablished_RemainsVisible_AndDedupesForSameSettlement()
    {
        WorldEvent firstEvent = CreateEvent(
            WorldEventType.TradeGoodEstablished,
            WorldEventSeverity.Major,
            year: 243,
            narrative: "Stonefen became known for pottery as a trade good",
            settlementId: 7004,
            settlementName: "Stonefen");
        firstEvent.Metadata["materialType"] = "Pottery";

        WorldEvent repeatedEvent = CreateEvent(
            WorldEventType.TradeGoodEstablished,
            WorldEventSeverity.Major,
            year: 244,
            narrative: "Stonefen became known for pottery as a trade good",
            settlementId: 7004,
            settlementName: "Stonefen");
        repeatedEvent.Metadata["materialType"] = "Pottery";

        Assert.True(_formatter.TryFormat(firstEvent, _focus, out string chronicleLine));
        Assert.Equal("Year 243 - Stonefen became known for pottery as a trade good.", chronicleLine);
        Assert.False(_formatter.TryFormat(repeatedEvent, _focus, out _));
    }

    [Fact]
    public void ExactDuplicateVisibleRecoveryLine_InSameYear_IsSuppressedBySafetyNet()
    {
        WorldEvent firstRecovery = CreateEvent(
            WorldEventType.FamineRelief,
            WorldEventSeverity.Major,
            year: 244,
            narrative: "Gloam Fen Hearth recovered from starvation",
            reason: "settlement_starvation_recovered",
            settlementId: 7005,
            settlementName: "Gloam Fen Hearth");

        WorldEvent duplicateRecovery = CreateEvent(
            WorldEventType.FamineRelief,
            WorldEventSeverity.Major,
            year: 244,
            narrative: "Gloam Fen Hearth recovered from starvation",
            reason: "settlement_starvation_recovered",
            settlementId: 7005,
            settlementName: "Gloam Fen Hearth");

        Assert.True(_formatter.TryFormat(firstRecovery, _focus, out _));
        Assert.False(_formatter.TryFormat(duplicateRecovery, _focus, out _));
    }

    [Fact]
    public void BootstrapEvents_AreSuppressed_ButLiveYearZeroTransitionsRemainVisible()
    {
        WorldEvent bootstrapEvent = CreateEvent(
            WorldEventType.MaterialHighlyValued,
            WorldEventSeverity.Major,
            year: 0,
            narrative: "Simple Tools became highly valued in Stonefen",
            settlementId: 7006,
            settlementName: "Stonefen") with
        {
            SimulationPhase = WorldSimulationPhase.Bootstrap
        };
        bootstrapEvent.Metadata["materialType"] = "SimpleTools";

        WorldEvent liveYearZeroEvent = CreateEvent(
            WorldEventType.MaterialHighlyValued,
            WorldEventSeverity.Major,
            year: 0,
            narrative: "Simple Tools became highly valued in Stonefen",
            settlementId: 7006,
            settlementName: "Stonefen");
        liveYearZeroEvent.Metadata["materialType"] = "SimpleTools";

        Assert.False(_formatter.TryFormat(bootstrapEvent, _focus, out _));
        Assert.True(_formatter.TryFormat(liveYearZeroEvent, _focus, out string chronicleLine));
        Assert.Equal("Year 0 - Simple Tools became highly valued in Stonefen.", chronicleLine);
    }

    private static WorldEvent CreateEvent(
        string type,
        WorldEventSeverity severity,
        int year,
        string narrative,
        int polityId = 7,
        int? relatedPolityId = null,
        string? reason = null,
        int? settlementId = null,
        string? settlementName = null)
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
            SettlementId = settlementId,
            SettlementName = settlementName,
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

    private static WorldEvent CreateAdaptationEvent(
        int year,
        int milestone,
        string narrative,
        WorldEventSeverity severity = WorldEventSeverity.Major)
    {
        return new WorldEvent
        {
            EventId = year,
            Year = year,
            Month = 12,
            Season = Season.Winter,
            Type = WorldEventType.SpeciesPopulationAdaptedToRegion,
            Severity = severity,
            Narrative = narrative,
            Reason = "sustained_habitat_mismatch",
            RelatedPolityId = 7,
            RelatedPolityName = "River Clan",
            SpeciesId = 6,
            SpeciesName = "Ashfang Wolf",
            RegionId = 2,
            RegionName = "Amber Reach",
            Metadata = new Dictionary<string, string>
            {
                ["adaptationMilestone"] = milestone.ToString(),
                ["adaptationStage"] = milestone >= 2 ? "strong_adaptation" : "regional_adaptation",
                ["adaptationSignal"] = "climate_tolerance"
            }
        };
    }

    private static WorldEvent CreateEvolutionEvent(int year, int regionId, string regionName, int milestone, string narrative)
    {
        return new WorldEvent
        {
            EventId = year,
            Year = year,
            Month = 12,
            Season = Season.Winter,
            Type = WorldEventType.SpeciesPopulationEvolutionaryTurningPoint,
            Severity = WorldEventSeverity.Major,
            Narrative = narrative,
            Reason = "accumulated_divergence",
            RelatedPolityId = 7,
            RelatedPolityName = "River Clan",
            SpeciesId = 6,
            SpeciesName = "Ashfang Wolf",
            RegionId = regionId,
            RegionName = regionName,
            Metadata = new Dictionary<string, string>
            {
                ["milestone"] = milestone.ToString()
            }
        };
    }

    private static WorldEvent CreateMigrationEvent(int year, int fromRegionId, int toRegionId, string toRegionName, string narrative)
    {
        return new WorldEvent
        {
            EventId = year,
            Year = year,
            Month = 12,
            Season = Season.Winter,
            Type = WorldEventType.Migration,
            Severity = WorldEventSeverity.Major,
            Narrative = narrative,
            Reason = "migration_pressure",
            PolityId = 7,
            PolityName = "River Clan",
            RegionId = toRegionId,
            RegionName = toRegionName,
            Before = new Dictionary<string, string>
            {
                ["regionId"] = fromRegionId.ToString()
            },
            After = new Dictionary<string, string>
            {
                ["regionId"] = toRegionId.ToString()
            }
        };
    }
}
