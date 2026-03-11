using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class FoodStressPropagationHandler : IWorldEventHandler
{
    public bool CanHandle(WorldEvent worldEvent)
        => worldEvent.Type is WorldEventType.FoodStress or WorldEventType.TradeRelief;

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

        if (worldEvent.Type == WorldEventType.TradeRelief && worldEvent.Reason is "trade_full_relief" or "annual_internal_trade_stability")
        {
            polity.EventDrivenMigrationPressureBonus = 0;
            polity.MigrationPressureBonusMonthsRemaining = 0;

            yield return new WorldEvent
            {
                Type = WorldEventType.FoodStabilized,
                Severity = WorldEventSeverity.Notable,
                Scope = WorldEventScope.Polity,
                Narrative = $"{polity.Name} stabilized after food relief",
                Details = $"{polity.Name} regained breathing room through food relief.",
                Reason = "trade_relief_stabilized_food",
                PolityId = polity.Id,
                PolityName = polity.Name,
                RelatedPolityId = worldEvent.RelatedPolityId,
                RelatedPolityName = worldEvent.RelatedPolityName,
                RegionId = polity.RegionId,
                RegionName = world.Regions.First(region => region.Id == polity.RegionId).Name
            };

            yield break;
        }

        string hardshipTier = GetValue(worldEvent.After, "hardshipTier");
        string transitionKind = worldEvent.Reason ?? GetValue(worldEvent.Metadata, "transitionKind");
        string regionName = world.Regions.First(region => region.Id == polity.RegionId).Name;

        if (transitionKind is "hardship_entered" or "hardship_worsened")
        {
            double migrationBonus = hardshipTier switch
            {
                "Famine" => 0.35,
                "Crisis" => 0.25,
                "Shortages" => 0.18,
                _ => 0.0
            };

            if (migrationBonus > 0)
            {
                polity.EventDrivenMigrationPressureBonus = Math.Max(polity.EventDrivenMigrationPressureBonus, migrationBonus);
                polity.MigrationPressureBonusMonthsRemaining = Math.Max(polity.MigrationPressureBonusMonthsRemaining, 18);

                yield return new WorldEvent
                {
                    Type = WorldEventType.MigrationPressure,
                    Severity = hardshipTier == "Famine" ? WorldEventSeverity.Major : WorldEventSeverity.Notable,
                    Scope = WorldEventScope.Polity,
                    Narrative = $"{polity.Name} came under migration pressure",
                    Details = $"{polity.Name} faced rising migration pressure after entering {hardshipTier.ToLowerInvariant()} in {regionName}.",
                    Reason = "food_stress_pressure",
                    PolityId = polity.Id,
                    PolityName = polity.Name,
                    RegionId = polity.RegionId,
                    RegionName = regionName,
                    After = new Dictionary<string, string>
                    {
                        ["migrationPressureBonus"] = polity.EventDrivenMigrationPressureBonus.ToString("F2")
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        ["triggerHardshipTier"] = hardshipTier
                    }
                };
            }

            if (hardshipTier is "Crisis" or "Famine")
            {
                polity.EventDrivenFragmentationPressureBonus = Math.Max(
                    polity.EventDrivenFragmentationPressureBonus,
                    hardshipTier == "Famine" ? 0.22 : 0.12);
                polity.FragmentationPressureBonusMonthsRemaining = Math.Max(
                    polity.FragmentationPressureBonusMonthsRemaining,
                    18);

                yield return new WorldEvent
                {
                    Type = WorldEventType.StarvationRisk,
                    Severity = hardshipTier == "Famine" ? WorldEventSeverity.Major : WorldEventSeverity.Notable,
                    Scope = WorldEventScope.Polity,
                    Narrative = hardshipTier == "Famine"
                        ? $"{polity.Name} faced catastrophic starvation"
                        : $"{polity.Name} faced worsening starvation risk",
                    Details = $"{polity.Name} entered {hardshipTier.ToLowerInvariant()} with starvation pressure still active.",
                    Reason = "food_stress_starvation_risk",
                    PolityId = polity.Id,
                    PolityName = polity.Name,
                    RegionId = polity.RegionId,
                    RegionName = regionName,
                    Metadata = new Dictionary<string, string>
                    {
                        ["triggerHardshipTier"] = hardshipTier
                    }
                };
            }

            yield break;
        }

        if (transitionKind is "hardship_improved" or "hardship_recovered")
        {
            polity.EventDrivenMigrationPressureBonus = 0;
            polity.MigrationPressureBonusMonthsRemaining = 0;

            if (transitionKind == "hardship_recovered")
            {
                polity.EventDrivenFragmentationPressureBonus = Math.Max(0, polity.EventDrivenFragmentationPressureBonus - 0.08);
                polity.FragmentationPressureBonusMonthsRemaining = Math.Min(polity.FragmentationPressureBonusMonthsRemaining, 6);
            }

            yield return new WorldEvent
            {
                Type = WorldEventType.FoodStabilized,
                Severity = transitionKind == "hardship_recovered" ? WorldEventSeverity.Major : WorldEventSeverity.Notable,
                Scope = WorldEventScope.Polity,
                Narrative = transitionKind == "hardship_recovered"
                    ? $"{polity.Name} stabilized after hardship"
                    : $"{polity.Name}'s food stability improved",
                Details = $"{polity.Name} moved from {GetValue(worldEvent.Before, "hardshipTier")} to {GetValue(worldEvent.After, "hardshipTier")}.",
                Reason = transitionKind == "hardship_recovered"
                    ? "food_recovered"
                    : "food_improved",
                PolityId = polity.Id,
                PolityName = polity.Name,
                RegionId = polity.RegionId,
                RegionName = regionName
            };
        }
    }

    private static string GetValue(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out string? value) ? value : string.Empty;
}
