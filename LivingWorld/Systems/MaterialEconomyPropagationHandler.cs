using LivingWorld.Core;
using LivingWorld.Economy;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class MaterialEconomyPropagationHandler : IWorldEventHandler
{
    public bool CanHandle(WorldEvent worldEvent)
        => worldEvent.Type is
            WorldEventType.PreservationEstablished or
            WorldEventType.ToolmakingEstablished or
            WorldEventType.MaterialConvoySent or
            WorldEventType.SettlementSpecialized or
            WorldEventType.TradeGoodEstablished or
            WorldEventType.ProductionBottleneckHit;

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

        string speciesName = world.Species.First(species => species.Id == polity.SpeciesId).Name;
        string regionName = world.Regions.First(region => region.Id == polity.RegionId).Name;

        if (worldEvent.Type == WorldEventType.PreservationEstablished)
        {
            polity.EventDrivenMigrationPressureBonus = Math.Max(0.0, polity.EventDrivenMigrationPressureBonus - 0.10);
            polity.MigrationPressureBonusMonthsRemaining = Math.Min(polity.MigrationPressureBonusMonthsRemaining, 6);

            yield return new WorldEvent
            {
                Type = WorldEventType.FoodStabilized,
                Severity = WorldEventSeverity.Notable,
                Scope = WorldEventScope.Polity,
                Narrative = $"{polity.Name} gained a stronger seasonal buffer",
                Details = $"{polity.Name} established preserved food stores that softened later shortages.",
                Reason = "preserved_food_buffered_supply",
                PolityId = polity.Id,
                PolityName = polity.Name,
                SpeciesId = polity.SpeciesId,
                SpeciesName = speciesName,
                RegionId = polity.RegionId,
                RegionName = regionName,
                SettlementId = worldEvent.SettlementId,
                SettlementName = worldEvent.SettlementName,
                Metadata = new Dictionary<string, string>
                {
                    ["materialType"] = MaterialType.PreservedFood.ToString()
                }
            };

            yield break;
        }

        if (worldEvent.Type == WorldEventType.ToolmakingEstablished)
        {
            polity.EventDrivenSettlementChanceBonus = Math.Max(polity.EventDrivenSettlementChanceBonus, 0.05);
            polity.SettlementChanceBonusMonthsRemaining = Math.Max(polity.SettlementChanceBonusMonthsRemaining, 12);

            yield return new WorldEvent
            {
                Type = WorldEventType.SettlementStabilized,
                Severity = WorldEventSeverity.Notable,
                Scope = WorldEventScope.Local,
                Narrative = $"{polity.Name}'s settlements grew more reliable through toolmaking",
                Details = $"{polity.Name} gained steadier tools for cultivation, extraction, and hunting.",
                Reason = "toolmaking_supported_settlement",
                PolityId = polity.Id,
                PolityName = polity.Name,
                SpeciesId = polity.SpeciesId,
                SpeciesName = speciesName,
                RegionId = worldEvent.RegionId ?? polity.RegionId,
                RegionName = worldEvent.RegionName ?? regionName,
                SettlementId = worldEvent.SettlementId,
                SettlementName = worldEvent.SettlementName,
                Metadata = new Dictionary<string, string>
                {
                    ["materialType"] = MaterialType.SimpleTools.ToString()
                }
            };

            yield break;
        }

        if (worldEvent.Type == WorldEventType.MaterialConvoySent
            && worldEvent.Metadata.TryGetValue("materialType", out string? materialType)
            && materialType is nameof(MaterialType.PreservedFood) or nameof(MaterialType.Pottery) or nameof(MaterialType.SimpleTools)
            && worldEvent.Reason == "critical_material_relief")
        {
            yield return new WorldEvent
            {
                Type = WorldEventType.FoodStabilized,
                Severity = WorldEventSeverity.Notable,
                Scope = WorldEventScope.Local,
                Narrative = $"{worldEvent.SettlementName} regained breathing room after material relief",
                Details = $"{worldEvent.SettlementName} received critical {MaterialEconomySystem.GetMaterialLabel(Enum.Parse<MaterialType>(materialType)).ToLowerInvariant()} from within {polity.Name}.",
                Reason = "critical_material_relief_supported_food",
                PolityId = polity.Id,
                PolityName = polity.Name,
                SpeciesId = polity.SpeciesId,
                SpeciesName = speciesName,
                RegionId = worldEvent.RegionId,
                RegionName = worldEvent.RegionName,
                SettlementId = worldEvent.SettlementId,
                SettlementName = worldEvent.SettlementName,
                Metadata = new Dictionary<string, string>
                {
                    ["materialType"] = materialType
                }
            };

            yield break;
        }

        if (worldEvent.Type == WorldEventType.SettlementSpecialized)
        {
            polity.EventDrivenSettlementChanceBonus = Math.Max(polity.EventDrivenSettlementChanceBonus, 0.03);
            polity.SettlementChanceBonusMonthsRemaining = Math.Max(polity.SettlementChanceBonusMonthsRemaining, 8);
            yield break;
        }

        if (worldEvent.Type == WorldEventType.TradeGoodEstablished)
        {
            polity.EventDrivenSettlementChanceBonus = Math.Max(polity.EventDrivenSettlementChanceBonus, 0.04);
            polity.SettlementChanceBonusMonthsRemaining = Math.Max(polity.SettlementChanceBonusMonthsRemaining, 10);
            yield break;
        }

        if (worldEvent.Type == WorldEventType.ProductionBottleneckHit)
        {
            polity.EventDrivenMigrationPressureBonus = Math.Max(polity.EventDrivenMigrationPressureBonus, 0.03);
            polity.MigrationPressureBonusMonthsRemaining = Math.Max(polity.MigrationPressureBonusMonthsRemaining, 4);
        }
    }
}
