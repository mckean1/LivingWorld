using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class DomesticationPropagationHandler : IWorldEventHandler
{
    public bool CanHandle(WorldEvent worldEvent)
        => worldEvent.Type is
            WorldEventType.AnimalDomesticated or
            WorldEventType.CropEstablished or
            WorldEventType.AgricultureStabilizedFoodSupply;

    public IEnumerable<WorldEvent> Handle(World world, WorldEvent worldEvent)
    {
        if (worldEvent.PolityId is not int polityId)
        {
            yield break;
        }

        Polity? polity = world.Polities.FirstOrDefault(candidate => candidate.Id == polityId);
        if (polity is null)
        {
            yield break;
        }

        if (worldEvent.Type is WorldEventType.AnimalDomesticated or WorldEventType.CropEstablished)
        {
            polity.EventDrivenSettlementChanceBonus = Math.Max(polity.EventDrivenSettlementChanceBonus, 0.06);
            polity.SettlementChanceBonusMonthsRemaining = Math.Max(polity.SettlementChanceBonusMonthsRemaining, 18);

            yield return new WorldEvent
            {
                Type = WorldEventType.SettlementStabilized,
                Severity = WorldEventSeverity.Notable,
                Scope = WorldEventScope.Local,
                Narrative = $"{polity.Name}'s settlements grew more stable through managed food",
                Details = $"{polity.Name} gained a more reliable managed food source.",
                Reason = "managed_food_supported_settlement",
                PolityId = polity.Id,
                PolityName = polity.Name,
                SpeciesId = polity.SpeciesId,
                SpeciesName = world.Species.First(species => species.Id == polity.SpeciesId).Name,
                RegionId = worldEvent.RegionId,
                RegionName = worldEvent.RegionName,
                SettlementId = worldEvent.SettlementId,
                SettlementName = worldEvent.SettlementName
            };

            yield break;
        }

        polity.EventDrivenMigrationPressureBonus = Math.Max(0, polity.EventDrivenMigrationPressureBonus - 0.18);
        polity.MigrationPressureBonusMonthsRemaining = Math.Min(polity.MigrationPressureBonusMonthsRemaining, 6);

        yield return new WorldEvent
        {
            Type = WorldEventType.FoodStabilized,
            Severity = worldEvent.Severity,
            Scope = WorldEventScope.Polity,
            Narrative = $"{polity.Name} stabilized through managed food",
            Details = $"{polity.Name} drew enough reliable food from crops and herds to ease migration pressure.",
            Reason = "managed_food_stabilized_supply",
            PolityId = polity.Id,
            PolityName = polity.Name,
            SpeciesId = polity.SpeciesId,
            SpeciesName = world.Species.First(species => species.Id == polity.SpeciesId).Name,
            RegionId = polity.RegionId,
            RegionName = world.Regions.First(region => region.Id == polity.RegionId).Name,
            After = new Dictionary<string, string>
            {
                ["hardshipTier"] = "Stable"
            }
        };
    }
}
