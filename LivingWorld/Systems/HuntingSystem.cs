using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class HuntingSystem
{
    private const double AdultPopulationShare = 0.55;
    private const double BaseHuntingRatio = 0.12;

    public void UpdateSeason(World world)
    {
        foreach (Polity polity in world.Polities.Where(candidate => candidate.Population > 0 && candidate.HasSettlements))
        {
            Region region = world.Regions.First(region => region.Id == polity.RegionId);
            List<RegionSpeciesPopulation> viableTargets = region.SpeciesPopulations
                .Where(population => population.PopulationCount > 0)
                .Where(population =>
                {
                    Species species = world.Species.First(candidate => candidate.Id == population.SpeciesId);
                    return !species.IsSapient && species.TrophicRole != TrophicRole.Producer;
                })
                .ToList();

            if (viableTargets.Count == 0)
            {
                continue;
            }

            int settlementCount = Math.Max(1, polity.SettlementCount);
            int adultPopulation = (int)Math.Round(polity.Population * AdultPopulationShare);
            int totalHunters = Math.Max(1, (int)Math.Round(adultPopulation * BaseHuntingRatio));
            int huntersPerSettlement = Math.Max(1, totalHunters / settlementCount);

            for (int settlementIndex = 0; settlementIndex < settlementCount; settlementIndex++)
            {
                RegionSpeciesPopulation? targetPopulation = SelectTargetSpecies(world, polity, viableTargets);
                if (targetPopulation is null)
                {
                    continue;
                }

                ResolveHunt(world, polity, region, targetPopulation, huntersPerSettlement);
            }
        }
    }

    private static RegionSpeciesPopulation? SelectTargetSpecies(World world, Polity polity, IReadOnlyList<RegionSpeciesPopulation> targets)
    {
        return targets
            .OrderByDescending(population =>
            {
                Species species = world.Species.First(candidate => candidate.Id == population.SpeciesId);
                double expectedYield = PopulationTraitResolver.GetEffectiveMeatYield(species, population) * Math.Min(population.PopulationCount, 20);
                double familiarity = polity.SuccessfulHuntsBySpecies.TryGetValue(species.Id, out int successes)
                    ? successes * 6.0
                    : 0.0;
                double safetyPenalty = PopulationTraitResolver.GetEffectiveHuntingDanger(species, population) * 45.0;
                double difficultyPenalty = PopulationTraitResolver.GetEffectiveHuntingDifficulty(species, population) * 35.0;
                double toxicPenalty = polity.KnownToxicSpeciesIds.Contains(species.Id) ? 90.0 : 0.0;
                double knowledgeBonus = polity.KnownEdibleSpeciesIds.Contains(species.Id) ? 15.0 : 0.0;
                double scarcityPenalty = population.CarryingCapacity <= 0
                    ? 18.0
                    : (1.0 - Math.Clamp((double)population.PopulationCount / population.CarryingCapacity, 0.0, 1.0)) * 28.0;
                double accessibility = population.HabitatSuitability * 25.0;
                double domesticationBias = species.DomesticationAffinity * 6.0;

                return expectedYield + familiarity + knowledgeBonus + accessibility + domesticationBias
                    - safetyPenalty - difficultyPenalty - toxicPenalty - scarcityPenalty;
            })
            .FirstOrDefault();
    }

    private static void ResolveHunt(World world, Polity polity, Region region, RegionSpeciesPopulation targetPopulation, int huntersCommitted)
    {
        Species targetSpecies = world.Species.First(species => species.Id == targetPopulation.SpeciesId);
        int targetBefore = targetPopulation.PopulationCount;
        if (targetBefore <= 0)
        {
            return;
        }

        double effectiveDanger = PopulationTraitResolver.GetEffectiveHuntingDanger(targetSpecies, targetPopulation);
        double effectiveDifficulty = PopulationTraitResolver.GetEffectiveHuntingDifficulty(targetSpecies, targetPopulation);
        double effectiveYield = PopulationTraitResolver.GetEffectiveMeatYield(targetSpecies, targetPopulation);
        double knownEdibleBonus = polity.KnownEdibleSpeciesIds.Contains(targetSpecies.Id) ? 0.15 : 0.0;
        double priorSuccessBonus = polity.SuccessfulHuntsBySpecies.TryGetValue(targetSpecies.Id, out int successes)
            ? Math.Min(0.22, successes * 0.03)
            : 0.0;
        double dangerPenalty = effectiveDanger * 0.22;
        double difficultyPenalty = effectiveDifficulty * 0.28;
        double accessibilityPenalty = (1.0 - targetPopulation.HabitatSuitability) * 0.18;
        double successChance = Math.Clamp(0.48 + knownEdibleBonus + priorSuccessBonus - dangerPenalty - difficultyPenalty - accessibilityPenalty, 0.12, 0.88);

        double huntPower = huntersCommitted * successChance;
        int harvestCap = Math.Max(1, (int)Math.Round(targetBefore * 0.16));
        int huntedCount = Math.Min(harvestCap, Math.Max(0, (int)Math.Round(huntPower / Math.Max(1.0, 1.0 + effectiveDifficulty * 2.5))));
        double meatGained = huntedCount * effectiveYield;
        int casualties = (int)Math.Round(huntersCommitted * effectiveDanger * (successChance < 0.45 ? 0.12 : 0.05));

        if (targetSpecies.IsToxicToEat && !polity.KnownToxicSpeciesIds.Contains(targetSpecies.Id))
        {
            polity.KnownToxicSpeciesIds.Add(targetSpecies.Id);
            polity.RecordFailedHunt(targetSpecies.Id);
            polity.IncreaseDomesticationInterest(targetSpecies.Id, 0.08);
            polity.AddDiscovery(new CulturalDiscovery(
                Key: $"species-toxic:{targetSpecies.Id}",
                Summary: $"{targetSpecies.Name} Toxic",
                Category: CulturalDiscoveryCategory.FoodSafety,
                SpeciesId: targetSpecies.Id,
                RegionId: region.Id));

            world.AddEvent(
                WorldEventType.ToxicFoodDiscovered,
                WorldEventSeverity.Notable,
                $"{polity.Name} discovered that {targetSpecies.Name} is toxic",
                $"{polity.Name} identified {targetSpecies.Name} as dangerous to eat after a failed hunt in {region.Name}.",
                reason: "toxic_species_discovered",
                scope: WorldEventScope.Polity,
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: polity.SpeciesId,
                speciesName: world.Species.First(species => species.Id == polity.SpeciesId).Name,
                regionId: region.Id,
                regionName: region.Name,
                metadata: new Dictionary<string, string>
                {
                    ["targetSpeciesId"] = targetSpecies.Id.ToString(),
                    ["targetSpeciesName"] = targetSpecies.Name
                });
            return;
        }

        if (huntedCount <= 0)
        {
            polity.RecordFailedHunt(targetSpecies.Id);
            targetPopulation.RecentHuntingPressure = Math.Clamp(targetPopulation.RecentHuntingPressure + 0.10, 0.0, 1.0);

            if (casualties > 0)
            {
                ApplyCasualties(polity, casualties);
                world.AddEvent(
                    WorldEventType.HuntingDisaster,
                    WorldEventSeverity.Major,
                    $"{polity.Name} suffered a disastrous hunt in {region.Name}",
                    $"{casualties} hunters died or were lost while pursuing {targetSpecies.Name}.",
                    reason: "hunt_failed",
                    scope: WorldEventScope.Polity,
                    polityId: polity.Id,
                    polityName: polity.Name,
                    speciesId: polity.SpeciesId,
                    speciesName: world.Species.First(species => species.Id == polity.SpeciesId).Name,
                    regionId: region.Id,
                    regionName: region.Name,
                    metadata: new Dictionary<string, string>
                    {
                        ["targetSpeciesId"] = targetSpecies.Id.ToString(),
                        ["targetSpeciesName"] = targetSpecies.Name
                    });
            }

            return;
        }

        if (!polity.KnownEdibleSpeciesIds.Contains(targetSpecies.Id))
        {
            polity.KnownEdibleSpeciesIds.Add(targetSpecies.Id);
            polity.AddDiscovery(new CulturalDiscovery(
                Key: $"species-edible:{targetSpecies.Id}",
                Summary: $"{targetSpecies.Name} Edible",
                Category: CulturalDiscoveryCategory.SpeciesUse,
                SpeciesId: targetSpecies.Id,
                RegionId: region.Id));
            world.AddEvent(
                WorldEventType.EdibleSpeciesDiscovered,
                WorldEventSeverity.Notable,
                $"{polity.Name} discovered that {targetSpecies.Name} is edible",
                $"{polity.Name} found reliable meat in {targetSpecies.Name} in {region.Name}.",
                reason: "edible_species_discovered",
                scope: WorldEventScope.Polity,
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: polity.SpeciesId,
                speciesName: world.Species.First(species => species.Id == polity.SpeciesId).Name,
                regionId: region.Id,
                regionName: region.Name,
                metadata: new Dictionary<string, string>
                {
                    ["targetSpeciesId"] = targetSpecies.Id.ToString(),
                    ["targetSpeciesName"] = targetSpecies.Name
                });
        }

        targetPopulation.PopulationCount = Math.Max(0, targetPopulation.PopulationCount - huntedCount);
        targetPopulation.RecentHuntingPressure = Math.Clamp(
            targetPopulation.RecentHuntingPressure + ((double)huntedCount / Math.Max(1, targetBefore)),
            0.0,
            1.0);
        polity.FoodStores += meatGained;
        polity.FoodHuntedThisYear += meatGained;
        polity.RecordSuccessfulHunt(targetSpecies.Id);
        polity.IncreaseDomesticationInterest(targetSpecies.Id, Math.Min(0.30, huntedCount * targetSpecies.DomesticationAffinity * 0.01));

        if (casualties > 0)
        {
            ApplyCasualties(polity, casualties);
            polity.KnownDangerousPreySpeciesIds.Add(targetSpecies.Id);
            polity.AddDiscovery(new CulturalDiscovery(
                Key: $"species-dangerous-prey:{targetSpecies.Id}",
                Summary: $"{targetSpecies.Name} Dangerous Prey",
                Category: CulturalDiscoveryCategory.AnimalBehavior,
                SpeciesId: targetSpecies.Id,
                RegionId: region.Id));

            world.AddEvent(
                WorldEventType.DangerousPreyKilledHunters,
                WorldEventSeverity.Notable,
                $"{targetSpecies.Name} killed hunters from {polity.Name}",
                $"{casualties} hunters were killed while pursuing {targetSpecies.Name} in {region.Name}.",
                reason: "dangerous_prey_casualties",
                scope: WorldEventScope.Polity,
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: polity.SpeciesId,
                speciesName: world.Species.First(species => species.Id == polity.SpeciesId).Name,
                regionId: region.Id,
                regionName: region.Name,
                metadata: new Dictionary<string, string>
                {
                    ["targetSpeciesId"] = targetSpecies.Id.ToString(),
                    ["targetSpeciesName"] = targetSpecies.Name,
                    ["casualties"] = casualties.ToString()
                });
        }

        EmitHuntOutcome(world, polity, region, targetSpecies, targetBefore, targetPopulation, huntedCount, meatGained, casualties);
        EmitPressureEvents(world, polity, region, targetSpecies, targetBefore, targetPopulation);
    }

    private static void EmitHuntOutcome(
        World world,
        Polity polity,
        Region region,
        Species targetSpecies,
        int targetBefore,
        RegionSpeciesPopulation targetPopulation,
        int huntedCount,
        double meatGained,
        int casualties)
    {
        bool legendary = PopulationTraitResolver.GetEffectiveHuntingDanger(targetSpecies, targetPopulation) >= 0.60
            && PopulationTraitResolver.GetEffectiveMeatYield(targetSpecies, targetPopulation) >= 20
            && huntedCount >= Math.Max(2, targetBefore / 8);

        if (legendary)
        {
            polity.LegendaryHuntsThisYear++;
            world.AddEvent(
                WorldEventType.LegendaryHunt,
                WorldEventSeverity.Legendary,
                $"{polity.Name} brought down a legendary {targetSpecies.Name} in {region.Name}",
                $"{polity.Name} slew {huntedCount} {targetSpecies.Name} for {meatGained:F0} food despite deadly risk.",
                reason: "legendary_hunt",
                scope: WorldEventScope.Polity,
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: polity.SpeciesId,
                speciesName: world.Species.First(species => species.Id == polity.SpeciesId).Name,
                regionId: region.Id,
                regionName: region.Name,
                metadata: new Dictionary<string, string>
                {
                    ["targetSpeciesId"] = targetSpecies.Id.ToString(),
                    ["targetSpeciesName"] = targetSpecies.Name,
                    ["casualties"] = casualties.ToString()
                });
            return;
        }

        WorldEventSeverity severity = huntedCount >= Math.Max(6, targetBefore / 7)
            ? WorldEventSeverity.Major
            : WorldEventSeverity.Notable;

        world.AddEvent(
            WorldEventType.HuntingSuccess,
            severity,
            $"{polity.Name} hunted {targetSpecies.Name} in {region.Name}",
            $"{polity.Name} harvested {huntedCount} {targetSpecies.Name} for {meatGained:F0} food; population {targetBefore} -> {targetPopulation.PopulationCount}.",
            reason: "successful_hunt",
            scope: WorldEventScope.Local,
            polityId: polity.Id,
            polityName: polity.Name,
            speciesId: polity.SpeciesId,
            speciesName: world.Species.First(species => species.Id == polity.SpeciesId).Name,
            regionId: region.Id,
            regionName: region.Name,
            before: new Dictionary<string, string>
            {
                ["targetPopulation"] = targetBefore.ToString()
            },
            after: new Dictionary<string, string>
            {
                ["targetPopulation"] = targetPopulation.PopulationCount.ToString(),
                ["foodGained"] = meatGained.ToString("F0")
            },
            metadata: new Dictionary<string, string>
            {
                ["targetSpeciesId"] = targetSpecies.Id.ToString(),
                ["targetSpeciesName"] = targetSpecies.Name,
                ["casualties"] = casualties.ToString()
            });
    }

    private static void EmitPressureEvents(
        World world,
        Polity polity,
        Region region,
        Species targetSpecies,
        int targetBefore,
        RegionSpeciesPopulation targetPopulation)
    {
        if (targetPopulation.PopulationCount <= 0)
        {
            world.AddEvent(
                WorldEventType.LocalSpeciesExtinction,
                WorldEventSeverity.Major,
                $"{targetSpecies.Name} was hunted out of {region.Name}",
                $"{polity.Name} and other pressures drove {targetSpecies.Name} from {targetBefore} to 0 in {region.Name}.",
                reason: "overhunted_to_extinction",
                scope: WorldEventScope.Regional,
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: targetSpecies.Id,
                speciesName: targetSpecies.Name,
                regionId: region.Id,
                regionName: region.Name);
            return;
        }

        double pressureRatio = (double)targetPopulation.PopulationCount / Math.Max(1, targetPopulation.CarryingCapacity);
        if (pressureRatio > 0.22 && targetPopulation.RecentHuntingPressure < 0.45)
        {
            return;
        }

        world.AddEvent(
            WorldEventType.OverhuntingPressure,
            pressureRatio < 0.10 ? WorldEventSeverity.Major : WorldEventSeverity.Notable,
            $"{targetSpecies.Name} came under overhunting pressure in {region.Name}",
            $"{polity.Name} drove the local {targetSpecies.Name} population down to {targetPopulation.PopulationCount}.",
            reason: "seasonal_overhunting",
            scope: WorldEventScope.Regional,
            polityId: polity.Id,
            polityName: polity.Name,
            speciesId: targetSpecies.Id,
            speciesName: targetSpecies.Name,
            regionId: region.Id,
            regionName: region.Name,
            after: new Dictionary<string, string>
            {
                ["targetPopulation"] = targetPopulation.PopulationCount.ToString(),
                ["carryingCapacity"] = targetPopulation.CarryingCapacity.ToString()
            });
    }

    private static void ApplyCasualties(Polity polity, int casualties)
    {
        if (casualties <= 0)
        {
            return;
        }

        polity.HuntingCasualtiesThisYear += casualties;
        polity.Population = Math.Max(0, polity.Population - casualties);
    }
}
