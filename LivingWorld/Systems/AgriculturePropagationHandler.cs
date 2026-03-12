using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class AgriculturePropagationHandler : IWorldEventHandler
{
    public bool CanHandle(WorldEvent worldEvent)
        => worldEvent.Type is WorldEventType.LearnedAdvancement or WorldEventType.CultivationExpanded;

    public IEnumerable<WorldEvent> Handle(World world, WorldEvent worldEvent)
    {
        WorldLookup lookup = new(world);

        if (worldEvent.PolityId is not int polityId)
        {
            yield break;
        }

        Polity? polity = world.Polities.FirstOrDefault(candidate => candidate.Id == polityId);
        if (polity is null)
        {
            yield break;
        }

        if (!lookup.TryGetRegion(polity.RegionId, out Map.Region? region) || region is null)
        {
            yield break;
        }

        string regionName = region.Name;
        string? speciesName = lookup.TryGetSpecies(polity.SpeciesId, out Life.Species? species)
            && species is not null
            ? species.Name
            : null;
        Settlement? settlement = polity.GetPrimarySettlementInRegion(region.Id) ?? polity.GetPrimarySettlement();

        if (worldEvent.Type == WorldEventType.LearnedAdvancement
            && worldEvent.Metadata.TryGetValue("advancementId", out string? advancementId)
            && advancementId == AdvancementId.Agriculture.ToString())
        {
            polity.LastLearnedAgricultureEventId = worldEvent.EventId;
            polity.EventDrivenSettlementChanceBonus = Math.Max(polity.EventDrivenSettlementChanceBonus, 0.10);
            polity.SettlementChanceBonusMonthsRemaining = Math.Max(polity.SettlementChanceBonusMonthsRemaining, 24);

            yield return new WorldEvent
            {
                Type = WorldEventType.CultivationExpanded,
                Severity = WorldEventSeverity.Notable,
                Scope = WorldEventScope.Local,
                Narrative = $"{polity.Name} began preparing fields in {regionName}",
                Details = $"{polity.Name} learned Agriculture and began shifting labor toward cultivated land.",
                Reason = "agriculture_learned_fields_prepared",
                PolityId = polity.Id,
                PolityName = polity.Name,
                SpeciesId = polity.SpeciesId,
                SpeciesName = speciesName,
                RegionId = polity.RegionId,
                RegionName = regionName,
                SettlementId = settlement?.Id,
                SettlementName = settlement?.Name
            };

            yield break;
        }

        if (worldEvent.Type == WorldEventType.CultivationExpanded
            && polity.HasSettlements
            && polity.EventDrivenSettlementChanceBonus > 0
            && polity.SettlementStatus != SettlementStatus.Nomadic)
        {
            yield return new WorldEvent
            {
                Type = WorldEventType.SettlementStabilized,
                Severity = WorldEventSeverity.Notable,
                Scope = WorldEventScope.Local,
                Narrative = $"{polity.Name}'s settlements grew more stable in {regionName}",
                Details = $"{polity.Name} expanded cultivation and improved the reliability of its settlements in {regionName}.",
                Reason = "cultivation_supported_settlement",
                PolityId = polity.Id,
                PolityName = polity.Name,
                SpeciesId = polity.SpeciesId,
                SpeciesName = speciesName,
                RegionId = polity.RegionId,
                RegionName = regionName,
                SettlementId = settlement?.Id,
                SettlementName = settlement?.Name
            };
        }
    }
}
