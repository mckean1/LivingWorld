using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class DomesticationSystem
{
    private readonly DomesticationSettings _settings;

    public DomesticationSystem(DomesticationSettings? settings = null)
    {
        _settings = settings ?? new DomesticationSettings();
    }

    public void UpdateMonthlyKnowledgeAndSources(World world)
    {
        WorldLookup lookup = new(world);

        foreach (Polity polity in world.Polities.Where(candidate => candidate.Population > 0 && candidate.HasSettlements))
        {
            polity.FoodManagedThisMonth = 0;

            foreach (Settlement settlement in polity.Settlements)
            {
                settlement.ManagedAnimalFoodThisMonth = 0;
                settlement.ManagedCropFoodThisMonth = 0;
            }

            AccumulatePlantFamiliarity(lookup, polity);
            IdentifyAnimalCandidates(world, lookup, polity);
            IdentifyCultivablePlants(world, lookup, polity);
            EstablishManagedHerds(world, lookup, polity);
            EstablishCultivatedCrops(world, lookup, polity);
        }
    }

    public void ProduceManagedAnimalFood(World world)
    {
        WorldLookup lookup = new(world);

        foreach (Polity polity in world.Polities.Where(candidate => candidate.Population > 0 && candidate.HasSettlements))
        {
            foreach (Settlement settlement in polity.Settlements)
            {
                foreach (ManagedHerd herd in settlement.ManagedHerds)
                {
                    if (!lookup.TryGetSpecies(herd.BaseSpeciesId, out Species? species) || species is null)
                    {
                        continue;
                    }

                    double seasonalFactor = world.Time.Season switch
                    {
                        Season.Winter => 0.72,
                        Season.Spring => 1.08,
                        Season.Summer => 1.12,
                        _ => 0.96
                    };
                    double foodProduced = herd.HerdSize
                        * species.MeatYield
                        * _settings.ManagedHerdFoodFactor
                        * herd.Reliability
                        * seasonalFactor;

                    if (foodProduced <= 0)
                    {
                        continue;
                    }

                    settlement.ManagedAnimalFoodThisMonth += foodProduced;
                    settlement.ManagedFoodThisYear += foodProduced;
                    polity.FoodManagedThisMonth += foodProduced;
                    polity.AnnualFoodManaged += foodProduced;
                    polity.FoodStores += foodProduced;

                    double herdGrowth = herd.HerdSize * herd.BreedingMultiplier * _settings.ManagedHerdGrowthFactor;
                    herd.HerdSize = Math.Clamp((int)Math.Round(herd.HerdSize + herdGrowth), 2, 48);
                }
            }
        }
    }

    public void UpdateAnnualManagedFood(World world)
    {
        if (world.Time.Month != 12)
        {
            return;
        }

        WorldLookup lookup = new(world);

        foreach (Polity polity in world.Polities.Where(candidate => candidate.Population > 0 && candidate.HasSettlements))
        {
            int herdCount = polity.Settlements.Sum(settlement => settlement.ManagedHerds.Count);
            int cropCount = polity.Settlements.Sum(settlement => settlement.CultivatedCrops.Count);
            double managedShare = polity.AnnualFoodConsumed <= 0
                ? 0.0
                : polity.AnnualFoodManaged / polity.AnnualFoodConsumed;
            bool managedFoodEstablished = (herdCount > 0 || cropCount > 0)
                && managedShare >= _settings.AnnualManagedFoodStabilityShare
                && polity.StarvationMonthsThisYear <= 2;

            if (!managedFoodEstablished)
            {
                polity.ManagedFoodSupplyEstablished = false;
                continue;
            }

            if (polity.ManagedFoodSupplyEstablished)
            {
                continue;
            }

            polity.ManagedFoodSupplyEstablished = true;
            polity.ManagedFoodSupplyEstablishedYear = world.Time.Year;

            Region region = lookup.GetRequiredRegion(polity.RegionId, "Annual managed food stability");
            Species species = lookup.GetRequiredSpecies(polity.SpeciesId, "Annual managed food stability");
            string managedSourceSummary = (cropCount, herdCount) switch
            {
                (> 0, > 0) => "managed crops and herds",
                (> 0, 0) => "managed crops",
                (0, > 0) => "managed herds",
                _ => "managed food sources"
            };
            world.AddEvent(
                WorldEventType.AgricultureStabilizedFoodSupply,
                managedShare >= 0.30 ? WorldEventSeverity.Legendary : WorldEventSeverity.Major,
                $"{polity.Name} established {managedSourceSummary}",
                $"{polity.Name} covered {managedShare:P0} of annual consumption through managed food sources.",
                reason: "managed_food_supply_established",
                scope: WorldEventScope.Polity,
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: polity.SpeciesId,
                speciesName: species.Name,
                regionId: region.Id,
                regionName: region.Name,
                before: new Dictionary<string, string>
                {
                    ["managedFoodEstablished"] = false.ToString()
                },
                after: new Dictionary<string, string>
                {
                    ["managedFoodEstablished"] = true.ToString(),
                    ["managedFoodShare"] = managedShare.ToString("F2"),
                    ["managedFood"] = polity.AnnualFoodManaged.ToString("F1")
                },
                metadata: new Dictionary<string, string>
                {
                    ["herdCount"] = herdCount.ToString(),
                    ["cropCount"] = cropCount.ToString(),
                    ["managedFoodState"] = "established",
                    ["managedSourceSummary"] = managedSourceSummary
                });
        }
    }

    public double GetCropYieldMultiplier(Settlement settlement)
    {
        if (settlement.CultivatedCrops.Count == 0)
        {
            return 1.0;
        }

        double bonus = settlement.CultivatedCrops.Sum(crop => crop.YieldMultiplier);
        return 1.0 + Math.Clamp(bonus, 0.0, 0.75);
    }

    public double GetCropSeasonalResilience(Settlement settlement)
    {
        if (settlement.CultivatedCrops.Count == 0)
        {
            return 0.0;
        }

        return Math.Clamp(settlement.CultivatedCrops.Sum(crop => crop.SeasonalResilience), 0.0, 0.30);
    }

    public double GetCropManagedFoodBonus(Settlement settlement, double baseYield)
    {
        if (settlement.CultivatedCrops.Count == 0 || baseYield <= 0)
        {
            return 0.0;
        }

        double multiplier = GetCropYieldMultiplier(settlement) - 1.0;
        return Math.Max(0.0, baseYield * multiplier);
    }

    private void AccumulatePlantFamiliarity(WorldLookup lookup, Polity polity)
    {
        double stressBonus = polity.FoodNeededThisMonth <= 0
            ? 0.0
            : Math.Clamp(polity.FoodShortageThisMonth / polity.FoodNeededThisMonth, 0.0, 1.0) * _settings.FoodStressCultivationBonus;
        double gatheringSignal = polity.FoodNeededThisMonth <= 0
            ? 0.0
            : Math.Clamp(polity.FoodGatheredThisMonth / polity.FoodNeededThisMonth, 0.0, 1.0) * 0.04;

        foreach (Settlement settlement in polity.Settlements)
        {
            if (!lookup.TryGetRegion(settlement.RegionId, out Region? region) || region is null)
            {
                continue;
            }

            foreach (RegionSpeciesPopulation population in region.SpeciesPopulations.Where(candidate => candidate.PopulationCount > 0))
            {
                if (!lookup.TryGetSpecies(population.SpeciesId, out Species? species) || species is null || species.TrophicRole != TrophicRole.Producer)
                {
                    continue;
                }

                double suitability = ResolveCultivationSuitability(species, region);
                if (suitability <= 0.30 || species.IsToxicToEat)
                {
                    continue;
                }

                double settlementBonus = settlement.YearsEstablished >= 2 ? 0.02 : 0.0;
                polity.IncreaseCultivationFamiliarity(
                    species.Id,
                    (_settings.BaseCultivationFamiliarityGain + gatheringSignal + stressBonus + settlementBonus) * suitability);
            }
        }
    }

    private void IdentifyAnimalCandidates(World world, WorldLookup lookup, Polity polity)
    {
        foreach (Settlement settlement in polity.Settlements)
        {
            if (!lookup.TryGetRegion(settlement.RegionId, out Region? region) || region is null)
            {
                continue;
            }

            foreach (RegionSpeciesPopulation population in region.SpeciesPopulations.Where(candidate => candidate.PopulationCount > 0))
            {
                if (!lookup.TryGetSpecies(population.SpeciesId, out Species? species) || species is null)
                {
                    continue;
                }

                if (species.IsSapient || species.TrophicRole == TrophicRole.Producer)
                {
                    continue;
                }

                string discoveryKey = $"species-domestication-candidate:{species.Id}";
                if (polity.HasDiscovery(discoveryKey))
                {
                    continue;
                }

                double suitability = ResolveAnimalSuitability(species, population);
                int successfulHunts = polity.SuccessfulHuntsBySpecies.TryGetValue(species.Id, out int successes) ? successes : 0;
                double interest = polity.DomesticationInterestBySpecies.TryGetValue(species.Id, out double value) ? value : 0.0;
                bool repeatedInteraction = successfulHunts >= _settings.MinimumSuccessfulHuntsForCandidate || interest >= _settings.AnimalCandidateInterestThreshold;
                bool coexistence = settlement.YearsEstablished >= 1 && population.PopulationCount >= 6;

                if (!repeatedInteraction || !coexistence || suitability < _settings.AnimalCandidateSuitabilityThreshold)
                {
                    continue;
                }

                if (!polity.AddDiscovery(new CulturalDiscovery(
                        discoveryKey,
                        $"{species.Name} Manageable",
                        CulturalDiscoveryCategory.AnimalBehavior,
                        species.Id,
                        region.Id)))
                {
                    continue;
                }

                WorldEventSeverity severity = suitability >= 0.68
                    ? WorldEventSeverity.Major
                    : WorldEventSeverity.Notable;
                world.AddEvent(
                    WorldEventType.SpeciesDomesticationCandidateIdentified,
                    severity,
                    $"{polity.Name} discovered that {species.Name} could be kept near camp",
                    $"{polity.Name} identified {species.Name} as a manageable settlement-adjacent animal in {region.Name}.",
                    reason: "repeated_settlement_contact",
                    scope: WorldEventScope.Local,
                    polityId: polity.Id,
                    polityName: polity.Name,
                    speciesId: polity.SpeciesId,
                    speciesName: lookup.GetRequiredSpecies(polity.SpeciesId, "Animal domestication candidate").Name,
                    regionId: region.Id,
                    regionName: region.Name,
                    settlementId: settlement.Id,
                    settlementName: settlement.Name,
                    metadata: new Dictionary<string, string>
                    {
                        ["targetSpeciesId"] = species.Id.ToString(),
                        ["targetSpeciesName"] = species.Name,
                        ["suitability"] = suitability.ToString("F2"),
                        ["domesticationInterest"] = interest.ToString("F2")
                    });
            }
        }
    }

    private void IdentifyCultivablePlants(World world, WorldLookup lookup, Polity polity)
    {
        foreach (Settlement settlement in polity.Settlements)
        {
            if (!lookup.TryGetRegion(settlement.RegionId, out Region? region) || region is null)
            {
                continue;
            }

            foreach (RegionSpeciesPopulation population in region.SpeciesPopulations.Where(candidate => candidate.PopulationCount > 0))
            {
                if (!lookup.TryGetSpecies(population.SpeciesId, out Species? species) || species is null || species.TrophicRole != TrophicRole.Producer)
                {
                    continue;
                }

                string discoveryKey = $"species-cultivable:{species.Id}";
                if (polity.HasDiscovery(discoveryKey))
                {
                    continue;
                }

                double familiarity = polity.CultivationFamiliarityBySpecies.TryGetValue(species.Id, out double value) ? value : 0.0;
                double suitability = ResolveCultivationSuitability(species, region);
                bool settledPressure = settlement.YearsEstablished >= 1 && polity.HasAdvancement(AdvancementId.SeasonalPlanning);

                if (!settledPressure || familiarity < _settings.PlantDiscoveryThreshold || suitability < 0.45 || species.IsToxicToEat)
                {
                    continue;
                }

                if (!polity.AddDiscovery(new CulturalDiscovery(
                        discoveryKey,
                        $"{species.Name} Cultivable",
                        CulturalDiscoveryCategory.SpeciesUse,
                        species.Id,
                        region.Id)))
                {
                    continue;
                }

                world.AddEvent(
                    WorldEventType.PlantCultivationDiscovered,
                    suitability >= 0.70 ? WorldEventSeverity.Major : WorldEventSeverity.Notable,
                    $"{polity.Name} discovered that {species.Name} could be cultivated",
                    $"{polity.Name} recognized {species.Name} as a useful plant for deliberate cultivation in {region.Name}.",
                    reason: "repeated_plant_familiarity",
                    scope: WorldEventScope.Local,
                    polityId: polity.Id,
                    polityName: polity.Name,
                    speciesId: polity.SpeciesId,
                    speciesName: lookup.GetRequiredSpecies(polity.SpeciesId, "Plant cultivation discovery").Name,
                    regionId: region.Id,
                    regionName: region.Name,
                    settlementId: settlement.Id,
                    settlementName: settlement.Name,
                    metadata: new Dictionary<string, string>
                    {
                        ["targetSpeciesId"] = species.Id.ToString(),
                        ["targetSpeciesName"] = species.Name,
                        ["cultivationFamiliarity"] = familiarity.ToString("F2"),
                        ["cultivationSuitability"] = suitability.ToString("F2")
                    });
            }
        }
    }

    private void EstablishManagedHerds(World world, WorldLookup lookup, Polity polity)
    {
        foreach (Settlement settlement in polity.Settlements)
        {
            if (!lookup.TryGetRegion(settlement.RegionId, out Region? region) || region is null)
            {
                continue;
            }

            foreach (RegionSpeciesPopulation population in region.SpeciesPopulations.Where(candidate => candidate.PopulationCount > 0))
            {
                if (!lookup.TryGetSpecies(population.SpeciesId, out Species? species) || species is null)
                {
                    continue;
                }

                if (species.IsSapient || species.TrophicRole == TrophicRole.Producer || settlement.GetManagedHerd(species.Id) is not null)
                {
                    continue;
                }

                if (!polity.HasDiscovery($"species-domestication-candidate:{species.Id}")
                    || !polity.HasAdvancement(AdvancementId.SeasonalPlanning)
                    || !polity.HasAdvancement(AdvancementId.FoodStorage))
                {
                    continue;
                }

                int successfulHunts = polity.SuccessfulHuntsBySpecies.TryGetValue(species.Id, out int successes) ? successes : 0;
                double interest = polity.DomesticationInterestBySpecies.TryGetValue(species.Id, out double value) ? value : 0.0;
                double suitability = ResolveAnimalSuitability(species, population);

                if (successfulHunts < _settings.MinimumSuccessfulHuntsForHerd
                    || interest < _settings.HerdEstablishmentInterestThreshold
                    || suitability < _settings.HerdEstablishmentSuitabilityThreshold
                    || settlement.YearsEstablished < 1
                    || population.PopulationCount < 8)
                {
                    continue;
                }

                int startingHerdSize = Math.Clamp((int)Math.Round(population.PopulationCount * 0.18), 4, 16);
                population.PopulationCount = Math.Max(0, population.PopulationCount - startingHerdSize);
                ManagedHerd herd = new(
                    species.Id,
                    $"Domestic {species.Name}",
                    world.Time.Year,
                    world.Time.Month,
                    startingHerdSize,
                    reliability: Math.Clamp((species.DomesticationAffinity * 0.55) + (1.0 - PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Aggression)) * 0.25 + PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Sociality) * 0.20, 0.30, 0.92),
                    foodYieldPerMonth: Math.Max(0.6, species.MeatYield * 0.10),
                    breedingMultiplier: Math.Clamp(PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Fertility), 0.35, 1.15),
                    aggressionReduction: Math.Clamp(PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Aggression) * 0.35, 0.02, 0.30));
                settlement.ManagedHerds.Add(herd);

                bool spread = polity.Settlements.Count(other => other.ManagedHerds.Any(existing => existing.BaseSpeciesId == species.Id)) > 1;
                if (spread)
                {
                    EmitSpreadEvent(world, lookup, polity, settlement, region, species, "herd");
                }

                world.AddEvent(
                    WorldEventType.AnimalDomesticated,
                    spread ? WorldEventSeverity.Major : WorldEventSeverity.Major,
                    $"{settlement.Name} established herds of {species.Name}",
                    $"{settlement.Name} captured and managed {startingHerdSize} {species.Name} in {region.Name}.",
                    reason: "animal_domestication_succeeded",
                    scope: WorldEventScope.Local,
                    polityId: polity.Id,
                    polityName: polity.Name,
                    speciesId: polity.SpeciesId,
                    speciesName: lookup.GetRequiredSpecies(polity.SpeciesId, "Animal domesticated").Name,
                    regionId: region.Id,
                    regionName: region.Name,
                    settlementId: settlement.Id,
                    settlementName: settlement.Name,
                    metadata: new Dictionary<string, string>
                    {
                        ["targetSpeciesId"] = species.Id.ToString(),
                        ["targetSpeciesName"] = species.Name,
                        ["variantName"] = herd.VariantName,
                        ["managedKind"] = "herd",
                        ["herdSize"] = herd.HerdSize.ToString()
                    });
            }
        }
    }

    private void EstablishCultivatedCrops(World world, WorldLookup lookup, Polity polity)
    {
        if (!polity.HasAdvancement(AdvancementId.Agriculture))
        {
            return;
        }

        foreach (Settlement settlement in polity.Settlements)
        {
            if (!lookup.TryGetRegion(settlement.RegionId, out Region? region) || region is null)
            {
                continue;
            }

            foreach (RegionSpeciesPopulation population in region.SpeciesPopulations.Where(candidate => candidate.PopulationCount > 0))
            {
                if (!lookup.TryGetSpecies(population.SpeciesId, out Species? species) || species is null || species.TrophicRole != TrophicRole.Producer)
                {
                    continue;
                }

                if (settlement.GetCultivatedCrop(species.Id) is not null || !polity.HasDiscovery($"species-cultivable:{species.Id}"))
                {
                    continue;
                }

                double familiarity = polity.CultivationFamiliarityBySpecies.TryGetValue(species.Id, out double value) ? value : 0.0;
                double suitability = ResolveCultivationSuitability(species, region);

                if (familiarity < _settings.CropEstablishmentThreshold
                    || suitability < 0.50
                    || settlement.CultivatedLand < 0.4
                    || settlement.YearsEstablished < 1)
                {
                    continue;
                }

                CultivatedCrop crop = new(
                    species.Id,
                    ResolveCropName(species),
                    world.Time.Year,
                    world.Time.Month,
                    yieldMultiplier: Math.Clamp(species.CultivationAffinity * _settings.CropYieldBonusScale, 0.04, 0.22),
                    stabilityBonus: Math.Clamp(suitability * _settings.CropStabilityBonusScale, 0.03, 0.14),
                    seasonalResilience: Math.Clamp((species.WaterPreference * 0.08) + (species.FertilityPreference * 0.06), 0.02, 0.14));
                settlement.CultivatedCrops.Add(crop);

                bool spread = polity.Settlements.Count(other => other.CultivatedCrops.Any(existing => existing.BaseSpeciesId == species.Id)) > 1;
                if (spread)
                {
                    EmitSpreadEvent(world, lookup, polity, settlement, region, species, "crop");
                }

                world.AddEvent(
                    WorldEventType.CropEstablished,
                    spread ? WorldEventSeverity.Major : WorldEventSeverity.Major,
                    $"{settlement.Name} began cultivating {crop.CropName} in {region.Name}",
                    $"{settlement.Name} established managed plots of {species.Name} in {region.Name}.",
                    reason: "crop_established_from_discovery",
                    scope: WorldEventScope.Local,
                    polityId: polity.Id,
                    polityName: polity.Name,
                    speciesId: polity.SpeciesId,
                    speciesName: lookup.GetRequiredSpecies(polity.SpeciesId, "Crop established").Name,
                    regionId: region.Id,
                    regionName: region.Name,
                    settlementId: settlement.Id,
                    settlementName: settlement.Name,
                    parentEventIds: polity.LastLearnedAgricultureEventId is long eventId ? [eventId] : null,
                    metadata: new Dictionary<string, string>
                    {
                        ["targetSpeciesId"] = species.Id.ToString(),
                        ["targetSpeciesName"] = species.Name,
                        ["cropName"] = crop.CropName,
                        ["managedKind"] = "crop"
                    });
            }
        }
    }

    private void EmitSpreadEvent(World world, WorldLookup lookup, Polity polity, Settlement settlement, Region region, Species sourceSpecies, string managedKind)
    {
        world.AddEvent(
            WorldEventType.DomesticationSpread,
            WorldEventSeverity.Major,
            managedKind == "herd"
                ? $"Managed herds of {sourceSpecies.Name} spread among {polity.Name}"
                : $"{ResolveCropName(sourceSpecies)} spread among the settlements of {polity.Name}",
            $"{polity.Name} established the same managed {managedKind} in more than one settlement.",
            reason: managedKind == "herd" ? "herd_spread" : "crop_spread",
            scope: WorldEventScope.Polity,
            polityId: polity.Id,
            polityName: polity.Name,
            speciesId: polity.SpeciesId,
            speciesName: lookup.GetRequiredSpecies(polity.SpeciesId, "Domestication spread").Name,
            regionId: region.Id,
            regionName: region.Name,
            settlementId: settlement.Id,
            settlementName: settlement.Name,
            metadata: new Dictionary<string, string>
            {
                ["targetSpeciesId"] = sourceSpecies.Id.ToString(),
                ["targetSpeciesName"] = sourceSpecies.Name,
                ["managedKind"] = managedKind
            });
    }

    private static double ResolveAnimalSuitability(Species species, RegionSpeciesPopulation population)
    {
        double sociality = PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Sociality);
        double aggression = PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Aggression);
        double fertility = PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Fertility);
        double dietFlexibility = PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.DietFlexibility);
        double size = PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Size);
        double mobilityPenalty = species.MigrationCapability * 0.20;
        double sizeManageability = 1.0 - Math.Max(0.0, size - 0.65);

        return Math.Clamp(
            (species.DomesticationAffinity * 0.34)
            + (sociality * 0.18)
            + ((1.0 - aggression) * 0.16)
            + (fertility * 0.12)
            + (dietFlexibility * 0.10)
            + (sizeManageability * 0.10)
            - mobilityPenalty,
            0.0,
            1.0);
    }

    private static double ResolveCultivationSuitability(Species species, Region region)
    {
        return Math.Clamp(
            (species.CultivationAffinity * 0.46)
            + (species.FertilityPreference * 0.18)
            + (species.WaterPreference * 0.14)
            + (region.Fertility * 0.14)
            + (region.WaterAvailability * 0.08),
            0.0,
            1.0);
    }

    private static string ResolveCropName(Species species)
    {
        if (species.Name.Contains("grass", StringComparison.OrdinalIgnoreCase))
        {
            return $"{species.Name} grain";
        }

        if (species.Name.Contains("root", StringComparison.OrdinalIgnoreCase))
        {
            return $"{species.Name} crop";
        }

        if (species.Name.Contains("reed", StringComparison.OrdinalIgnoreCase))
        {
            return $"{species.Name} plots";
        }

        return species.Name;
    }
}
