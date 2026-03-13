using LivingWorld.Core;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class SettlementFoodRedistributionSystem
{
    private const double ExportShareLimitPerMonth = 0.25;
    private const double TransportLossPerHop = 0.05;
    private const double SignificantAidShare = 0.20;
    private const double LegendaryReliefShare = 0.65;

    public void UpdateMonthlyFoodStatesAndRedistribution(World world)
    {
        WorldLookup lookup = new(world);

        foreach (Polity polity in world.Polities)
        {
            PrepareSettlementFoodStates(lookup, polity);
        }

        foreach (Polity polity in world.Polities.Where(candidate => candidate.Population > 0 && candidate.SettlementCount > 1))
        {
            RedistributeWithinPolity(world, lookup, polity);
        }

        foreach (Polity polity in world.Polities)
        {
            foreach (Settlement settlement in polity.Settlements)
            {
                EvaluateSettlementStarvationTransition(world, lookup, polity, settlement);
            }
        }
    }

    public void InitializeBootstrapStates(World world)
    {
        WorldLookup lookup = new(world);

        foreach (Polity polity in world.Polities)
        {
            if (polity.FoodNeededThisMonth <= 0)
            {
                polity.FoodNeededThisMonth = polity.Population * polity.Capabilities.FoodNeedMultiplier;
            }

            PrepareSettlementFoodStates(lookup, polity);
            foreach (Settlement settlement in polity.Settlements)
            {
                settlement.LastRecordedStarvationStage = settlement.StarvationStage;
            }
        }
    }

    private static void PrepareSettlementFoodStates(WorldLookup lookup, Polity polity)
    {
        if (polity.SettlementCount == 0)
        {
            return;
        }

        IReadOnlyList<Settlement> settlements = polity.Settlements;
        Dictionary<int, double> needWeights = settlements.ToDictionary(
            settlement => settlement.Id,
            ResolveNeedWeight);
        Dictionary<int, double> forageWeights = settlements.ToDictionary(
            settlement => settlement.Id,
            settlement => ResolveForageWeight(lookup, settlement));
        Dictionary<int, double> farmWeights = settlements.ToDictionary(
            settlement => settlement.Id,
            settlement => Math.Max(0, settlement.CultivatedLand));

        double totalNeedWeight = Math.Max(0.0001, needWeights.Values.Sum());
        double totalForageWeight = Math.Max(0.0001, forageWeights.Values.Sum());
        double totalFarmWeight = farmWeights.Values.Sum();

        foreach (Settlement settlement in settlements)
        {
            double shareByNeed = needWeights[settlement.Id] / totalNeedWeight;
            double shareByForage = forageWeights[settlement.Id] / totalForageWeight;
            double shareByFarm = totalFarmWeight <= 0
                ? shareByNeed
                : farmWeights[settlement.Id] / totalFarmWeight;

            settlement.LastAidReceived = 0;
            settlement.LastAidSent = 0;
            settlement.FoodRequired = polity.FoodNeededThisMonth * shareByNeed;
            settlement.FoodProduced =
                (polity.FoodGatheredThisMonth * shareByForage)
                + (polity.FoodFarmedThisMonth * shareByFarm);
            settlement.FoodStored = polity.FoodStores * shareByNeed;
            settlement.CalculateFoodState();
        }
    }

    private static void RedistributeWithinPolity(World world, WorldLookup lookup, Polity polity)
    {
        List<Settlement> senders = polity.Settlements
            .Where(settlement => settlement.FoodState == FoodState.Surplus && settlement.FoodBalance > 0)
            .OrderByDescending(settlement => settlement.FoodBalance)
            .ToList();
        Dictionary<int, double> senderExportBudget = senders.ToDictionary(
            settlement => settlement.Id,
            settlement => Math.Max(0, settlement.FoodBalance) * ExportShareLimitPerMonth);
        List<Settlement> receivers = polity.Settlements
            .Where(settlement => settlement.FoodState is FoodState.Deficit or FoodState.Starving)
            .OrderByDescending(settlement => settlement.FoodState == FoodState.Starving)
            .ThenByDescending(settlement => settlement.RequestFoodAid())
            .ToList();

        if (senders.Count == 0 || receivers.Count == 0)
        {
            return;
        }

        foreach (Settlement receiver in receivers)
        {
            List<Settlement> candidateSenders = senders
                .Where(sender => sender.Id != receiver.Id && sender.FoodState == FoodState.Surplus && sender.FoodBalance > 0)
                .OrderBy(sender => ResolvePriorityBucket(lookup, sender.RegionId, receiver.RegionId))
                .ThenBy(sender => ResolveRegionDistance(lookup, sender.RegionId, receiver.RegionId))
                .ThenByDescending(sender => sender.FoodBalance)
                .ToList();

            foreach (Settlement sender in candidateSenders)
            {
                double deficitNeeded = receiver.RequestFoodAid();
                if (deficitNeeded <= 0)
                {
                    break;
                }

                double senderSurplus = Math.Max(0, sender.FoodBalance);
                if (senderSurplus <= 0)
                {
                    continue;
                }

                double remainingExportBudget = senderExportBudget.TryGetValue(sender.Id, out double budget)
                    ? budget
                    : 0.0;
                if (remainingExportBudget <= 0.01)
                {
                    continue;
                }

                double plannedTransfer = Math.Min(Math.Min(senderSurplus, remainingExportBudget), deficitNeeded);
                if (plannedTransfer <= 0.01)
                {
                    continue;
                }

                FoodState receiverStateBefore = receiver.FoodState;
                double needBefore = deficitNeeded;
                int distance = ResolveRegionDistance(lookup, sender.RegionId, receiver.RegionId);
                double transportLoss = Math.Clamp(distance * TransportLossPerHop, 0.0, 0.95);
                double shippedFood = sender.SendFoodAid(plannedTransfer);
                if (shippedFood <= 0)
                {
                    continue;
                }

                senderExportBudget[sender.Id] = Math.Max(0.0, remainingExportBudget - shippedFood);

                double receivedFood = shippedFood * (1.0 - transportLoss);
                receiver.FoodStored += receivedFood;
                receiver.LastAidReceived += receivedFood;
                receiver.AidReceivedThisYear += receivedFood;
                receiver.CalculateFoodState();

                EmitAidEvents(
                    world,
                    lookup,
                    polity,
                    sender,
                    receiver,
                    receiverStateBefore,
                    needBefore,
                    shippedFood,
                    receivedFood,
                    transportLoss,
                    distance);
            }
        }
    }

    private static void EmitAidEvents(
        World world,
        WorldLookup lookup,
        Polity polity,
        Settlement sender,
        Settlement receiver,
        FoodState receiverStateBefore,
        double receiverNeedBefore,
        double shippedFood,
        double receivedFood,
        double transportLoss,
        int distance)
    {
        double receiverNeedShare = receiver.FoodRequired <= 0
            ? 0.0
            : receivedFood / receiver.FoodRequired;
        bool starvingSettlementReceivedAid = receiverStateBefore == FoodState.Starving && receivedFood > 0;
        bool largeTransfer = receiverNeedShare > SignificantAidShare;
        bool starvationPrevented = receiverStateBefore == FoodState.Starving && receiver.FoodState != FoodState.Starving;

        if (starvingSettlementReceivedAid || largeTransfer)
        {
            Region receiverRegion = lookup.GetRequiredRegion(receiver.RegionId, "Settlement food aid");
            WorldEventSeverity severity = largeTransfer || starvationPrevented
                ? WorldEventSeverity.Major
                : WorldEventSeverity.Minor;
            world.AddEvent(
                WorldEventType.FoodAidSent,
                severity,
                $"{sender.Name} sent grain to {receiver.Name}",
                $"{sender.Name} shipped {shippedFood:F1} food to {receiver.Name}; {receivedFood:F1} arrived after {transportLoss:P0} loss.",
                reason: starvationPrevented
                    ? "starvation_relief_convoy"
                    : starvingSettlementReceivedAid
                        ? "starving_settlement_received_aid"
                        : "large_internal_food_transfer",
                scope: WorldEventScope.Local,
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: polity.SpeciesId,
                speciesName: lookup.GetRequiredSpecies(polity.SpeciesId, "Settlement food aid").Name,
                regionId: receiverRegion.Id,
                regionName: receiverRegion.Name,
                settlementId: receiver.Id,
                settlementName: receiver.Name,
                before: new Dictionary<string, string>
                {
                    ["foodState"] = receiverStateBefore.ToString(),
                    ["aidNeed"] = receiverNeedBefore.ToString("F1")
                },
                after: new Dictionary<string, string>
                {
                    ["foodState"] = receiver.FoodState.ToString(),
                    ["aidReceived"] = receivedFood.ToString("F1")
                },
                metadata: new Dictionary<string, string>
                {
                    ["type"] = WorldEventType.FoodAidSent,
                    ["location"] = receiverRegion.Name,
                    ["actors"] = $"{sender.Name}|{receiver.Name}",
                    ["cause"] = "intra_polity_food_redistribution",
                    ["severity"] = severity.ToString(),
                    ["senderSettlementId"] = sender.Id.ToString(),
                    ["senderSettlementName"] = sender.Name,
                    ["distance"] = distance.ToString(),
                    ["transportLoss"] = transportLoss.ToString("F2")
                });
        }

        if (starvationPrevented)
        {
            WorldEventSeverity severity = receiverNeedShare >= LegendaryReliefShare
                ? WorldEventSeverity.Legendary
                : WorldEventSeverity.Major;
            Region receiverRegion = lookup.GetRequiredRegion(receiver.RegionId, "Settlement famine relief");

            world.AddEvent(
                WorldEventType.FamineRelief,
                severity,
                $"Food caravans arrived from {sender.Name}, relieving famine in {receiver.Name}",
                $"{sender.Name} shipped {shippedFood:F1} food to {receiver.Name}; {receivedFood:F1} arrived after {transportLoss:P0} loss.",
                reason: "settlement_starvation_prevented",
                scope: WorldEventScope.Local,
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: polity.SpeciesId,
                speciesName: lookup.GetRequiredSpecies(polity.SpeciesId, "Settlement famine relief").Name,
                regionId: receiverRegion.Id,
                regionName: receiverRegion.Name,
                settlementId: receiver.Id,
                settlementName: receiver.Name,
                before: new Dictionary<string, string>
                {
                    ["foodState"] = receiverStateBefore.ToString(),
                    ["aidNeed"] = receiverNeedBefore.ToString("F1")
                },
                after: new Dictionary<string, string>
                {
                    ["foodState"] = receiver.FoodState.ToString(),
                    ["aidReceived"] = receivedFood.ToString("F1")
                },
                metadata: new Dictionary<string, string>
                {
                    ["type"] = WorldEventType.FamineRelief,
                    ["location"] = receiverRegion.Name,
                    ["actors"] = $"{sender.Name}|{receiver.Name}",
                    ["cause"] = "intra_polity_food_redistribution",
                    ["severity"] = severity.ToString(),
                    ["senderSettlementId"] = sender.Id.ToString(),
                    ["senderSettlementName"] = sender.Name,
                    ["distance"] = distance.ToString(),
                    ["transportLoss"] = transportLoss.ToString("F2")
                });
        }
    }

    private static void EmitAidFailedEvent(World world, WorldLookup lookup, Polity polity, Settlement settlement)
        => EmitAidFailedEvent(
            world,
            lookup,
            polity,
            settlement,
            settlement.StarvationStage,
            settlement.LastAidReceived <= 0
                ? $"No aid arrived. {settlement.Name} began to starve"
                : $"{settlement.Name} began to starve",
            settlement.LastAidReceived <= 0
                ? $"{settlement.Name} entered a starving state after polity redistribution failed to reach it."
                : $"{settlement.Name} entered a starving state despite receiving only partial relief.",
            settlement.LastAidReceived <= 0
                ? "settlement_starvation_began_unaided"
                : "settlement_starvation_began");

    private static void EvaluateSettlementStarvationTransition(World world, WorldLookup lookup, Polity polity, Settlement settlement)
    {
        SettlementStarvationStage previousStage = settlement.LastRecordedStarvationStage;
        SettlementStarvationStage currentStage = settlement.StarvationStage;

        if (currentStage == previousStage)
        {
            return;
        }

        // Settlement aid failure used to emit on every monthly starving tick.
        // Track starvation stage explicitly so we only emit on meaningful changes.
        if (currentStage == SettlementStarvationStage.None)
        {
            if (previousStage != SettlementStarvationStage.None && settlement.LastAidReceived <= 0)
            {
                EmitStarvationRecoveryEvent(world, lookup, polity, settlement, previousStage);
            }

            settlement.LastRecordedStarvationStage = currentStage;
            return;
        }

        if (previousStage == SettlementStarvationStage.None)
        {
            EmitAidFailedEvent(world, lookup, polity, settlement);
            settlement.LastRecordedStarvationStage = currentStage;
            return;
        }

        if (currentStage > previousStage)
        {
            EmitAidFailedEvent(
                world,
                lookup,
                polity,
                settlement,
                currentStage,
                settlement.LastAidReceived <= 0
                    ? $"No aid arrived. Starvation worsened in {settlement.Name}"
                    : $"Starvation worsened in {settlement.Name}",
                settlement.LastAidReceived <= 0
                    ? $"{settlement.Name} fell deeper into starvation after no aid reached it."
                    : $"{settlement.Name} fell deeper into starvation despite partial relief.",
                settlement.LastAidReceived <= 0
                    ? "settlement_starvation_worsened_unaided"
                    : "settlement_starvation_worsened");
        }

        settlement.LastRecordedStarvationStage = currentStage;
    }

    private static void EmitAidFailedEvent(
        World world,
        WorldLookup lookup,
        Polity polity,
        Settlement settlement,
        SettlementStarvationStage starvationStage,
        string narrative,
        string details,
        string reason)
    {
        Region region = lookup.GetRequiredRegion(settlement.RegionId, "Settlement aid failure");
        world.AddEvent(
            WorldEventType.AidFailed,
            starvationStage >= SettlementStarvationStage.Severe
                ? WorldEventSeverity.Legendary
                : WorldEventSeverity.Major,
            narrative,
            details,
            reason: reason,
            scope: WorldEventScope.Local,
            polityId: polity.Id,
            polityName: polity.Name,
            speciesId: polity.SpeciesId,
            speciesName: lookup.GetRequiredSpecies(polity.SpeciesId, "Settlement aid failure").Name,
            regionId: region.Id,
            regionName: region.Name,
            settlementId: settlement.Id,
            settlementName: settlement.Name,
            after: new Dictionary<string, string>
            {
                ["foodState"] = settlement.FoodState.ToString(),
                ["starvationStage"] = starvationStage.ToString(),
                ["aidReceived"] = settlement.LastAidReceived.ToString("F1")
            },
            metadata: new Dictionary<string, string>
            {
                ["type"] = WorldEventType.AidFailed,
                ["location"] = region.Name,
                ["actors"] = settlement.Name,
                ["cause"] = "intra_polity_food_redistribution_failed",
                ["severity"] = starvationStage >= SettlementStarvationStage.Severe
                    ? WorldEventSeverity.Legendary.ToString()
                    : WorldEventSeverity.Major.ToString(),
                ["starvationStage"] = starvationStage.ToString()
            });
    }

    private static void EmitStarvationRecoveryEvent(
        World world,
        WorldLookup lookup,
        Polity polity,
        Settlement settlement,
        SettlementStarvationStage previousStage)
    {
        if (settlement.LastStarvationRecoveryYear == world.Time.Year)
        {
            return;
        }

        Region region = lookup.GetRequiredRegion(settlement.RegionId, "Settlement starvation recovery");
        world.AddEvent(
            WorldEventType.FamineRelief,
            previousStage >= SettlementStarvationStage.Severe
                ? WorldEventSeverity.Legendary
                : WorldEventSeverity.Major,
            $"{settlement.Name} recovered from starvation",
            $"{settlement.Name} moved out of starvation without a new aid convoy in the current month.",
            reason: "settlement_starvation_recovered",
            scope: WorldEventScope.Local,
            polityId: polity.Id,
            polityName: polity.Name,
            speciesId: polity.SpeciesId,
            speciesName: lookup.GetRequiredSpecies(polity.SpeciesId, "Settlement starvation recovery").Name,
            regionId: region.Id,
            regionName: region.Name,
            settlementId: settlement.Id,
            settlementName: settlement.Name,
            before: new Dictionary<string, string>
            {
                ["foodState"] = FoodState.Starving.ToString(),
                ["starvationStage"] = previousStage.ToString()
            },
            after: new Dictionary<string, string>
            {
                ["foodState"] = settlement.FoodState.ToString(),
                ["starvationStage"] = settlement.StarvationStage.ToString()
            },
            metadata: new Dictionary<string, string>
            {
                ["type"] = WorldEventType.FamineRelief,
                ["location"] = region.Name,
                ["actors"] = settlement.Name,
                ["cause"] = "settlement_starvation_recovered",
                ["severity"] = previousStage >= SettlementStarvationStage.Severe
                    ? WorldEventSeverity.Legendary.ToString()
                    : WorldEventSeverity.Major.ToString()
            });
        settlement.LastStarvationRecoveryYear = world.Time.Year;
    }

    private static double ResolveNeedWeight(Settlement settlement)
        => 1.0
           + (settlement.CultivatedLand * 0.20)
           + Math.Min(1.5, settlement.YearsEstablished * 0.03);

    private static double ResolveForageWeight(WorldLookup lookup, Settlement settlement)
    {
        if (!lookup.TryGetRegion(settlement.RegionId, out Region? region) || region is null)
        {
            return 1.0;
        }

        double plantRatio = region.MaxPlantBiomass <= 0 ? 0.0 : region.PlantBiomass / region.MaxPlantBiomass;
        double animalRatio = region.MaxAnimalBiomass <= 0 ? 0.0 : region.AnimalBiomass / region.MaxAnimalBiomass;
        return 0.50
            + (region.Fertility * 0.45)
            + (region.WaterAvailability * 0.30)
            + (plantRatio * 0.20)
            + (animalRatio * 0.10);
    }

    private static int ResolvePriorityBucket(WorldLookup lookup, int senderRegionId, int receiverRegionId)
    {
        if (senderRegionId == receiverRegionId)
        {
            return 0;
        }

        if (lookup.TryGetRegion(senderRegionId, out Region? senderRegion) && senderRegion is not null
            && senderRegion.ConnectedRegionIds.Contains(receiverRegionId))
        {
            return 1;
        }

        return 2;
    }

    private static int ResolveRegionDistance(WorldLookup lookup, int sourceRegionId, int targetRegionId)
    {
        if (sourceRegionId == targetRegionId)
        {
            return 0;
        }

        Queue<(int regionId, int depth)> queue = new();
        HashSet<int> visited = [sourceRegionId];
        queue.Enqueue((sourceRegionId, 0));

        while (queue.Count > 0)
        {
            (int currentRegionId, int depth) = queue.Dequeue();
            if (!lookup.TryGetRegion(currentRegionId, out Region? region) || region is null)
            {
                continue;
            }

            foreach (int neighborRegionId in region.ConnectedRegionIds)
            {
                if (!visited.Add(neighborRegionId))
                {
                    continue;
                }

                int nextDepth = depth + 1;
                if (neighborRegionId == targetRegionId)
                {
                    return nextDepth;
                }

                queue.Enqueue((neighborRegionId, nextDepth));
            }
        }

        return int.MaxValue / 2;
    }
}
