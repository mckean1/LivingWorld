using LivingWorld.Core;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class MigrationPropagationHandler : IWorldEventHandler
{
    public bool CanHandle(WorldEvent worldEvent)
        => worldEvent.Type is WorldEventType.Migration or WorldEventType.MigrationPressure;

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

        if (worldEvent.Type == WorldEventType.MigrationPressure)
        {
            if (polity.FragmentationPressure >= 0.45 || polity.EventDrivenFragmentationPressureBonus >= 0.10)
            {
                yield return new WorldEvent
                {
                    Type = WorldEventType.SchismRisk,
                    Severity = WorldEventSeverity.Notable,
                    Scope = WorldEventScope.Polity,
                    Narrative = $"{polity.Name} came under schism pressure",
                    Details = $"{polity.Name} carried both migration pressure and internal strain.",
                    Reason = "migration_pressure_schism_risk",
                    PolityId = polity.Id,
                    PolityName = polity.Name,
                    RegionId = polity.RegionId,
                    RegionName = world.Regions.First(region => region.Id == polity.RegionId).Name
                };
            }

            yield break;
        }

        Region destination = world.Regions.First(region => region.Id == polity.RegionId);
        int localPopulation = world.Polities
            .Where(candidate => candidate.RegionId == destination.Id && candidate.Population > 0)
            .Sum(candidate => candidate.Population);
        double crowdingRatio = destination.CarryingCapacity <= 0
            ? 1.0
            : localPopulation / destination.CarryingCapacity;

        polity.EventDrivenSettlementChanceBonus = Math.Max(
            polity.EventDrivenSettlementChanceBonus,
            polity.Capabilities.CanFarm ? 0.12 : 0.06);
        polity.SettlementChanceBonusMonthsRemaining = Math.Max(polity.SettlementChanceBonusMonthsRemaining, 18);
        polity.EventDrivenMigrationPressureBonus = 0;
        polity.MigrationPressureBonusMonthsRemaining = 0;

        if (crowdingRatio >= 0.90)
        {
            yield return new WorldEvent
            {
                Type = WorldEventType.LocalTension,
                Severity = WorldEventSeverity.Notable,
                Scope = WorldEventScope.Local,
                Narrative = $"{polity.Name}'s arrival raised local tension in {destination.Name}",
                Details = $"{destination.Name} was already crowded when {polity.Name} arrived.",
                Reason = "migration_arrival_crowding",
                PolityId = polity.Id,
                PolityName = polity.Name,
                RegionId = destination.Id,
                RegionName = destination.Name
            };
        }
    }
}
