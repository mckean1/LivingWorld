using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Economy;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class MaterialEconomySystem
{
    private const double ExtractionLaborShare = 0.18;
    private const double CraftLaborShare = 0.08;
    private const double MaterialExportShareLimitPerMonth = 0.30;
    private const double TransportLossPerHop = 0.05;
    private const double SignificantConvoyShare = 0.35;

    private static readonly MaterialType[] RawMaterials =
    [
        MaterialType.Wood,
        MaterialType.Stone,
        MaterialType.Clay,
        MaterialType.Fiber,
        MaterialType.Salt,
        MaterialType.CopperOre,
        MaterialType.IronOre
    ];

    private static readonly MaterialType[] RedistributionPriority =
    [
        MaterialType.SimpleTools,
        MaterialType.Pottery,
        MaterialType.PreservedFood,
        MaterialType.Salt,
        MaterialType.Lumber,
        MaterialType.StoneBlocks,
        MaterialType.Rope,
        MaterialType.Textiles,
        MaterialType.Wood,
        MaterialType.Stone,
        MaterialType.Clay,
        MaterialType.Fiber,
        MaterialType.CopperOre,
        MaterialType.IronOre
    ];

    private static readonly IReadOnlyList<ProductionRecipe> Recipes =
    [
        new("lumber", MaterialType.Lumber, 1.0, new Dictionary<MaterialType, double> { [MaterialType.Wood] = 1.0 }, []),
        new("stone_blocks", MaterialType.StoneBlocks, 1.0, new Dictionary<MaterialType, double> { [MaterialType.Stone] = 1.0 }, [AdvancementId.BasicConstruction]),
        new("pottery", MaterialType.Pottery, 1.0, new Dictionary<MaterialType, double> { [MaterialType.Clay] = 1.0 }, [AdvancementId.Fire, AdvancementId.FoodStorage]),
        new("rope", MaterialType.Rope, 1.0, new Dictionary<MaterialType, double> { [MaterialType.Fiber] = 1.0 }, [AdvancementId.SeasonalPlanning]),
        new("textiles", MaterialType.Textiles, 1.0, new Dictionary<MaterialType, double> { [MaterialType.Fiber] = 1.25 }, [AdvancementId.CraftSpecialization]),
        new("simple_tools", MaterialType.SimpleTools, 1.0, new Dictionary<MaterialType, double>
        {
            [MaterialType.Wood] = 0.8,
            [MaterialType.Stone] = 0.8
        }, [AdvancementId.StoneTools]),
    ];

    public void UpdateMonthlyMaterials(World world)
    {
        WorldLookup lookup = new(world);

        foreach (Polity polity in world.Polities.Where(candidate => candidate.Population > 0 && candidate.HasSettlements))
        {
            foreach (Settlement settlement in polity.Settlements)
            {
                settlement.ResetMonthlyMaterialStats();
                SetTargetReserves(polity, settlement);
                DiscoverRegionalMaterials(world, lookup, polity, settlement);
                ExtractMaterials(world, lookup, polity, settlement);
            }

            foreach (Settlement settlement in polity.Settlements)
            {
                ProduceMaterials(world, lookup, polity, settlement);
                ApplyMonthlyMaterialWear(settlement);
            }

            UpdateSettlementMaterialPressure(polity.Settlements);
            RedistributeMaterialsWithinPolity(world, lookup, polity);
            UpdateSettlementMaterialPressure(polity.Settlements);

            foreach (Settlement settlement in polity.Settlements)
            {
                EmitMaterialPressureTransitions(world, lookup, polity, settlement);
                UpdateSpecialization(world, lookup, polity, settlement);
            }
        }
    }

    private static void DiscoverRegionalMaterials(World world, WorldLookup lookup, Polity polity, Settlement settlement)
    {
        Region region = lookup.GetRequiredRegion(settlement.RegionId, "Material discovery");

        foreach (MaterialType materialType in RawMaterials)
        {
            double abundance = region.GetMaterialAbundance(materialType);
            if (abundance < 0.42)
            {
                continue;
            }

            string discoveryKey = BuildMaterialDiscoveryKey(region.Id, materialType);
            if (!polity.AddDiscovery(new CulturalDiscovery(
                    discoveryKey,
                    $"{region.Name} {GetMaterialLabel(materialType)}",
                    CulturalDiscoveryCategory.Resource,
                    null,
                    region.Id)))
            {
                continue;
            }

            world.AddEvent(
                WorldEventType.MaterialDiscovered,
                abundance >= 0.72 && materialType is MaterialType.Salt or MaterialType.CopperOre or MaterialType.IronOre
                    ? WorldEventSeverity.Major
                    : WorldEventSeverity.Notable,
                $"{polity.Name} discovered {GetMaterialLabel(materialType).ToLowerInvariant()} in {region.Name}",
                $"{polity.Name} recognized workable {GetMaterialLabel(materialType).ToLowerInvariant()} in {region.Name}.",
                reason: "regional_material_discovered",
                scope: WorldEventScope.Local,
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: polity.SpeciesId,
                speciesName: lookup.GetRequiredSpecies(polity.SpeciesId, "Material discovery").Name,
                regionId: region.Id,
                regionName: region.Name,
                settlementId: settlement.Id,
                settlementName: settlement.Name,
                metadata: new Dictionary<string, string>
                {
                    ["materialType"] = materialType.ToString(),
                    ["abundance"] = abundance.ToString("F2")
                });
        }
    }

    private static void ExtractMaterials(World world, WorldLookup lookup, Polity polity, Settlement settlement)
    {
        Region region = lookup.GetRequiredRegion(settlement.RegionId, "Material extraction");
        double localPopulationShare = polity.Settlements.Count == 0 ? 0.0 : polity.Population / (double)polity.Settlements.Count;
        double extractionLabor = Math.Max(1.0, localPopulationShare * ExtractionLaborShare);
        double hardshipPenalty = settlement.FoodState switch
        {
            FoodState.Starving => 0.60,
            FoodState.Deficit => 0.82,
            _ => 1.0
        };
        double extractionModifier = settlement.ResolveToolEffectiveness() * hardshipPenalty;
        bool extractionStarted = false;

        foreach (MaterialType materialType in RawMaterials)
        {
            double abundance = region.GetMaterialAbundance(materialType);
            if (abundance <= 0.15 || !CanExtract(polity, materialType, region))
            {
                continue;
            }

            double output = ResolveExtractionOutput(materialType, extractionLabor, abundance, extractionModifier, polity);
            if (output <= 0.05)
            {
                continue;
            }

            settlement.AddMaterial(materialType, output);
            extractionStarted = true;
        }

        if (extractionStarted && settlement.MaterialMilestonesRecorded.Add("extraction-started"))
        {
            world.AddEvent(
                WorldEventType.MaterialExtractionStarted,
                WorldEventSeverity.Notable,
                $"{settlement.Name} began regular material extraction",
                $"{settlement.Name} began sustained extraction of local timber, stone, clay, fiber, salt, or ore.",
                reason: "settlement_material_extraction_started",
                scope: WorldEventScope.Local,
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: polity.SpeciesId,
                speciesName: lookup.GetRequiredSpecies(polity.SpeciesId, "Material extraction start").Name,
                regionId: region.Id,
                regionName: region.Name,
                settlementId: settlement.Id,
                settlementName: settlement.Name);
        }
    }

    private static void ProduceMaterials(World world, WorldLookup lookup, Polity polity, Settlement settlement)
    {
        Region region = lookup.GetRequiredRegion(settlement.RegionId, "Material production");
        double localPopulationShare = polity.Settlements.Count == 0 ? 0.0 : polity.Population / (double)polity.Settlements.Count;
        double craftLabor = Math.Max(1.0, localPopulationShare * CraftLaborShare);
        double craftEfficiency = settlement.ResolveToolEffectiveness() * (polity.HasAdvancement(AdvancementId.CraftSpecialization) ? 1.20 : 1.0);

        foreach (ProductionRecipe recipe in Recipes)
        {
            if (!recipe.IsAvailable(polity.Advancements))
            {
                continue;
            }

            double maxByLabor = craftLabor * craftEfficiency * ResolveRecipeLaborRate(recipe.Output);
            double maxByInputs = recipe.Inputs.Min(input => settlement.GetMaterialStockpile(input.Key) / input.Value);
            double cycles = Math.Min(maxByLabor, maxByInputs);
            if (cycles < 0.75)
            {
                continue;
            }

            double roundedCycles = Math.Floor(cycles);
            foreach ((MaterialType materialType, double amount) in recipe.Inputs)
            {
                settlement.ConsumeMaterial(materialType, amount * roundedCycles);
            }

            double output = roundedCycles * recipe.OutputAmount;
            settlement.AddMaterial(recipe.Output, output);

            if (settlement.MaterialMilestonesRecorded.Add($"production-started:{recipe.Id}"))
            {
                world.AddEvent(
                    WorldEventType.ProductionStarted,
                    WorldEventSeverity.Notable,
                    $"{settlement.Name} began making {GetMaterialLabel(recipe.Output).ToLowerInvariant()}",
                    $"{settlement.Name} established regular production of {GetMaterialLabel(recipe.Output).ToLowerInvariant()} in {region.Name}.",
                    reason: "production_started",
                    scope: WorldEventScope.Local,
                    polityId: polity.Id,
                    polityName: polity.Name,
                    speciesId: polity.SpeciesId,
                    speciesName: lookup.GetRequiredSpecies(polity.SpeciesId, "Production started").Name,
                    regionId: region.Id,
                    regionName: region.Name,
                    settlementId: settlement.Id,
                    settlementName: settlement.Name,
                    metadata: new Dictionary<string, string>
                    {
                        ["materialType"] = recipe.Output.ToString()
                    });
            }

            EmitProductionMilestoneIfNeeded(world, lookup, polity, settlement, region, recipe.Output, output);
        }

        ProducePreservedFood(world, lookup, polity, settlement, region, craftLabor * craftEfficiency);
        UpgradeToolmakingTierIfPossible(world, lookup, polity, settlement, region);
    }

    private static void ProducePreservedFood(World world, WorldLookup lookup, Polity polity, Settlement settlement, Region region, double capacity)
    {
        if (!polity.HasAdvancement(AdvancementId.FoodStorage)
            || settlement.GetMaterialStockpile(MaterialType.Salt) < 1.0
            || polity.FoodStores <= polity.FoodNeededThisMonth * 0.75)
        {
            return;
        }

        double foodAvailableForPreservation = Math.Max(0.0, polity.FoodStores - (polity.FoodNeededThisMonth * 0.75));
        double potentialCycles = Math.Min(capacity * 0.40, foodAvailableForPreservation / 6.0);
        if (potentialCycles < 1.0)
        {
            return;
        }

        double cycles = Math.Min(Math.Floor(potentialCycles), settlement.GetMaterialStockpile(MaterialType.Salt));
        if (cycles <= 0)
        {
            return;
        }

        settlement.ConsumeMaterial(MaterialType.Salt, cycles);
        double foodCommitted = cycles * 6.0;
        polity.FoodStores = Math.Max(0.0, polity.FoodStores - foodCommitted);
        settlement.AddMaterial(MaterialType.PreservedFood, cycles * 5.0);

        if (settlement.MaterialMilestonesRecorded.Add("preservation-established"))
        {
            world.AddEvent(
                WorldEventType.PreservationEstablished,
                WorldEventSeverity.Major,
                $"{settlement.Name} established food preservation in {region.Name}",
                $"{settlement.Name} began preserving seasonal surplus with salt from {region.Name}.",
                reason: "preservation_established",
                scope: WorldEventScope.Local,
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: polity.SpeciesId,
                speciesName: lookup.GetRequiredSpecies(polity.SpeciesId, "Preservation established").Name,
                regionId: region.Id,
                regionName: region.Name,
                settlementId: settlement.Id,
                settlementName: settlement.Name,
                metadata: new Dictionary<string, string>
                {
                    ["materialType"] = MaterialType.PreservedFood.ToString()
                });
        }
    }

    private static void UpgradeToolmakingTierIfPossible(World world, WorldLookup lookup, Polity polity, Settlement settlement, Region region)
    {
        if (!polity.HasAdvancement(AdvancementId.StoneTools) || settlement.GetMaterialStockpile(MaterialType.SimpleTools) < 1.0)
        {
            settlement.ToolProductionTier = 0;
            return;
        }

        int previousTier = settlement.ToolProductionTier;
        if (polity.HasAdvancement(AdvancementId.CraftSpecialization)
            && polity.HasAdvancement(AdvancementId.BasicConstruction)
            && settlement.GetMaterialStockpile(MaterialType.IronOre) >= 1.0)
        {
            settlement.ConsumeMaterial(MaterialType.IronOre, 1.0);
            settlement.ToolProductionTier = 2;
        }
        else if (polity.HasAdvancement(AdvancementId.CraftSpecialization)
                 && settlement.GetMaterialStockpile(MaterialType.CopperOre) >= 1.0)
        {
            settlement.ConsumeMaterial(MaterialType.CopperOre, 1.0);
            settlement.ToolProductionTier = 1;
        }
        else
        {
            settlement.ToolProductionTier = 0;
        }

        if (settlement.ToolProductionTier > previousTier && settlement.MaterialMilestonesRecorded.Add("toolmaking-established"))
        {
            world.AddEvent(
                WorldEventType.ToolmakingEstablished,
                WorldEventSeverity.Major,
                $"{settlement.Name} established sustained toolmaking",
                $"{settlement.Name} began producing more reliable tools in {region.Name}.",
                reason: settlement.ToolProductionTier >= 2 ? "iron_toolmaking_established" : "toolmaking_established",
                scope: WorldEventScope.Local,
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: polity.SpeciesId,
                speciesName: lookup.GetRequiredSpecies(polity.SpeciesId, "Toolmaking established").Name,
                regionId: region.Id,
                regionName: region.Name,
                settlementId: settlement.Id,
                settlementName: settlement.Name,
                metadata: new Dictionary<string, string>
                {
                    ["materialType"] = MaterialType.SimpleTools.ToString(),
                    ["toolTier"] = settlement.ToolProductionTier.ToString()
                });
        }
    }

    private static void ApplyMonthlyMaterialWear(Settlement settlement)
    {
        settlement.ConsumeMaterial(MaterialType.SimpleTools, Math.Max(0.25, settlement.FoodRequired * 0.008));
        settlement.ConsumeMaterial(MaterialType.Pottery, Math.Max(0.20, settlement.FoodRequired * 0.006));
        settlement.ConsumeMaterial(MaterialType.Rope, Math.Max(0.10, settlement.FoodRequired * 0.003));
        settlement.ConsumeMaterial(MaterialType.Textiles, Math.Max(0.10, settlement.FoodRequired * 0.003));
        settlement.ConsumeMaterial(MaterialType.Lumber, Math.Max(0.05, settlement.YearsEstablished * 0.04));
        settlement.ConsumeMaterial(MaterialType.StoneBlocks, Math.Max(0.04, settlement.YearsEstablished * 0.03));
    }

    private static void UpdateSettlementMaterialPressure(IEnumerable<Settlement> settlements)
    {
        foreach (Settlement settlement in settlements)
        {
            foreach (MaterialType materialType in RedistributionPriority)
            {
                settlement.CalculateMaterialPressure(materialType);
            }
        }
    }

    private static void RedistributeMaterialsWithinPolity(World world, WorldLookup lookup, Polity polity)
    {
        if (polity.SettlementCount <= 1)
        {
            return;
        }

        foreach (MaterialType materialType in RedistributionPriority)
        {
            List<Settlement> senders = polity.Settlements
                .Where(settlement => settlement.MaterialPressureStates[materialType] == MaterialPressureState.Surplus)
                .ToList();
            List<Settlement> receivers = polity.Settlements
                .Where(settlement => settlement.MaterialPressureStates[materialType] == MaterialPressureState.Deficit)
                .OrderByDescending(settlement => ResolveMaterialNeedShare(settlement, materialType))
                .ThenByDescending(_ => IsCriticalMaterial(materialType))
                .ToList();

            if (senders.Count == 0 || receivers.Count == 0)
            {
                continue;
            }

            Dictionary<int, double> senderExportBudget = senders.ToDictionary(
                settlement => settlement.Id,
                settlement =>
                {
                    double target = settlement.MaterialTargetReserves[materialType];
                    return Math.Max(0.0, settlement.GetMaterialStockpile(materialType) - target) * MaterialExportShareLimitPerMonth;
                });

            foreach (Settlement receiver in receivers)
            {
                double receiverNeed = Math.Max(0.0, receiver.MaterialTargetReserves[materialType] - receiver.GetMaterialStockpile(materialType));
                if (receiverNeed <= 0.05)
                {
                    continue;
                }

                List<Settlement> candidateSenders = senders
                    .Where(sender => sender.Id != receiver.Id && senderExportBudget[sender.Id] > 0.05)
                    .OrderBy(sender => ResolvePriorityBucket(lookup, sender.RegionId, receiver.RegionId))
                    .ThenBy(sender => ResolveRegionDistance(lookup, sender.RegionId, receiver.RegionId))
                    .ThenByDescending(sender => sender.GetMaterialStockpile(materialType))
                    .ToList();

                foreach (Settlement sender in candidateSenders)
                {
                    double availableBudget = senderExportBudget[sender.Id];
                    if (availableBudget <= 0.05)
                    {
                        continue;
                    }

                    double target = sender.MaterialTargetReserves[materialType];
                    double senderSurplus = Math.Max(0.0, sender.GetMaterialStockpile(materialType) - target);
                    if (senderSurplus <= 0.05)
                    {
                        continue;
                    }

                    double plannedTransfer = Math.Min(Math.Min(senderSurplus, availableBudget), receiverNeed);
                    if (plannedTransfer <= 0.05)
                    {
                        continue;
                    }

                    int distance = ResolveRegionDistance(lookup, sender.RegionId, receiver.RegionId);
                    double transportLoss = Math.Clamp(distance * TransportLossPerHop, 0.0, 0.90);
                    double transportMultiplier = (sender.ResolveTransportAidMultiplier() + receiver.ResolveTransportAidMultiplier()) / 2.0;
                    double effectiveLoss = Math.Clamp(transportLoss / Math.Max(1.0, transportMultiplier), 0.0, 0.90);
                    double shipped = sender.ConsumeMaterial(materialType, plannedTransfer);
                    if (shipped <= 0.05)
                    {
                        continue;
                    }

                    double received = shipped * (1.0 - effectiveLoss);
                    receiver.AddMaterial(materialType, received);
                    receiver.MaterialConsumedThisMonth[materialType] = Math.Max(0.0, receiver.MaterialConsumedThisMonth[materialType] - received);
                    senderExportBudget[sender.Id] = Math.Max(0.0, availableBudget - shipped);
                    receiverNeed = Math.Max(0.0, receiverNeed - received);
                    polity.MaterialMovedThisYear[materialType] = polity.MaterialMovedThisYear.TryGetValue(materialType, out double moved)
                        ? moved + received
                        : received;

                    bool relievedCriticalNeed = IsCriticalMaterial(materialType) && receiverNeed <= Math.Max(0.5, receiver.MaterialTargetReserves[materialType] * 0.20);
                    bool significantConvoy = receiver.MaterialTargetReserves[materialType] > 0
                        && received / receiver.MaterialTargetReserves[materialType] >= SignificantConvoyShare;
                    if (relievedCriticalNeed || significantConvoy)
                    {
                        world.AddEvent(
                            WorldEventType.MaterialConvoySent,
                            WorldEventSeverity.Notable,
                            $"{sender.Name} sent {GetMaterialLabel(materialType).ToLowerInvariant()} to {receiver.Name}",
                            $"{sender.Name} sent {shipped:F1} {GetMaterialLabel(materialType).ToLowerInvariant()} to {receiver.Name}; {received:F1} arrived after transport loss.",
                            reason: relievedCriticalNeed ? "critical_material_relief" : "material_convoy_sent",
                            scope: WorldEventScope.Local,
                            polityId: polity.Id,
                            polityName: polity.Name,
                            speciesId: polity.SpeciesId,
                            speciesName: lookup.GetRequiredSpecies(polity.SpeciesId, "Material convoy").Name,
                            regionId: receiver.RegionId,
                            regionName: lookup.GetRequiredRegion(receiver.RegionId, "Material convoy").Name,
                            settlementId: receiver.Id,
                            settlementName: receiver.Name,
                            after: new Dictionary<string, string>
                            {
                                ["materialType"] = materialType.ToString(),
                                ["received"] = received.ToString("F1")
                            },
                            metadata: new Dictionary<string, string>
                            {
                                ["senderSettlementId"] = sender.Id.ToString(),
                                ["senderSettlementName"] = sender.Name,
                                ["materialType"] = materialType.ToString(),
                                ["distance"] = distance.ToString()
                            });
                    }
                }
            }
        }
    }

    private static void EmitMaterialPressureTransitions(World world, WorldLookup lookup, Polity polity, Settlement settlement)
    {
        Region region = lookup.GetRequiredRegion(settlement.RegionId, "Material pressure");
        Species species = lookup.GetRequiredSpecies(polity.SpeciesId, "Material pressure");
        List<MaterialType> startedMaterials = [];
        List<MaterialType> worsenedMaterials = [];
        List<MaterialType> resolvedMaterials = [];
        List<MaterialType> convoyFailureMaterials = [];

        foreach (MaterialType materialType in RedistributionPriority)
        {
            int previousBand = settlement.LastRecordedMaterialShortageBands[materialType];
            int currentBand = settlement.ResolveMaterialShortageBand(materialType);
            if (previousBand == currentBand)
            {
                continue;
            }

            string label = GetMaterialLabel(materialType).ToLowerInvariant();
            bool criticalMaterial = IsCriticalMaterial(materialType);
            bool convoyFailure = currentBand >= 2 && criticalMaterial;

            if (currentBand == 0 && previousBand > 0)
            {
                resolvedMaterials.Add(materialType);
                world.AddEvent(
                    WorldEventType.MaterialShortageResolved,
                    criticalMaterial ? WorldEventSeverity.Notable : WorldEventSeverity.Minor,
                    $"{settlement.Name} resolved its {label} shortage",
                    $"{settlement.Name} recovered enough {label} to leave shortage conditions behind.",
                    reason: "material_shortage_resolved",
                    scope: WorldEventScope.Local,
                    polityId: polity.Id,
                    polityName: polity.Name,
                    speciesId: polity.SpeciesId,
                    speciesName: species.Name,
                    regionId: region.Id,
                    regionName: region.Name,
                    settlementId: settlement.Id,
                    settlementName: settlement.Name,
                    metadata: new Dictionary<string, string>
                    {
                        ["materialType"] = materialType.ToString()
                    });
            }
            else if (previousBand == 0 && currentBand > 0)
            {
                startedMaterials.Add(materialType);
                if (convoyFailure)
                {
                    convoyFailureMaterials.Add(materialType);
                }

                world.AddEvent(
                    convoyFailure ? WorldEventType.MaterialConvoyFailed : WorldEventType.MaterialShortageStarted,
                    convoyFailure ? WorldEventSeverity.Notable : WorldEventSeverity.Minor,
                    convoyFailure
                        ? $"No material convoy arrived. {settlement.Name} fell into {label} shortage"
                        : $"{settlement.Name} entered a {label} shortage",
                    $"{settlement.Name} fell below its working reserve of {label}.",
                    reason: convoyFailure
                        ? "critical_material_shortage_unaided"
                        : "material_shortage_started",
                    scope: WorldEventScope.Local,
                    polityId: polity.Id,
                    polityName: polity.Name,
                    speciesId: polity.SpeciesId,
                    speciesName: species.Name,
                    regionId: region.Id,
                    regionName: region.Name,
                    settlementId: settlement.Id,
                    settlementName: settlement.Name,
                    metadata: new Dictionary<string, string>
                    {
                        ["materialType"] = materialType.ToString(),
                        ["shortageBand"] = currentBand.ToString()
                    });
            }
            else if (currentBand > previousBand)
            {
                worsenedMaterials.Add(materialType);
                if (convoyFailure)
                {
                    convoyFailureMaterials.Add(materialType);
                }

                world.AddEvent(
                    convoyFailure ? WorldEventType.MaterialConvoyFailed : WorldEventType.MaterialShortageWorsened,
                    convoyFailure ? WorldEventSeverity.Notable : WorldEventSeverity.Minor,
                    convoyFailure
                        ? $"No material convoy arrived. {label} shortage worsened in {settlement.Name}"
                        : $"{label} shortage worsened in {settlement.Name}",
                    $"{settlement.Name} slipped deeper into {label} shortage.",
                    reason: convoyFailure
                        ? "critical_material_shortage_worsened_unaided"
                        : "material_shortage_worsened",
                    scope: WorldEventScope.Local,
                    polityId: polity.Id,
                    polityName: polity.Name,
                    speciesId: polity.SpeciesId,
                    speciesName: species.Name,
                    regionId: region.Id,
                    regionName: region.Name,
                    settlementId: settlement.Id,
                    settlementName: settlement.Name,
                    metadata: new Dictionary<string, string>
                    {
                        ["materialType"] = materialType.ToString(),
                        ["shortageBand"] = currentBand.ToString()
                    });
            }

            settlement.LastRecordedMaterialShortageBands[materialType] = currentBand;
        }

        EmitGroupedMaterialCrisisEvent(
            world,
            polity,
            settlement,
            region,
            species,
            startedMaterials,
            worsenedMaterials,
            resolvedMaterials,
            convoyFailureMaterials);
    }

    private static void UpdateSpecialization(World world, WorldLookup lookup, Polity polity, Settlement settlement)
    {
        Region region = lookup.GetRequiredRegion(settlement.RegionId, "Settlement specialization");
        foreach ((SettlementSpecializationTag tag, MaterialType output, double monthlyOutput, double localMatch) in ResolveSpecializationSignals(region, settlement))
        {
            if (monthlyOutput <= 0.05)
            {
                continue;
            }

            double updatedScore = settlement.SpecializationScores.TryGetValue(tag, out double current)
                ? current + (monthlyOutput * (0.60 + localMatch))
                : monthlyOutput * (0.60 + localMatch);
            settlement.SpecializationScores[tag] = updatedScore;

            if (updatedScore < 18.0 || !settlement.SpecializationTags.Add(tag))
            {
                continue;
            }

            world.AddEvent(
                WorldEventType.SettlementSpecialized,
                WorldEventSeverity.Major,
                $"{settlement.Name} became known for {DescribeSpecialization(tag)}",
                $"{settlement.Name} developed sustained output in {DescribeSpecialization(tag)}.",
                reason: "settlement_specialized",
                scope: WorldEventScope.Local,
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: polity.SpeciesId,
                speciesName: lookup.GetRequiredSpecies(polity.SpeciesId, "Settlement specialization").Name,
                regionId: region.Id,
                regionName: region.Name,
                settlementId: settlement.Id,
                settlementName: settlement.Name,
                metadata: new Dictionary<string, string>
                {
                    ["specializationTag"] = tag.ToString(),
                    ["materialType"] = output.ToString()
                });
        }
    }

    private static void EmitGroupedMaterialCrisisEvent(
        World world,
        Polity polity,
        Settlement settlement,
        Region region,
        Species species,
        IReadOnlyList<MaterialType> startedMaterials,
        IReadOnlyList<MaterialType> worsenedMaterials,
        IReadOnlyList<MaterialType> resolvedMaterials,
        IReadOnlyList<MaterialType> convoyFailureMaterials)
    {
        if (ShouldEmitGroupedMaterialCrisisStarted(startedMaterials, convoyFailureMaterials))
        {
            bool convoyFailure = convoyFailureMaterials.Count > 0;
            IReadOnlyList<MaterialType> groupedMaterials = convoyFailure ? convoyFailureMaterials : startedMaterials;
            world.AddEvent(
                WorldEventType.MaterialCrisisStarted,
                WorldEventSeverity.Major,
                convoyFailure
                    ? $"No material convoy arrived. {settlement.Name} fell into shortages of {DescribeMaterialList(groupedMaterials)}"
                    : groupedMaterials.Count > 1
                        ? $"{settlement.Name} fell into multiple material shortages"
                        : $"{settlement.Name} entered a material shortage",
                convoyFailure
                    ? $"{settlement.Name} entered a broader material crisis after convoy failure left shortages in {DescribeMaterialList(groupedMaterials)}."
                    : $"{settlement.Name} fell into shortages of {DescribeMaterialList(groupedMaterials)}.",
                reason: convoyFailure ? "grouped_material_crisis_unaided" : "grouped_material_crisis_started",
                scope: WorldEventScope.Local,
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: polity.SpeciesId,
                speciesName: species.Name,
                regionId: region.Id,
                regionName: region.Name,
                settlementId: settlement.Id,
                settlementName: settlement.Name,
                metadata: BuildGroupedMaterialMetadata(groupedMaterials, convoyFailure));
        }

        if (ShouldEmitGroupedMaterialCrisisWorsened(worsenedMaterials, convoyFailureMaterials))
        {
            bool convoyFailure = convoyFailureMaterials.Intersect(worsenedMaterials).Any();
            IReadOnlyList<MaterialType> groupedMaterials = convoyFailure
                ? worsenedMaterials.Where(convoyFailureMaterials.Contains).ToList()
                : worsenedMaterials;
            world.AddEvent(
                WorldEventType.MaterialCrisisWorsened,
                WorldEventSeverity.Major,
                convoyFailure
                    ? $"A failed convoy worsened {settlement.Name}'s material crisis"
                    : $"{settlement.Name}'s material shortages deepened",
                $"{settlement.Name}'s shortages worsened in {DescribeMaterialList(groupedMaterials)}.",
                reason: convoyFailure ? "grouped_material_crisis_worsened_unaided" : "grouped_material_crisis_worsened",
                scope: WorldEventScope.Local,
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: polity.SpeciesId,
                speciesName: species.Name,
                regionId: region.Id,
                regionName: region.Name,
                settlementId: settlement.Id,
                settlementName: settlement.Name,
                metadata: BuildGroupedMaterialMetadata(groupedMaterials, convoyFailure));
        }

        if (ShouldEmitGroupedMaterialCrisisResolved(resolvedMaterials))
        {
            world.AddEvent(
                WorldEventType.MaterialCrisisResolved,
                WorldEventSeverity.Major,
                $"{settlement.Name} recovered from a broader material crisis",
                $"{settlement.Name}'s shortages eased in {DescribeMaterialList(resolvedMaterials)}.",
                reason: "grouped_material_crisis_resolved",
                scope: WorldEventScope.Local,
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: polity.SpeciesId,
                speciesName: species.Name,
                regionId: region.Id,
                regionName: region.Name,
                settlementId: settlement.Id,
                settlementName: settlement.Name,
                metadata: BuildGroupedMaterialMetadata(resolvedMaterials, convoyFailure: false));
        }
    }

    private static bool ShouldEmitGroupedMaterialCrisisStarted(
        IReadOnlyList<MaterialType> startedMaterials,
        IReadOnlyList<MaterialType> convoyFailureMaterials)
        => convoyFailureMaterials.Count > 0 || startedMaterials.Count > 1;

    private static bool ShouldEmitGroupedMaterialCrisisWorsened(
        IReadOnlyList<MaterialType> worsenedMaterials,
        IReadOnlyList<MaterialType> convoyFailureMaterials)
        => worsenedMaterials.Count > 1 || convoyFailureMaterials.Intersect(worsenedMaterials).Any();

    private static bool ShouldEmitGroupedMaterialCrisisResolved(IReadOnlyList<MaterialType> resolvedMaterials)
        => resolvedMaterials.Count > 1;

    private static Dictionary<string, string> BuildGroupedMaterialMetadata(
        IReadOnlyList<MaterialType> materials,
        bool convoyFailure)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["groupedMaterials"] = string.Join(",", materials.Select(materialType => materialType.ToString())),
            ["groupedCount"] = materials.Count.ToString(),
            ["convoyFailure"] = convoyFailure.ToString()
        };

    private static string DescribeMaterialList(IReadOnlyList<MaterialType> materials)
    {
        List<string> labels = materials
            .Distinct()
            .Select(materialType => GetMaterialLabel(materialType).ToLowerInvariant())
            .Take(3)
            .ToList();
        if (labels.Count == 0)
        {
            return "materials";
        }

        if (materials.Count > labels.Count)
        {
            labels.Add("other materials");
        }

        return labels.Count switch
        {
            1 => labels[0],
            2 => $"{labels[0]} and {labels[1]}",
            _ => $"{string.Join(", ", labels.Take(labels.Count - 1))}, and {labels[^1]}"
        };
    }

    private static IEnumerable<(SettlementSpecializationTag Tag, MaterialType Output, double MonthlyOutput, double LocalMatch)> ResolveSpecializationSignals(Region region, Settlement settlement)
    {
        yield return (SettlementSpecializationTag.LumberCenter, MaterialType.Lumber, settlement.MaterialProducedThisMonth[MaterialType.Lumber], region.WoodAbundance);
        yield return (SettlementSpecializationTag.Stoneworks, MaterialType.StoneBlocks, settlement.MaterialProducedThisMonth[MaterialType.StoneBlocks], region.StoneAbundance);
        yield return (SettlementSpecializationTag.PotteryTradition, MaterialType.Pottery, settlement.MaterialProducedThisMonth[MaterialType.Pottery], region.ClayAbundance);
        yield return (SettlementSpecializationTag.Ropeworks, MaterialType.Rope, settlement.MaterialProducedThisMonth[MaterialType.Rope], region.FiberAbundance);
        yield return (SettlementSpecializationTag.TextileWorks, MaterialType.Textiles, settlement.MaterialProducedThisMonth[MaterialType.Textiles], region.FiberAbundance);
        yield return (SettlementSpecializationTag.PreservationCenter, MaterialType.PreservedFood, settlement.MaterialProducedThisMonth[MaterialType.PreservedFood], region.SaltAbundance);
        yield return (SettlementSpecializationTag.ToolmakingCenter, MaterialType.SimpleTools, settlement.MaterialProducedThisMonth[MaterialType.SimpleTools], Math.Max(region.StoneAbundance, Math.Max(region.CopperOreAbundance, region.IronOreAbundance)));
        yield return (SettlementSpecializationTag.OreWorks, MaterialType.IronOre, settlement.MaterialProducedThisMonth[MaterialType.IronOre] + settlement.MaterialProducedThisMonth[MaterialType.CopperOre], Math.Max(region.CopperOreAbundance, region.IronOreAbundance));
    }

    private static void SetTargetReserves(Polity polity, Settlement settlement)
    {
        settlement.SetMaterialTargetReserve(MaterialType.Wood, Math.Max(4.0, settlement.FoodRequired * 0.08));
        settlement.SetMaterialTargetReserve(MaterialType.Stone, Math.Max(3.0, settlement.FoodRequired * 0.06));
        settlement.SetMaterialTargetReserve(MaterialType.Clay, Math.Max(2.0, settlement.FoodRequired * 0.04));
        settlement.SetMaterialTargetReserve(MaterialType.Fiber, Math.Max(2.0, settlement.FoodRequired * 0.04));
        settlement.SetMaterialTargetReserve(MaterialType.Salt, Math.Max(2.0, settlement.FoodRequired * 0.03));
        settlement.SetMaterialTargetReserve(MaterialType.CopperOre, Math.Max(1.0, settlement.CultivatedLand * 0.05));
        settlement.SetMaterialTargetReserve(MaterialType.IronOre, Math.Max(1.0, settlement.CultivatedLand * 0.04));
        settlement.SetMaterialTargetReserve(MaterialType.Lumber, Math.Max(3.0, settlement.YearsEstablished * 0.15 + settlement.CultivatedLand * 0.25));
        settlement.SetMaterialTargetReserve(MaterialType.StoneBlocks, Math.Max(2.0, settlement.YearsEstablished * 0.10 + settlement.CultivatedLand * 0.12));
        settlement.SetMaterialTargetReserve(MaterialType.Pottery, Math.Max(3.0, settlement.FoodRequired * 0.08));
        settlement.SetMaterialTargetReserve(MaterialType.Rope, Math.Max(2.0, settlement.FoodRequired * 0.03));
        settlement.SetMaterialTargetReserve(MaterialType.Textiles, Math.Max(2.0, settlement.FoodRequired * 0.03));
        settlement.SetMaterialTargetReserve(MaterialType.SimpleTools, Math.Max(3.0, settlement.FoodRequired * 0.10 + settlement.CultivatedLand * 0.35));
        settlement.SetMaterialTargetReserve(MaterialType.PreservedFood, Math.Max(4.0, settlement.FoodRequired * 0.16));
    }

    private static bool CanExtract(Polity polity, MaterialType materialType, Region region)
    {
        if (materialType is MaterialType.CopperOre or MaterialType.IronOre)
        {
            return polity.HasAdvancement(AdvancementId.StoneTools)
                   && polity.HasAdvancement(AdvancementId.BasicConstruction)
                   && polity.HasDiscovery(BuildMaterialDiscoveryKey(region.Id, materialType));
        }

        if (materialType == MaterialType.Salt)
        {
            return polity.HasDiscovery(BuildMaterialDiscoveryKey(region.Id, materialType));
        }

        return true;
    }

    private static double ResolveExtractionOutput(MaterialType materialType, double extractionLabor, double abundance, double extractionModifier, Polity polity)
    {
        double baseRate = materialType switch
        {
            MaterialType.Wood => 0.85,
            MaterialType.Stone => 0.60,
            MaterialType.Clay => 0.52,
            MaterialType.Fiber => 0.56,
            MaterialType.Salt => 0.38,
            MaterialType.CopperOre => 0.18,
            MaterialType.IronOre => 0.12,
            _ => 0.0
        };

        double capabilityBonus = materialType switch
        {
            MaterialType.Stone or MaterialType.Clay or MaterialType.Wood when polity.HasAdvancement(AdvancementId.BasicConstruction) => 1.15,
            MaterialType.CopperOre or MaterialType.IronOre when polity.HasAdvancement(AdvancementId.CraftSpecialization) => 1.20,
            _ => 1.0
        };

        return extractionLabor * abundance * baseRate * extractionModifier * capabilityBonus;
    }

    private static double ResolveRecipeLaborRate(MaterialType materialType)
        => materialType switch
        {
            MaterialType.SimpleTools => 0.28,
            MaterialType.Textiles => 0.34,
            MaterialType.Pottery => 0.36,
            MaterialType.PreservedFood => 0.42,
            _ => 0.48
        };

    private static double ResolveMaterialNeedShare(Settlement settlement, MaterialType materialType)
    {
        double target = settlement.MaterialTargetReserves[materialType];
        if (target <= 0.01)
        {
            return 0.0;
        }

        return Math.Max(0.0, target - settlement.GetMaterialStockpile(materialType)) / target;
    }

    private static bool IsCriticalMaterial(MaterialType materialType)
        => materialType is MaterialType.SimpleTools or MaterialType.Pottery or MaterialType.PreservedFood or MaterialType.Salt;

    private static string BuildMaterialDiscoveryKey(int regionId, MaterialType materialType)
        => $"region-material:{regionId}:{materialType}";

    public static string GetMaterialLabel(MaterialType materialType)
        => materialType switch
        {
            MaterialType.Wood => "Wood",
            MaterialType.Stone => "Stone",
            MaterialType.Clay => "Clay",
            MaterialType.Fiber => "Fiber",
            MaterialType.Salt => "Salt",
            MaterialType.CopperOre => "Copper Ore",
            MaterialType.IronOre => "Iron Ore",
            MaterialType.Lumber => "Lumber",
            MaterialType.StoneBlocks => "Stone Blocks",
            MaterialType.Pottery => "Pottery",
            MaterialType.Rope => "Rope",
            MaterialType.Textiles => "Textiles",
            MaterialType.SimpleTools => "Simple Tools",
            MaterialType.PreservedFood => "Preserved Food",
            _ => materialType.ToString()
        };

    public static string DescribeSpecialization(SettlementSpecializationTag tag)
        => tag switch
        {
            SettlementSpecializationTag.LumberCenter => "timber work",
            SettlementSpecializationTag.Stoneworks => "stoneworking",
            SettlementSpecializationTag.PotteryTradition => "pottery",
            SettlementSpecializationTag.Ropeworks => "ropework",
            SettlementSpecializationTag.TextileWorks => "textiles",
            SettlementSpecializationTag.PreservationCenter => "food preservation",
            SettlementSpecializationTag.ToolmakingCenter => "toolmaking",
            SettlementSpecializationTag.OreWorks => "ore extraction",
            _ => "craft production"
        };

    private static void EmitProductionMilestoneIfNeeded(
        World world,
        WorldLookup lookup,
        Polity polity,
        Settlement settlement,
        Region region,
        MaterialType materialType,
        double output)
    {
        if (output <= 0.05)
        {
            return;
        }

        string milestoneKey = $"production-milestone:{materialType}";
        if (!settlement.MaterialMilestonesRecorded.Add(milestoneKey))
        {
            return;
        }

        string? narrative = materialType switch
        {
            MaterialType.Pottery => $"{settlement.Name} established a pottery tradition",
            MaterialType.Lumber => $"{settlement.Name} began shaping timber in {region.Name}",
            MaterialType.StoneBlocks => $"{settlement.Name} began cutting stone in {region.Name}",
            _ => null
        };

        if (narrative is null)
        {
            return;
        }

        world.AddEvent(
            WorldEventType.ProductionMilestone,
            materialType == MaterialType.Pottery ? WorldEventSeverity.Major : WorldEventSeverity.Notable,
            narrative,
            $"{settlement.Name} reached a first sustained production milestone in {GetMaterialLabel(materialType).ToLowerInvariant()}.",
            reason: $"production_milestone_{materialType.ToString().ToLowerInvariant()}",
            scope: WorldEventScope.Local,
            polityId: polity.Id,
            polityName: polity.Name,
            speciesId: polity.SpeciesId,
            speciesName: lookup.GetRequiredSpecies(polity.SpeciesId, "Production milestone").Name,
            regionId: region.Id,
            regionName: region.Name,
            settlementId: settlement.Id,
            settlementName: settlement.Name,
            metadata: new Dictionary<string, string>
            {
                ["materialType"] = materialType.ToString()
            });
    }

    private static int ResolvePriorityBucket(WorldLookup lookup, int senderRegionId, int receiverRegionId)
    {
        if (senderRegionId == receiverRegionId)
        {
            return 0;
        }

        if (lookup.TryGetRegion(senderRegionId, out Region? senderRegion)
            && senderRegion is not null
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
